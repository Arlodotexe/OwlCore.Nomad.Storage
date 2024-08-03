using System;

namespace OwlCore.Nomad.Storage.Models;

/// <summary>
/// Represents the state of a file stored in Nomad.
/// </summary>
/// <typeparam name="TContentPointer">The content pointer to use for <see cref="NomadFileData{TContentPointer}.ContentId"/>.</typeparam>
public record NomadFileData<TContentPointer> : NomadStorableData
    where TContentPointer : class
{
    /// <summary>
    /// A content pointer to the file content.
    /// </summary>
    public required TContentPointer? ContentId { get; set; }
}