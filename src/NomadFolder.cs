using OwlCore.ComponentModel;
using OwlCore.Storage;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;
using OwlCore.Nomad.Storage.Models;

namespace OwlCore.Nomad.Storage;

/// <summary>
/// A virtual folder constructed by advancing an <see cref="IEventStreamHandler{TContentPointer, TEventStream, TEventStreamEntry}.EventStreamPosition"/> using multiple <see cref="ISources{T}.Sources"/> in concert with other <see cref="ISharedEventStreamHandler{TContentPointer, TEventStreamSource, TEventStreamEntry, TListeningHandlers}.ListeningEventStreamHandlers"/>.
/// </summary>
public abstract class NomadFolder<TContentPointer, TEventStreamSource, TEventStreamEntry> : ISharedEventStreamHandler<TContentPointer, TEventStreamSource, TEventStreamEntry>, IModifiableFolder, IMutableFolder, IChildFolder, IGetItem, IGetRoot, IGetFirstByName, IDelegable<NomadFolderData<TContentPointer>>
    where TEventStreamSource : EventStream<TContentPointer>
    where TEventStreamEntry : EventStreamEntry<TContentPointer>
    where TContentPointer : class
{
    /// <summary>
    /// Creates a new instance of <see cref="NomadFolder{TContentPointer,TEventStreamSource,TEventStreamEntry}"/>.
    /// </summary>
    /// <param name="listeningEventStreamHandlers">The shared list of known nomad event streams participating in event seeking.</param>
    protected NomadFolder(ICollection<ISharedEventStreamHandler<TContentPointer, TEventStreamSource, TEventStreamEntry>> listeningEventStreamHandlers)
    {
        listeningEventStreamHandlers.Add(this);
        ListeningEventStreamHandlers = listeningEventStreamHandlers;
    }

    /// <inheritdoc />
    public virtual ICollection<ISharedEventStreamHandler<TContentPointer, TEventStreamSource, TEventStreamEntry>> ListeningEventStreamHandlers { get; }

    /// <inheritdoc />
    public required string EventStreamHandlerId { get; init; }

    /// <inheritdoc />
    public required ICollection<TContentPointer> Sources { get; init; }

    /// <inheritdoc />
    public required ICollection<TEventStreamEntry> AllEventStreamEntries { get; set; }

    /// <inheritdoc />
    public required TEventStreamSource LocalEventStream { get; set; }

    /// <inheritdoc />
    public TEventStreamEntry? EventStreamPosition { get; set; }

    /// <inheritdoc cref="IStorable.Id" />
    public string Id => Inner.StorableItemId;

    /// <inheritdoc />
    public string Name => Inner.StorableItemName;

    /// <summary>
    /// The parent for this folder, if any.
    /// </summary>
    public required IFolder? Parent { get; init; }
    
    /// <inheritdoc />
    public required NomadFolderData<TContentPointer> Inner { get; set; }

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

    /// <inheritdoc/>
    public async Task<IStorableChild> GetFirstByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var fileTarget = Inner.Files.FirstOrDefault(x => x.StorableItemName == name);
        if (fileTarget is not null)
            return await FileDataToInstanceAsync(fileTarget, cancellationToken);

        var folderTarget = Inner.Folders.FirstOrDefault(x => x.StorableItemName == name);
        if (folderTarget is not null)
            return await FolderDataToInstanceAsync(folderTarget, cancellationToken);

        throw new FileNotFoundException($"No storage item with the name '{name}' could be found.");
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(IStorableChild item, CancellationToken cancellationToken = default)
    {
        var storageUpdateEvent = new DeleteFromFolderEvent(Id, item.Id, item.Name);
        await ApplyEntryUpdateAsync(storageUpdateEvent, cancellationToken);
        EventStreamPosition = await AppendNewEntryAsync(storageUpdateEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IChildFolder> CreateFolderAsync(string name, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        var storageUpdateEvent = new CreateFolderInFolderEvent(Id, $"{Id}/{name}", name, overwrite);
        var createdFolderData = await ApplyFolderUpdateAsync(storageUpdateEvent, cancellationToken);
        EventStreamPosition = await AppendNewEntryAsync(storageUpdateEvent, cancellationToken);

        Guard.IsNotNull(createdFolderData);
        return await FolderDataToInstanceAsync(createdFolderData, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IChildFile> CreateFileAsync(string name, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        var storageUpdateEvent = new CreateFileInFolderEvent(Id, $"{Id}/{name}", name, overwrite);
        var createdFileData = await ApplyFolderUpdateAsync(storageUpdateEvent, cancellationToken);
        EventStreamPosition = await AppendNewEntryAsync(storageUpdateEvent, cancellationToken);

        Guard.IsNotNull(createdFileData);
        return await FileDataToInstanceAsync(createdFileData, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IFolder?> GetRootAsync(CancellationToken cancellationToken = default)
    {
        // No parent = no root
        if (Parent is null)
            return Task.FromResult<IFolder?>(null);
        
        // At least one parent is required for a root to exist
        // Crawl up and return where parent is null
        var current = this;
        while (current.Parent is NomadFolder<TContentPointer, TEventStreamSource, TEventStreamEntry> parent)
        {
            current = parent;
        }

        return Task.FromResult<IFolder?>(current);
    }

    /// <inheritdoc />
    public Task<IFolder?> GetParentAsync(CancellationToken cancellationToken = default) => Task.FromResult<IFolder?>(Parent);

    /// <inheritdoc />
    public abstract Task<IFolderWatcher> GetFolderWatcherAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task AdvanceEventStreamAsync(TEventStreamEntry streamEntry, CancellationToken cancellationToken);

    /// <inheritdoc />
    public virtual Task ResetEventStreamPositionAsync(CancellationToken cancellationToken)
    {
        EventStreamPosition = null;
        Inner = new NomadFolderData<TContentPointer> {  StorableItemName = Name, StorableItemId = Id, Sources = Sources };
            
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Appends a new entry to the event stream.
    /// </summary>
    /// <param name="updateEvent">The event to append.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    /// <returns>The event stream entry that was created and appended to the event stream.</returns>
    public abstract Task<TEventStreamEntry> AppendNewEntryAsync(FolderUpdateEvent updateEvent, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Applies an event stream update to this object without side effects.
    /// </summary>
    /// <param name="updateEvent">The update to apply without side effects.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public abstract Task ApplyEntryUpdateAsync(FolderUpdateEvent updateEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Transform file data to file instance.
    /// </summary>
    /// <param name="fileData">The file data to transform.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    protected abstract Task<NomadFile<TContentPointer, TEventStreamSource, TEventStreamEntry>> FileDataToInstanceAsync(NomadFileData<TContentPointer> fileData, CancellationToken cancellationToken);   
    
    /// <summary>
    /// Transforms folder data to a folder instance.
    /// </summary>
    /// <param name="folderData">The folder data to transform.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    protected abstract Task<NomadFolder<TContentPointer, TEventStreamSource, TEventStreamEntry>> FolderDataToInstanceAsync(NomadFolderData<TContentPointer> folderData, CancellationToken cancellationToken); 

    /// <summary>
    /// Applies the provided <paramref name="updateEvent"/>.
    /// </summary>
    /// <param name="updateEvent">The event content to apply without side effects.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing task.</param>
    public Task<NomadFileData<TContentPointer>?> ApplyFolderUpdateAsync(CreateFileInFolderEvent updateEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var nomadFolder = this;
        
        var existing = nomadFolder.Inner.Files.FirstOrDefault(x =>
            x.StorableItemId == updateEvent.StorableItemId ||
            x.StorableItemName == updateEvent.StorableItemName);

        if (updateEvent.Overwrite && existing is not null)
        {
            nomadFolder.Inner.Files.Remove(existing);
            existing = null;
        }

        var nomadFileData = existing ?? new NomadFileData<TContentPointer>
        {
            ContentId = null,
            StorableItemId = updateEvent.StorableItemId,
            StorableItemName = updateEvent.StorableItemName,
        };
        
        if (nomadFolder.Inner.Files.All(x => x.StorableItemId != nomadFileData.StorableItemId))
            nomadFolder.Inner.Files.Add(nomadFileData);
        
        return Task.FromResult<NomadFileData<TContentPointer>?>(nomadFileData);
    }

    /// <summary>
    /// Applies the provided <paramref name="updateEvent"/>.
    /// </summary>
    /// <param name="updateEvent">The event content to apply without side effects.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing task.</param>
    public Task<NomadFolderData<TContentPointer>?> ApplyFolderUpdateAsync(CreateFolderInFolderEvent updateEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var nomadFolder = this;

        // Apply folder updates
        var existing = nomadFolder.Inner.Folders.FirstOrDefault(x =>
            x.StorableItemId == updateEvent.StorableItemId ||
            x.StorableItemName == updateEvent.StorableItemName);

        if (updateEvent.Overwrite)
        {
            nomadFolder.Inner.Folders.Remove(existing);
            existing = null;
        }

        var nomadFolderData = existing ?? new NomadFolderData<TContentPointer>
        {
            StorableItemName = updateEvent.StorableItemName,
            StorableItemId = updateEvent.StorableItemId,
            Sources = nomadFolder.Sources,
            Files = [],
            Folders = [],
        };
        
        if (nomadFolder.Inner.Folders.All(x => x.StorableItemId != nomadFolderData.StorableItemId))
            nomadFolder.Inner.Folders.Add(nomadFolderData);
        
        return Task.FromResult<NomadFolderData<TContentPointer>?>(nomadFolderData);
    }
}