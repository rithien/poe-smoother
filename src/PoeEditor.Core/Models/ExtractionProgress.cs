namespace PoeEditor.Core.Models;

/// <summary>
/// Progress information for extraction operations.
/// </summary>
public record ExtractionProgress
{
    /// <summary>
    /// Current file being processed (1-based).
    /// </summary>
    public int CurrentFile { get; init; }

    /// <summary>
    /// Total number of files to process.
    /// </summary>
    public int TotalFiles { get; init; }

    /// <summary>
    /// Name of the file currently being extracted.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Size of the current file in bytes.
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// Percentage of overall progress (0-100).
    /// </summary>
    public int Percentage => TotalFiles > 0 ? (int)((double)CurrentFile / TotalFiles * 100) : 0;
}
