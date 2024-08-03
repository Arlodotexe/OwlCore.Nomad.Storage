using Newtonsoft.Json;
using OwlCore.Storage;

namespace OwlCore.Nomad.Storage.Models;

/// <summary>
/// Represents an update event for a folder.
/// </summary>
/// <param name="StorableItemId">The id of the <see cref="IStorable"/> that was updated.</param>
/// <param name="EventId">A unique identifier for this event.</param>
[JsonConverter(typeof(NomadEventJsonConverter))]
public abstract record StorageUpdateEvent(string StorableItemId, string EventId);

/// <summary>
/// Represents an update event for a file.
/// </summary>
/// <param name="StorableItemId">The id of the file that was changed.</param>
/// <param name="NewContentId">A Cid that represents immutable content. The same ID should always point to the same content, and different content should point to different Ids.</param>
public record FileUpdateEvent<TContentPointer>(string StorableItemId, TContentPointer NewContentId) : StorageUpdateEvent(StorableItemId, "file_update");

/// <summary>
/// Represents an update event for a folder.
/// </summary>
/// <param name="WorkingFolderId">The ID of the folder being updated.</param>
/// <param name="StorableItemId">The storable item being updated in the folder.</param>
/// <param name="EventId">A unique identifier for this event.</param>
public abstract record FolderUpdateEvent(string WorkingFolderId, string StorableItemId, string EventId) : StorageUpdateEvent(StorableItemId, EventId);

/// <summary>
/// An event that represents the creation of a new file within a specific folder.
/// </summary>
public record CreateFileInFolderEvent(string WorkingFolderId, string StorableItemId, string StorableItemName, bool Overwrite) : FolderUpdateEvent(WorkingFolderId, StorableItemId, "create_file_in_folder");

/// <summary>
/// An event that represents the creation of a new folder within a specific folder.
/// </summary>
public record CreateFolderInFolderEvent(string WorkingFolderId, string StorableItemId, string StorableItemName, bool Overwrite) : FolderUpdateEvent(WorkingFolderId, StorableItemId, "create_folder_in_folder");

/// <summary>
/// An event that represents the deletion of an item from a specific folder.
/// </summary>
public record DeleteFromFolderEvent(string WorkingFolderId, string StorableItemId, string StorableItemName) : FolderUpdateEvent(WorkingFolderId, StorableItemId, "deleted_from_folder");