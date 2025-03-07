using OwlCore.Storage;

namespace OwlCore.Nomad.Storage.Models;

/// <summary>
/// A common base record for <see cref="NomadFolderData{TImmutablePointer, TMutablePointer}"/> and <see cref="NomadFileData{TContentPointer}"/>.
/// </summary>
public abstract record NomadStorableData
{
    /// <summary>
    /// The <see cref="IStorable.Id"/> of the file or folder this record refers to.
    /// </summary>
    public required string StorableItemId { get; set; }
    
    /// <summary>
    /// The <see cref="IStorable.Name"/> of the file or folder this record refers to.
    /// </summary>
    public required string StorableItemName { get; set; }
}