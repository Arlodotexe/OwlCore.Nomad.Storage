using OwlCore.ComponentModel;
using OwlCore.Storage;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OwlCore.Nomad.Storage.Models;

namespace OwlCore.Nomad.Storage;

/// <summary>
/// A virtual folder constructed by advancing an <see cref="IEventStreamHandler{TEventStreamEntry}.EventStreamPosition"/> using multiple <see cref="ISources{T}.Sources"/> in concert with other <see cref="ISharedEventStreamHandler{TContentPointer, TEventStreamSource, TEventStreamEntry, TListeningHandlers}.ListeningEventStreamHandlers"/>.
/// </summary>
public abstract class ReadOnlyNomadFolder<TContentPointer, TEventStreamSource, TEventStreamEntry> : ISharedEventStreamHandler<TContentPointer, TEventStreamSource, TEventStreamEntry>, IFolder, IMutableFolder, IChildFolder, IGetItem, IDelegable<NomadFolderData<TContentPointer>>, IGetRoot
    where TEventStreamSource : EventStream<TContentPointer>
    where TEventStreamEntry : EventStreamEntry<TContentPointer>
    where TContentPointer : class
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
    public string Id => Inner.StorableItemId;

    /// <inheritdoc />
    public string Name => Inner.StorableItemName;

    /// <inheritdoc />
    public required ICollection<TContentPointer> Sources { get; init; }

    /// <inheritdoc />
    public TEventStreamEntry? EventStreamPosition { get; set; }

    /// <summary>
    /// The parent for this folder, if any.
    /// </summary>
    public required ReadOnlyNomadFolder<TContentPointer, TEventStreamSource, TEventStreamEntry>? Parent { get; init; }
    
    /// <inheritdoc />
    public required NomadFolderData<TContentPointer> Inner { get; set; }

    /// <inheritdoc />
    public virtual Task ResetEventStreamPositionAsync(CancellationToken cancellationToken)
    {
        EventStreamPosition = null;
        Inner = new NomadFolderData<TContentPointer> {  StorableItemName = Name, StorableItemId = Id };
            
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public abstract Task TryAdvanceEventStreamAsync(TEventStreamEntry streamEntry, CancellationToken cancellationToken);

    /// <inheritdoc />
    public virtual async IAsyncEnumerable<IStorableChild> GetItemsAsync(StorableType type = StorableType.All, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        if (type.HasFlag(StorableType.File))
        {
            foreach (var item in Inner.Files.ToArray())
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;

                if (item != null)
                    yield return await FileDataToInstanceAsync(item, cancellationToken);
            }
        }
            

        if (type.HasFlag(StorableType.Folder))
        {
            foreach (var item in Inner.Folders.ToArray())
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;
                
                if (item != null)
                    yield return await FolderDataToInstanceAsync(item, cancellationToken);
            }
        }
    }

    /// <inheritdoc />
    public virtual async Task<IStorableChild> GetItemAsync(string id, CancellationToken cancellationToken = default)
    {
        var fileTarget = Inner.Files.FirstOrDefault(x => x.StorableItemId == id);
        if (fileTarget is not null)
            return await FileDataToInstanceAsync(fileTarget, cancellationToken);
        
        var folderTarget = Inner.Folders.FirstOrDefault(x => x.StorableItemId == id);
        if (folderTarget is not null)
            return await FolderDataToInstanceAsync(folderTarget, cancellationToken);
        
        throw new FileNotFoundException();
    }

    /// <inheritdoc />
    public abstract Task<IFolderWatcher> GetFolderWatcherAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public Task<IFolder?> GetParentAsync(CancellationToken cancellationToken = default) => Task.FromResult<IFolder?>(Parent);

    /// <inheritdoc />
    public Task<IFolder?> GetRootAsync(CancellationToken cancellationToken = default)
    {
        // No parent = no root
        if (Parent is null)
            return Task.FromResult<IFolder?>(null);
        
        // At least one parent is required for a root to exist
        // Crawl up and return where parent is null
        var current = this;
        while (current.Parent is { } parent)
        {
            current = parent;
        }

        return Task.FromResult<IFolder?>(current);
    }

    /// <summary>
    /// Transform file data to file instance.
    /// </summary>
    /// <param name="fileData">The file data to transform.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    protected abstract Task<ReadOnlyNomadFile<TContentPointer, TEventStreamSource, TEventStreamEntry>> FileDataToInstanceAsync(NomadFileData<TContentPointer> fileData, CancellationToken cancellationToken);   
    
    /// <summary>
    /// Transforms folder data to a folder instance.
    /// </summary>
    /// <param name="folderData">The folder data to transform.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    protected abstract Task<ReadOnlyNomadFolder<TContentPointer, TEventStreamSource, TEventStreamEntry>> FolderDataToInstanceAsync(NomadFolderData<TContentPointer> folderData, CancellationToken cancellationToken); 
}