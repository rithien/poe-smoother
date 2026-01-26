namespace PoeEditor.Core.Services;

/// <summary>
/// Simplified backup service with First Touch strategy.
/// Backups are stored preserving original folder structure.
/// No manifest - folder structure IS the manifest.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Path to the backup directory.
    /// </summary>
    string BackupDirectory { get; }

    /// <summary>
    /// Initialize the backup service.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Check if a file has been backed up (First Touch check).
    /// </summary>
    /// <param name="virtualPath">Virtual path of the file in the archive.</param>
    /// <returns>True if backup exists, false otherwise.</returns>
    bool HasBackup(string virtualPath);

    /// <summary>
    /// Create a backup of a file (First Touch strategy - only if not already backed up).
    /// IMPORTANT: If backup already exists, it will NOT be overwritten.
    /// Multiple patchers may modify the same file - backup contains ORIGINAL file.
    /// </summary>
    /// <param name="virtualPath">Virtual path of the file in the archive.</param>
    /// <param name="data">Original file content.</param>
    /// <returns>True if backup was created, false if it already existed.</returns>
    Task<bool> BackupFileAsync(string virtualPath, byte[] data);

    /// <summary>
    /// Get the original backed up content of a file.
    /// </summary>
    /// <param name="virtualPath">Virtual path of the file in the archive.</param>
    /// <returns>Original file content, or null if no backup exists.</returns>
    Task<byte[]?> GetBackupAsync(string virtualPath);

    /// <summary>
    /// Remove a single backup file.
    /// </summary>
    /// <param name="virtualPath">Virtual path of the file in the archive.</param>
    Task RemoveBackupAsync(string virtualPath);

    /// <summary>
    /// Check if file content contains any known patcher markers (dirty state detection).
    /// </summary>
    /// <param name="virtualPath">Virtual path for reporting.</param>
    /// <param name="content">File content to check.</param>
    /// <returns>Dirty state result with detected markers.</returns>
    DirtyStateResult CheckForMarkers(string virtualPath, string content);

    /// <summary>
    /// Check if a file is in dirty state (has markers but no backup).
    /// </summary>
    /// <param name="virtualPath">Virtual path of the file.</param>
    /// <param name="content">File content to check.</param>
    /// <returns>Dirty state result.</returns>
    DirtyStateResult CheckDirtyState(string virtualPath, string content);

    /// <summary>
    /// Register a marker pattern for a patcher.
    /// </summary>
    /// <param name="patcherName">Name of the patcher.</param>
    /// <param name="markerPattern">The marker comment pattern (e.g., "{{RITHIEN_bloom}}").</param>
    void RegisterMarker(string patcherName, string markerPattern);

    /// <summary>
    /// Get all registered markers.
    /// </summary>
    IReadOnlyDictionary<string, string> GetRegisteredMarkers();

    /// <summary>
    /// Get list of all backed up file paths (virtual paths).
    /// </summary>
    IEnumerable<string> GetAllBackupPaths();

    /// <summary>
    /// Check if any backups exist.
    /// </summary>
    bool HasAnyBackups();

    /// <summary>
    /// Clear all backups.
    /// </summary>
    Task ClearAllBackupsAsync();
}
