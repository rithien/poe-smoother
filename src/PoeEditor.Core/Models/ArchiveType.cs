namespace PoeEditor.Core.Models;

/// <summary>
/// Represents the type of archive being processed.
/// </summary>
public enum ArchiveType
{
    /// <summary>
    /// Unknown or undetected archive type.
    /// </summary>
    Unknown,

    /// <summary>
    /// Steam/Epic version using bundle files (_.index.bin).
    /// </summary>
    Bundle,

    /// <summary>
    /// Standalone version using Content.ggpk file.
    /// </summary>
    Ggpk
}
