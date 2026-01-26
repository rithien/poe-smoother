namespace PoeEditor.Core.Services;

/// <summary>
/// Simplified backup service with First Touch strategy.
///
/// Key features:
/// - No hash in folder/file names - uses original folder structure
/// - No manifest.json - folder structure IS the manifest
/// - First Touch: backup created only on first modification, never overwritten
/// - Multiple patchers can modify same file - backup contains ORIGINAL file
/// - Restore All: restores all backed up files at once
/// </summary>
public class BackupService : IBackupService
{
    private readonly string _backupDirectory;
    private readonly Dictionary<string, string> _registeredMarkers = new();
    private readonly object _lock = new();
    private ILogService? _logService;

    public BackupService()
    {
        _backupDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PoeEditorPatcher",
            "backups"
        );
    }

    public BackupService(ILogService? logService) : this()
    {
        _logService = logService;
    }

    public void SetLogService(ILogService? logService)
    {
        _logService = logService;
    }

    public string BackupDirectory => _backupDirectory;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_backupDirectory);
        return Task.CompletedTask;
    }

    public bool HasBackup(string virtualPath)
    {
        var backupPath = GetBackupPath(virtualPath);
        return File.Exists(backupPath);
    }

    /// <summary>
    /// Create a backup of a file (First Touch strategy).
    /// CRITICAL: If backup already exists, it will NOT be overwritten!
    /// Another patcher may have already backed up this file.
    /// Backup must contain ORIGINAL file, not a version after first patch.
    /// Thread-safe: Uses lock to prevent race conditions during parallel patching.
    /// </summary>
    public Task<bool> BackupFileAsync(string virtualPath, byte[] data)
    {
        var backupPath = GetBackupPath(virtualPath);

        // Use lock to ensure atomic check-and-create
        lock (_lock)
        {
            // CRITICAL: Don't overwrite existing backup!
            // Another patcher may have already backed up this file.
            if (File.Exists(backupPath))
            {
                _logService?.LogDebug($"[Backup] Already exists: {virtualPath}");
                return Task.FromResult(false); // Backup already exists - keep original version
            }

            var directory = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write synchronously within lock to ensure atomicity
            File.WriteAllBytes(backupPath, data);
            _logService?.LogInfo($"[Backup] Created: {virtualPath} ({data.Length} bytes)");
            return Task.FromResult(true);
        }
    }

    public async Task<byte[]?> GetBackupAsync(string virtualPath)
    {
        var backupPath = GetBackupPath(virtualPath);
        if (!File.Exists(backupPath))
            return null;

        return await File.ReadAllBytesAsync(backupPath);
    }

    public Task RemoveBackupAsync(string virtualPath)
    {
        var backupPath = GetBackupPath(virtualPath);
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
            _logService?.LogDebug($"[Backup] Removed: {virtualPath}");

            // Clean up empty directories
            CleanupEmptyDirectories(Path.GetDirectoryName(backupPath));
        }
        return Task.CompletedTask;
    }

    public DirtyStateResult CheckForMarkers(string virtualPath, string content)
    {
        var result = new DirtyStateResult
        {
            FilePath = virtualPath,
            IsDirty = false,
            DetectedMarkers = new List<string>()
        };

        lock (_lock)
        {
            foreach (var (patcherName, marker) in _registeredMarkers)
            {
                if (content.Contains(marker, StringComparison.Ordinal))
                {
                    result.DetectedMarkers.Add($"{patcherName}: {marker}");
                }
            }
        }

        return result;
    }

    public DirtyStateResult CheckDirtyState(string virtualPath, string content)
    {
        var result = CheckForMarkers(virtualPath, content);

        // File is dirty if it has markers but we don't have a backup
        result.IsDirty = result.DetectedMarkers.Count > 0 && !HasBackup(virtualPath);

        if (result.IsDirty)
        {
            _logService?.LogWarning($"[Backup] Dirty state: {virtualPath}, markers: {string.Join(", ", result.DetectedMarkers)}");
        }

        return result;
    }

    public void RegisterMarker(string patcherName, string markerPattern)
    {
        lock (_lock)
        {
            _registeredMarkers[patcherName] = markerPattern;
        }
    }

    public IReadOnlyDictionary<string, string> GetRegisteredMarkers()
    {
        lock (_lock)
        {
            return new Dictionary<string, string>(_registeredMarkers);
        }
    }

    public IEnumerable<string> GetAllBackupPaths()
    {
        if (!Directory.Exists(_backupDirectory))
            return Enumerable.Empty<string>();

        return Directory.GetFiles(_backupDirectory, "*", SearchOption.AllDirectories)
            .Select(f => GetVirtualPathFromBackupPath(f))
            .Where(p => !string.IsNullOrEmpty(p));
    }

    public bool HasAnyBackups()
    {
        if (!Directory.Exists(_backupDirectory))
            return false;

        return Directory.EnumerateFiles(_backupDirectory, "*", SearchOption.AllDirectories).Any();
    }

    public Task ClearAllBackupsAsync()
    {
        if (Directory.Exists(_backupDirectory))
        {
            var count = Directory.GetFiles(_backupDirectory, "*", SearchOption.AllDirectories).Length;
            Directory.Delete(_backupDirectory, recursive: true);
            _logService?.LogInfo($"[Backup] Cleared all backups ({count} files)");
        }
        Directory.CreateDirectory(_backupDirectory);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Convert virtual path to backup file path.
    /// Preserves folder structure: "shaders/fog.ffx" -> "%APPDATA%/PoeEditorPatcher/backups/shaders/fog.ffx"
    /// </summary>
    private string GetBackupPath(string virtualPath)
    {
        // Normalize path separators and remove leading slash
        var normalizedPath = virtualPath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        return Path.Combine(_backupDirectory, normalizedPath);
    }

    /// <summary>
    /// Convert backup file path back to virtual path.
    /// </summary>
    private string GetVirtualPathFromBackupPath(string backupPath)
    {
        if (!backupPath.StartsWith(_backupDirectory))
            return string.Empty;

        var relativePath = backupPath.Substring(_backupDirectory.Length)
            .TrimStart(Path.DirectorySeparatorChar)
            .Replace(Path.DirectorySeparatorChar, '/');

        return relativePath;
    }

    /// <summary>
    /// Remove empty directories up to backup root.
    /// </summary>
    private void CleanupEmptyDirectories(string? directory)
    {
        if (string.IsNullOrEmpty(directory))
            return;

        try
        {
            while (directory != null &&
                   directory.StartsWith(_backupDirectory) &&
                   directory != _backupDirectory &&
                   Directory.Exists(directory) &&
                   !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
                directory = Path.GetDirectoryName(directory);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
