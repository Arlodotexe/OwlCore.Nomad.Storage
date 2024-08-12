using OwlCore.ComponentModel;
using OwlCore.Nomad.Storage.Models;
using OwlCore.Storage;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OwlCore.Nomad.Storage;

/// <summary>
/// A virtual file constructed by advancing an <see cref="IEventStreamHandler{TEventStreamEntry}.EventStreamPosition"/> using multiple <see cref="ISources{T}.Sources"/> in concert with other <see cref="ISharedEventStreamHandler{TContentPointer, TEventStreamSource, TEventStreamEntry, TListeningHandlers}.ListeningEventStreamHandlers"/>.
/// </summary>
public abstract class NomadFolder<TContentPointer, TEventStreamSource, TEventStreamEntry> : ReadOnlyNomadFolder<TContentPointer, TEventStreamSource, TEventStreamEntry>, IModifiableFolder, IModifiableSharedEventStreamHandler<FolderUpdateEvent, TContentPointer, TEventStreamSource, TEventStreamEntry>
    where TEventStreamSource : EventStream<TContentPointer>
    where TEventStreamEntry : EventStreamEntry<TContentPointer>
    where TContentPointer : class
{
    /// <summary>
    /// Creates a new instance of <see cref="NomadFolder{TContentPointer, TEventStreamSource, TEventStreamEntry}"/>.
    /// </summary>
    /// <param name="listeningEventStreamHandlers">The shared collection of known nomad event streams participating in event seeking.</param>
    protected NomadFolder(ICollection<ISharedEventStreamHandler<TContentPointer, TEventStreamSource, TEventStreamEntry>> listeningEventStreamHandlers) : base(listeningEventStreamHandlers)
    {
    }

    /// <inheritdoc/>
    public abstract Task<IChildFile> CreateFileAsync(string name, bool overwrite = false, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public abstract Task<IChildFolder> CreateFolderAsync(string name, bool overwrite = false, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public abstract Task DeleteAsync(IStorableChild item, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public abstract Task<TEventStreamEntry> AppendNewEntryAsync(FolderUpdateEvent updateEvent, CancellationToken cancellationToken = default);
}