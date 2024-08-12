using OwlCore.ComponentModel;
using OwlCore.Nomad.Storage.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OwlCore.Nomad.Storage;

/// <summary>
/// A virtual file constructed by advancing an <see cref="IEventStreamHandler{TEventStreamEntry}.EventStreamPosition"/> using multiple <see cref="ISources{T}.Sources"/> in concert with other <see cref="ISharedEventStreamHandler{TContentPointer, TEventStreamSource, TEventStreamEntry, TListeningHandlers}.ListeningEventStreamHandlers"/>.
/// </summary>
public abstract class NomadFile<TContentPointer, TEventStreamSource, TEventStreamEntry> : ReadOnlyNomadFile<TContentPointer, TEventStreamSource, TEventStreamEntry>
    where TEventStreamSource : EventStream<TContentPointer>
    where TEventStreamEntry : EventStreamEntry<TContentPointer>
    where TContentPointer : class
{
    /// <summary>
    /// Creates a new instance of <see cref="NomadFile{TContentPointer, TEventStreamSource, TEventStreamEntry}"/>.
    /// </summary>
    /// <param name="listeningEventStreamHandlers">The shared collection of known event stream targets participating in event seeking.</param>
    protected NomadFile(ICollection<ISharedEventStreamHandler<TContentPointer, TEventStreamSource, TEventStreamEntry>> listeningEventStreamHandlers)
        : base(listeningEventStreamHandlers)
    {
    }
}