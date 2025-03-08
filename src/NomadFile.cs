using OwlCore.ComponentModel;
using OwlCore.Nomad.Storage.Models;
using OwlCore.Storage;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OwlCore.Nomad.Storage;

/// <summary>
/// A virtual file constructed by advancing an <see cref="IEventStreamHandler{TImmutablePointer, TMutablePointer, TEventStreamSource, TEventStreamEntry}.EventStreamPosition"/> using multiple <see cref="ISources{T}.Sources"/>.
/// </summary>
public abstract class NomadFile<TImmutablePointer, TMutablePointer, TEventStreamSource, TEventStreamEntry> : IFile, IChildFile, IEventStreamHandler<TImmutablePointer, TMutablePointer, TEventStreamSource, TEventStreamEntry>, IDelegable<NomadFileData<TImmutablePointer>>
    where TEventStreamSource : EventStream<TImmutablePointer>
    where TEventStreamEntry : EventStreamEntry<TImmutablePointer>
    where TImmutablePointer : class
{
    /// <inheritdoc cref="IStorable.Id" />
    public string Id => Inner.StorableItemId;

    /// <inheritdoc />
    public required string EventStreamHandlerId { get; init; }

    /// <inheritdoc />
    public string Name => Inner.StorableItemName;

    /// <summary>
    /// The parent folder of this file.
    /// </summary>
    public required IFolder? Parent { get; init; }
    
    /// <inheritdoc />
    public required NomadFileData<TImmutablePointer> Inner { get; set; }

    /// <inheritdoc />
    public virtual Task ResetEventStreamPositionAsync(CancellationToken cancellationToken)
    {
        EventStreamPosition = null;
        Inner = new NomadFileData<TImmutablePointer> {  StorableItemName = Name, StorableItemId = Id, ContentId = null, };
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public required TEventStreamSource LocalEventStream { get; set; }

    /// <inheritdoc />
    public TEventStreamEntry? EventStreamPosition { get; set; }

    /// <inheritdoc />
    public required ICollection<TMutablePointer> Sources { get; init; }

    /// <inheritdoc />
    public abstract Task<Stream> OpenStreamAsync(FileAccess accessMode = FileAccess.Read, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task AdvanceEventStreamAsync(TEventStreamEntry streamEntry, CancellationToken cancellationToken);

    /// <inheritdoc />
    public Task<IFolder?> GetParentAsync(CancellationToken cancellationToken = default) => Task.FromResult(Parent);
}