using OwlCore.ComponentModel;
using OwlCore.Storage;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace OwlCore.Nomad.Storage;

/// <summary>
/// A virtual folder constructed by advancing an <see cref="IEventStreamHandler{TEventStreamEntry}.EventStreamPosition"/> using multiple <see cref="ISources{T}.Sources"/> in concert with other <see cref="ISharedEventStreamHandler{TContentPointer, TEventStreamSource, TEventStreamEntry, TListeningHandlers}.ListeningEventStreamHandlers"/>.
/// </summary>
public abstract class ReadOnlyNomadFolder<TContentPointer, TEventStreamSource, TEventStreamEntry> : ISharedEventStreamHandler<TContentPointer, TEventStreamSource, TEventStreamEntry>, IFolder, IMutableFolder, IChildFolder, IGetItem
    where TEventStreamSource : EventStream<TContentPointer>
    where TEventStreamEntry : EventStreamEntry<TContentPointer>
{
    /// <summary>
    /// Creates a new instance of <see cref="ReadOnlyNomadFolder{TContentPointer, TEventStreamSource, TEventStreamEntry}"/>.
    /// </summary>
    /// <param name="listeningEventStreamHandlers">The shared list of known nomad event streams participating in event seeking.</param>
    protected ReadOnlyNomadFolder(ICollection<ISharedEventStreamHandler<TContentPointer, TEventStreamSource, TEventStreamEntry>> listeningEventStreamHandlers)
    {
        listeningEventStreamHandlers.Add(this);
        ListeningEventStreamHandlers = listeningEventStreamHandlers;
    }

    /// <inheritdoc />
    public virtual ICollection<ISharedEventStreamHandler<TContentPointer, TEventStreamSource, TEventStreamEntry>> ListeningEventStreamHandlers { get; }

    /// <inheritdoc cref="IStorable.Id" />
    public required string Id { get; init; }

    /// <inheritdoc />
    public required string Name { get; init; }

    /// <inheritdoc />
    public required ICollection<TContentPointer> Sources { get; init; }

    /// <inheritdoc />
    public TEventStreamEntry? EventStreamPosition { get; set; }

    /// <summary>
    /// The parent for this folder, if any.
    /// </summary>
    public required ReadOnlyNomadFolder<TContentPointer, TEventStreamSource, TEventStreamEntry>? Parent { get; init; }

    /// <summary>
    /// The items in this folder at the current event stream position.
    /// </summary>
    public List<IStorableChild> Items { get; init; } = new();

    /// <inheritdoc />
    public virtual Task ResetEventStreamPositionAsync(CancellationToken cancellationToken)
    {
        EventStreamPosition = null;
        Items.Clear();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public abstract Task TryAdvanceEventStreamAsync(TEventStreamEntry streamEntry, CancellationToken cancellationToken);

    /// <inheritdoc />
    public virtual async IAsyncEnumerable<IStorableChild> GetItemsAsync(StorableType type = StorableType.All, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        foreach (var item in Items)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            if (type == StorableType.All || (item is IFile && type.HasFlag(StorableType.File)) || (item is IFolder && type.HasFlag(StorableType.Folder)))
                yield return item;
        }
    }

    /// <inheritdoc />
    public virtual Task<IStorableChild> GetItemAsync(string id, CancellationToken cancellationToken = default)
    {
        var target = Items.FirstOrDefault(x => x.Id == id);
        if (target is null)
            throw new FileNotFoundException();

        return Task.FromResult(target);
    }

    /// <inheritdoc />
    public abstract Task<IFolderWatcher> GetFolderWatcherAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public Task<IFolder?> GetParentAsync(CancellationToken cancellationToken = default) => Task.FromResult<IFolder?>(Parent);
}