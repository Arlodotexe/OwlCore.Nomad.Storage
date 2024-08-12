using Newtonsoft.Json;

namespace OwlCore.Nomad.Storage.Models;

/// <summary>
/// Represents an update event for a folder.
/// </summary>
/// <param name="WorkingFolderId">The ID of the folder being updated.</param>
/// <param name="StorableItemId">The storable item being updated in the folder.</param>
/// <param name="EventId">A unique identifier for this event.</param>
[JsonConverter(typeof(NomadFolderEventJsonConverter))]
public abstract record FolderUpdateEvent(string WorkingFolderId, string StorableItemId, string EventId);

/// <summary>
/// An event that represents the creation of a new file within a specific folder.
/// </summary>
public record CreateFileInFolderEvent(string WorkingFolderId, string StorableItemId, string StorableItemName, bool Overwrite) : FolderUpdateEvent(WorkingFolderId, StorableItemId, nameof(CreateFileInFolderEvent));

/// <summary>
/// An event that represents the creation of a new folder within a specific folder.
/// </summary>
public record CreateFolderInFolderEvent(string WorkingFolderId, string StorableItemId, string StorableItemName, bool Overwrite) : FolderUpdateEvent(WorkingFolderId, StorableItemId, nameof(CreateFolderInFolderEvent));

/// <summary>
/// An event that represents the deletion of an item from a specific folder.
/// </summary>
public record DeleteFromFolderEvent(string WorkingFolderId, string StorableItemId, string StorableItemName) : FolderUpdateEvent(WorkingFolderId, StorableItemId, nameof(DeleteFromFolderEvent));