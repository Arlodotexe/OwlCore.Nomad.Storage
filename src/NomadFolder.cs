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
using System;

namespace OwlCore.Nomad.Storage;

/// <summary>
/// A virtual folder constructed by advancing an <see cref="IEventStreamHandler{TImmutablePointer, TMutablePointer, TEventStream, TEventStreamEntry}.EventStreamPosition"/> using multiple <see cref="ISources{T}.Sources"/>
/// </summary>
public abstract class NomadFolder<TImmutablePointer, TMutablePointer, TEventStream, TEventStreamEntry> : IEventStreamHandler<TImmutablePointer, TMutablePointer, TEventStream, TEventStreamEntry>, IModifiableFolder, IMutableFolder, IChildFolder, IGetItem, IGetRoot, IGetFirstByName, IDelegable<NomadFolderData<TImmutablePointer, TMutablePointer>>
    where TEventStream : EventStream<TImmutablePointer>
    where TEventStreamEntry : EventStreamEntry<TImmutablePointer>
    where TImmutablePointer : class
{
    /// <inheritdoc />
    public required string EventStreamHandlerId { get; init; }

    /// <inheritdoc />
    public required ICollection<TMutablePointer> Sources { get; init; }

    /// <inheritdoc />
    public required TEventStream LocalEventStream { get; set; }

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
    public required NomadFolderData<TImmutablePointer, TMutablePointer> Inner { get; set; }

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
    public virtual async Task DeleteAsync(IStorableChild item, CancellationToken cancellationToken = default)
    {
        var storageUpdateEvent = new DeleteFromFolderEvent(Id, item.Id, item.Name);
        EventStreamPosition = await AppendNewEntryAsync(targetId: item.Id, eventId: nameof(DeleteFromFolderEvent), storageUpdateEvent, DateTime.UtcNow, cancellationToken);
        await ApplyEntryUpdateAsync(EventStreamPosition, storageUpdateEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public virtual async Task<IChildFolder> CreateFolderAsync(string name, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        var storageUpdateEvent = new CreateFolderInFolderEvent(Id, $"{Id}/{name}", name, overwrite);
        EventStreamPosition = await AppendNewEntryAsync(targetId: Id, eventId: nameof(CreateFolderInFolderEvent), storageUpdateEvent, DateTime.UtcNow, cancellationToken);
        var createdFolderData = await ApplyFolderUpdateAsync(storageUpdateEvent, cancellationToken);

        Guard.IsNotNull(createdFolderData);
        return await FolderDataToInstanceAsync(createdFolderData, cancellationToken);
    }

    /// <inheritdoc/>
    public virtual async Task<IChildFile> CreateFileAsync(string name, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        var storageUpdateEvent = new CreateFileInFolderEvent(Id, $"{Id}/{name}", name, overwrite);
        EventStreamPosition = await AppendNewEntryAsync(targetId: Id, eventId: nameof(CreateFileInFolderEvent), storageUpdateEvent, DateTime.UtcNow, cancellationToken);
        var createdFileData = await ApplyFolderUpdateAsync(storageUpdateEvent, cancellationToken);

        Guard.IsNotNull(createdFileData);
        return await FileDataToInstanceAsync(createdFileData, cancellationToken);
    }

    /// <inheritdoc />
    public virtual Task<IFolder?> GetRootAsync(CancellationToken cancellationToken = default)
    {
        // No parent = no root
        if (Parent is null)
            return Task.FromResult<IFolder?>(null);
        
        // At least one parent is required for a root to exist
        // Crawl up and return where parent is null
        var current = this;
        while (current.Parent is NomadFolder<TImmutablePointer, TMutablePointer, TEventStream, TEventStreamEntry> parent)
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
        Inner = new NomadFolderData<TImmutablePointer, TMutablePointer> {  StorableItemName = Name, StorableItemId = Id, Sources = Sources };
            
        return Task.CompletedTask;
    }

    /// <summary>
    /// Appends a new entry to the event stream.
    /// </summary>
    /// <param name="targetId">The object being targeted with this event.</param>
    /// <param name="eventId">The event that occurred within some domain.</param>
    /// <param name="updateEvent">The event to append.</param>
    /// <param name="timestampUtc">The time in UTC that the event occurred.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    /// <returns>The event stream entry that was created and appended to the event stream.</returns>
    public abstract Task<TEventStreamEntry> AppendNewEntryAsync(string targetId, string eventId, FolderUpdateEvent updateEvent, DateTime? timestampUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies an event stream update to this object without side effects.
    /// </summary>
    /// <param name="eventStreamEntry">The event stream entry to apply.</param>
    /// <param name="updateEvent">The update to apply without side effects.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public abstract Task ApplyEntryUpdateAsync(EventStreamEntry<TImmutablePointer> eventStreamEntry, FolderUpdateEvent updateEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Transform file data to file instance.
    /// </summary>
    /// <param name="fileData">The file data to transform.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    protected abstract Task<NomadFile<TImmutablePointer, TMutablePointer, TEventStream, TEventStreamEntry>> FileDataToInstanceAsync(NomadFileData<TImmutablePointer> fileData, CancellationToken cancellationToken);   
    
    /// <summary>
    /// Transforms folder data to a folder instance.
    /// </summary>
    /// <param name="folderData">The folder data to transform.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    protected abstract Task<NomadFolder<TImmutablePointer, TMutablePointer, TEventStream, TEventStreamEntry>> FolderDataToInstanceAsync(NomadFolderData<TImmutablePointer, TMutablePointer> folderData, CancellationToken cancellationToken); 

    /// <summary>
    /// Applies the provided <paramref name="updateEvent"/>.
    /// </summary>
    /// <param name="updateEvent">The event content to apply without side effects.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing task.</param>
    public virtual Task<NomadFileData<TImmutablePointer>?> ApplyFolderUpdateAsync(CreateFileInFolderEvent updateEvent, CancellationToken cancellationToken)
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

        var nomadFileData = existing ?? new NomadFileData<TImmutablePointer>
        {
            ContentId = null,
            StorableItemId = updateEvent.StorableItemId,
            StorableItemName = updateEvent.StorableItemName,
        };
        
        if (nomadFolder.Inner.Files.All(x => x.StorableItemId != nomadFileData.StorableItemId))
            nomadFolder.Inner.Files.Add(nomadFileData);
        
        return Task.FromResult<NomadFileData<TImmutablePointer>?>(nomadFileData);
    }

    /// <summary>
    /// Applies the provided <paramref name="updateEvent"/>.
    /// </summary>
    /// <param name="updateEvent">The event content to apply without side effects.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing task.</param>
    public virtual Task<NomadFolderData<TImmutablePointer, TMutablePointer>?> ApplyFolderUpdateAsync(CreateFolderInFolderEvent updateEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var nomadFolder = this;

        // Apply folder updates
        var existing = nomadFolder.Inner.Folders.FirstOrDefault(x =>
            x.StorableItemId == updateEvent.StorableItemId ||
            x.StorableItemName == updateEvent.StorableItemName);

        if (updateEvent.Overwrite && existing is not null)
        {
            nomadFolder.Inner.Folders.Remove(existing);
            existing = null;
        }

        var nomadFolderData = existing ?? new NomadFolderData<TImmutablePointer, TMutablePointer>
        {
            StorableItemName = updateEvent.StorableItemName,
            StorableItemId = updateEvent.StorableItemId,
            Sources = nomadFolder.Sources,
            Files = [],
            Folders = [],
        };
        
        if (nomadFolder.Inner.Folders.All(x => x.StorableItemId != nomadFolderData.StorableItemId))
            nomadFolder.Inner.Folders.Add(nomadFolderData);
        
        return Task.FromResult<NomadFolderData<TImmutablePointer, TMutablePointer>?>(nomadFolderData);
    }

    /// <summary>
    /// Applies the provided <paramref name="updateEvent"/> in the folder.
    /// </summary>
    /// <param name="updateEvent">The event content to apply without side effects.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing task.</param>
    public virtual Task ApplyFolderUpdateAsync(DeleteFromFolderEvent updateEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        // If deleted, it should already exist in the folder.
        // Remove the item if it exists.
        // If it doesn't exist, it may have been removed in another timeline (by another peer).
        // Folders
        var targetFolder = Inner.Folders.FirstOrDefault(x => x.StorableItemId == updateEvent.StorableItemId || x.StorableItemName == updateEvent.StorableItemName);
        if (targetFolder is not null)
            Inner.Folders.Remove(targetFolder);
        
        // Files
        var targetFile = Inner.Files.FirstOrDefault(x=> x.StorableItemId == updateEvent.StorableItemId || updateEvent.StorableItemName == x.StorableItemName);
        if (targetFile is not null)
            Inner.Files.Remove(targetFile);

        return Task.CompletedTask;
    }
}