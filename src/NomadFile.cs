using OwlCore.ComponentModel;
using OwlCore.ComponentModel.Nomad;
using OwlCore.Nomad.Storage.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OwlCore.Kubo.Nomad.Storage;

/// <summary>
/// A virtual file constructed by advancing an <see cref="IEventStreamHandler{TEventStreamEntry}.EventStreamPosition"/> using multiple <see cref="ISources{T}.Sources"/> in concert with other <see cref="ISharedEventStreamHandler{TContentPointer, TEventStreamSource, TEventStreamEntry, TListeningHandlers}.ListeningEventStreamHandlers"/>.
/// </summary>
public abstract class NomadFile<TContentPointer, TEventStreamSource, TEventStreamEntry> : ReadOnlyNomadFile<TContentPointer, TEventStreamSource, TEventStreamEntry>, IModifiableSharedEventStreamHandler<StorageUpdateEvent, TContentPointer, TEventStreamSource, TEventStreamEntry>
    where TEventStreamSource : EventStream<TContentPointer>
    where TEventStreamEntry : EventStreamEntry<TContentPointer>
{
    /// <summary>
    /// Creates a new instance of <see cref="NomadFile{TContentPointer, TEventStreamSource, TEventStreamEntry}"/>.
    /// </summary>
    /// <param name="listeningEventStreamHandlers">The shared collection of known event stream targets participating in event seeking.</param>
    protected NomadFile(ICollection<ISharedEventStreamHandler<TContentPointer, TEventStreamSource, TEventStreamEntry>> listeningEventStreamHandlers)
        : base(listeningEventStreamHandlers)
    {
    }

    /// <summary>
    /// The name of the local ipns key to publish event stream changes to.
    /// </summary>
    public required string LocalEventStreamKeyName { get; init; }

    /// <inheritdoc/>
    public abstract Task AppendNewEntryAsync(StorageUpdateEvent updateEvent, CancellationToken cancellationToken = default);
}