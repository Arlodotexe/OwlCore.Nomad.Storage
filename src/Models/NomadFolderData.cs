using System.Collections.Generic;
using OwlCore.ComponentModel;

namespace OwlCore.Nomad.Storage.Models;

/// <summary>
/// Represents the state of a folder stored in Nomad.
/// </summary>
public record NomadFolderData<TImmutablePointer, TMutablePointer> : NomadStorableData, ISources<TMutablePointer>
    where TImmutablePointer : class
{
    /// <summary>
    /// Data that represents the files in this folder
    /// </summary>
    public List<NomadFileData<TImmutablePointer>> Files { get; set; } = new();
    
    /// <summary>
    /// Data that represents the folders in this folder.
    /// </summary>
    public List<NomadFolderData<TImmutablePointer, TMutablePointer>> Folders { get; set; } = new();

    /// <inheritdoc />
    // ? do this here or upstream?
    // are we publishing this model or transforming it to mfs and a single cid for roaming?
    // roaming needs sources, but folders aren't json 
    public ICollection<TMutablePointer> Sources { get; init; } = new List<TMutablePointer>();
}