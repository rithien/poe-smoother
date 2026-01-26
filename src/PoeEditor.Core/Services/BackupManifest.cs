namespace PoeEditor.Core.Services;

/// <summary>
/// Result of a dirty state check.
/// A file is "dirty" if it contains patcher markers but no backup exists.
/// </summary>
public class DirtyStateResult
{
    /// <summary>
    /// Whether the file appears to be modified without a backup.
    /// </summary>
    public bool IsDirty { get; set; }

    /// <summary>
    /// List of markers found in the file.
    /// </summary>
    public List<string> DetectedMarkers { get; set; } = new();

    /// <summary>
    /// The virtual path of the file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
}
