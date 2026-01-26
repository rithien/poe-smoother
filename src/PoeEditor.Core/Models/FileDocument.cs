namespace PoeEditor.Core.Models;

/// <summary>
/// Represents a file document to be indexed in Elasticsearch.
/// </summary>
public class FileDocument
{
    /// <summary>
    /// Unique identifier (hash of file path).
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Full virtual path within the archive.
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// File name without path.
    /// </summary>
    public string FileName { get; set; } = "";

    /// <summary>
    /// File extension (e.g., ".hlsl", ".txt").
    /// </summary>
    public string Extension { get; set; } = "";

    /// <summary>
    /// Full text content of the file.
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Source archive path.
    /// </summary>
    public string SourceArchive { get; set; } = "";

    /// <summary>
    /// Timestamp when the file was indexed.
    /// </summary>
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
}
