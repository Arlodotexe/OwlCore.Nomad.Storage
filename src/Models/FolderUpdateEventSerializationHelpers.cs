using System;
using System.Linq;
using CommunityToolkit.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OwlCore.Extensions;

namespace OwlCore.Nomad.Storage.Models;

internal static class FolderUpdateEventSerializationHelpers
{
    internal static JObject? Write(StorageUpdateEvent @event)
    {
        var jObject = new JObject();

        jObject.AddFirst(new JProperty("eventId", new JValue(@event.EventId)));

        jObject.AddFirst(new JProperty("storableItemId", new JValue(@event.StorableItemId)));

        if (@event is CreateFileInFolderEvent createFileInFolderEvent)
        {
            jObject.AddFirst(new JProperty("workingFolderId", new JValue(createFileInFolderEvent.WorkingFolderId)));
            jObject.AddFirst(new JProperty("storableItemName", new JValue(createFileInFolderEvent.StorableItemName)));
            jObject.AddFirst(new JProperty("overwrite", new JValue(createFileInFolderEvent.Overwrite)));
        }

        if (@event is CreateFolderInFolderEvent createFolderInFolderEvent)
        {
            jObject.AddFirst(new JProperty("workingFolderId", new JValue(createFolderInFolderEvent.WorkingFolderId)));
            jObject.AddFirst(new JProperty("storableItemName", new JValue(createFolderInFolderEvent.StorableItemName)));
            jObject.AddFirst(new JProperty("overwrite", new JValue(createFolderInFolderEvent.Overwrite)));
        }

        if (@event is DeleteFromFolderEvent deleteFromFolderEvent)
        {
            jObject.AddFirst(new JProperty("workingFolderId", new JValue(deleteFromFolderEvent.WorkingFolderId)));
            jObject.AddFirst(new JProperty("storableItemName", new JValue(deleteFromFolderEvent.StorableItemName)));
        }

        return jObject;
    }

    internal static object? Read(JToken token, JsonSerializer serializer)
    {
        if (token.Type == JTokenType.Array)
        {
            var jsonArray = (JArray)token;
            return jsonArray.Select(jToken => Read((JToken)jToken, serializer)).PruneNull().FirstOrDefault();
        }

        if (token is JObject jObject)
            return Read(jObject, serializer);

        throw new NotSupportedException($"Token type {token.Type} is not supported.");
    }

    internal static StorageUpdateEvent? Read(JObject jObject, JsonSerializer serializer)
    {
        var eventId = jObject["eventId"]?.Value<string>();
        var workingFolderId = jObject["workingFolderId"]?.Value<string>();
        var storableItemId = jObject["storableItemId"]?.Value<string>();
        var storableItemName = jObject["storableItemName"]?.Value<string>();
        
        Guard.IsNotNullOrWhiteSpace(eventId);
        Guard.IsNotNullOrWhiteSpace(workingFolderId);
        Guard.IsNotNullOrWhiteSpace(storableItemId);

        if (eventId == "create_file_in_folder")
        {
            Guard.IsNotNull(storableItemName);

            var overwrite = jObject["overwrite"]?.Value<bool>();
            Guard.IsNotNull(overwrite);

            return new CreateFileInFolderEvent(workingFolderId, storableItemId, storableItemName, overwrite.Value);
        }

        if (eventId == "create_folder_in_folder")
        {
            Guard.IsNotNull(storableItemName);
            
            var overwrite = jObject["overwrite"]?.Value<bool>();
            Guard.IsNotNull(overwrite);

            return new CreateFolderInFolderEvent(workingFolderId, storableItemId, storableItemName, overwrite.Value);
        }

        if (eventId == "deleted_from_folder")
        {
            Guard.IsNotNull(storableItemName);
            return new DeleteFromFolderEvent(workingFolderId, storableItemId, storableItemName);
        }

        return null;
    }
}