namespace PoeEditor.Core.Models;

/// <summary>
/// Represents a file or directory entry in the virtual file system.
/// </summary>
public record VirtualFileEntry
{
    /// <summary>
    /// Name of the file or directory.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Full virtual path within the archive.
    /// </summary>
    public required string FullPath { get; init; }

    /// <summary>
    /// True if this entry is a directory, false if it's a file.
    /// </summary>
    public bool IsDirectory { get; init; }

    /// <summary>
    /// Size of the file in bytes (0 for directories).
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Optional hash of the file content.
    /// </summary>
    public string? Hash { get; init; }

    /// <summary>
    /// Child entries (for directories).
    /// </summary>
    public List<VirtualFileEntry> Children { get; init; } = [];
}
