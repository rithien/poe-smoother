using System.Text;
using System.Text.Json;
using LibBundle3;
using LibBundle3.Records;
using PoeEditor.Core.Services;
using BundleIndex = LibBundle3.Index;

namespace PoeEditor.Core.Patchers;

// Static logger for all patchers
public static class PatcherLogger
{
    public static ILogService? LogService { get; set; }

    public static void SetLogService(ILogService? logService)
    {
        LogService = logService;
    }
}

/// <summary>
/// Base class for all patchers with common functionality.
/// Implements backup strategy from analysis/backups.md:
/// - First Touch: Backup only on first modification
/// - Markers: Identify patched code via comments
/// - Dirty State Detection: Warn when markers exist without backup
/// </summary>
public abstract class BasePatcher : IPatcher
{
    protected PatcherConfig Config { get; private set; } = new();
    protected Dictionary<string, byte[]> OriginalFiles { get; } = new();
    protected IBackupService? BackupService { get; private set; }

    public virtual string Name => Config.Name;
    public virtual string Description => Config.Description;
    public bool IsEnabled { get; set; }
    public virtual string Category => Config.Category;
    public virtual int ImpactLevel => Config.ImpactLevel;

    /// <summary>
    /// File used to detect if patch is applied. From config markerFile field.
    /// </summary>
    public virtual string MarkerFile => Config.MarkerFile;

    /// <summary>
    /// Marker comment used by this patcher. Format: {{RITHIEN_patchername}}
    /// Uses config marker field, or generates default if not specified.
    /// </summary>
    public virtual string Marker => !string.IsNullOrEmpty(Config.Marker)
        ? Config.Marker
        : $"{{{{RITHIEN_{GetType().Name.Replace("Patcher", "").ToLowerInvariant()}}}}}";

    public virtual void LoadConfig(string configPath)
    {
        if (File.Exists(configPath))
        {
            var json = File.ReadAllText(configPath);
            Config = JsonSerializer.Deserialize<PatcherConfig>(json) ?? new PatcherConfig();
            IsEnabled = Config.Enabled;
            PatcherLogger.LogService?.LogDebug($"[{GetType().Name}] Config loaded from {Path.GetFileName(configPath)}");
        }
        else
        {
            PatcherLogger.LogService?.LogWarning($"[{GetType().Name}] Config not found: {configPath}");
        }
    }

    public void LoadConfigFromJson(string json)
    {
        Config = JsonSerializer.Deserialize<PatcherConfig>(json) ?? new PatcherConfig();
        IsEnabled = Config.Enabled;
    }

    public void SetBackupService(IBackupService? backupService)
    {
        BackupService = backupService;
        // Register this patcher's marker with the backup service
        backupService?.RegisterMarker(GetType().Name, Marker);
    }

    /// <summary>
    /// Check for dirty state before applying patches.
    /// Detects if files have been modified (contain markers) but no backup exists.
    /// </summary>
    public virtual async Task<PrePatchCheckResult> CheckPrePatchStateAsync(BundleIndex index, CancellationToken ct = default)
    {
        var dirtyFiles = new List<DirtyStateResult>();

        if (BackupService == null)
        {
            // No backup service - can't check dirty state
            return new PrePatchCheckResult(true, dirtyFiles);
        }

        return await Task.Run(() =>
        {
            var targetFiles = GetTargetFiles(index);

            foreach (var file in targetFiles)
            {
                ct.ThrowIfCancellationRequested();

                if (file.Path == null) continue;

                var data = file.Read();
                var content = DetectAndDecodeContent(data.Span, file.Path);

                var dirtyState = BackupService.CheckDirtyState(file.Path, content);
                if (dirtyState.IsDirty)
                {
                    dirtyFiles.Add(dirtyState);
                }
            }

            if (dirtyFiles.Count > 0)
            {
                var warning = $"Detected {dirtyFiles.Count} file(s) with modifications but no backup. " +
                             "Please verify game files integrity in Steam/Launcher before continuing.";
                PatcherLogger.LogService?.LogWarning($"[{GetType().Name}] {warning}");
                foreach (var df in dirtyFiles)
                {
                    PatcherLogger.LogService?.LogWarning($"  Dirty file: {df.FilePath}, markers: {string.Join(", ", df.DetectedMarkers)}");
                }
                return new PrePatchCheckResult(false, dirtyFiles, warning);
            }

            return new PrePatchCheckResult(true, dirtyFiles);
        }, ct);
    }

    /// <summary>
    /// Check if this patcher's modifications are already present in the archive.
    /// If markerFile is defined, checks only that file. Otherwise searches all target files.
    /// </summary>
    public virtual async Task<bool> IsAppliedAsync(BundleIndex index, PatchContext? context = null, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            // Helper to get file content (from context or disk)
            ReadOnlySpan<byte> GetContent(FileRecord file)
            {
                if (file.Path != null && context != null && context.TryGetContent(file.Path, out var cachedData))
                {
                    return cachedData;
                }
                return file.Read().Span;
            }

            // If markerFile is defined, check only that file for the marker
            if (!string.IsNullOrEmpty(MarkerFile))
            {
                var markerFileRecord = FindFileByPath(index, MarkerFile);
                if (markerFileRecord != null)
                {
                    var data = GetContent(markerFileRecord);
                    var content = DetectAndDecodeContent(data, MarkerFile);
                    return content.Contains(Marker, StringComparison.Ordinal);
                }
                return false;
            }

            // Fallback: check all target files
            var targetFiles = GetTargetFiles(index);

            foreach (var file in targetFiles)
            {
                ct.ThrowIfCancellationRequested();

                if (file.Path == null) continue;

                var data = GetContent(file);
                var content = DetectAndDecodeContent(data, file.Path);

                // Check if this patcher's marker is present
                if (content.Contains(Marker, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }, ct);
    }

    public virtual async Task<PatchResult> ApplyAsync(BundleIndex index, IProgress<string>? progress = null, PatchContext? context = null, CancellationToken ct = default)
    {
        var patcherName = GetType().Name;
        PatcherLogger.LogService?.LogInfo($"[{patcherName}] Starting patch (repatch={Config.Repatch})");

        return await Task.Run(async () =>
        {
            var modifiedCount = 0;

            try
            {
                // Check if already applied and repatch is disabled (one-time patch)
                if (!Config.Repatch)
                {
                    var isApplied = await IsAppliedAsync(index, context, ct);
                    if (isApplied)
                    {
                        // One-time patch already applied - return success without modifications
                        progress?.Report($"Patch already applied (one-time patch, skipping)");
                        PatcherLogger.LogService?.LogInfo($"[{patcherName}] Already applied (one-time), skipping");
                        return new PatchResult(true, 0, "Already applied");
                    }
                }

                var targetFiles = GetTargetFiles(index);

                foreach (var file in targetFiles)
                {
                    ct.ThrowIfCancellationRequested();

                    progress?.Report($"Patching: {file.Path}");

                    // Read content (from context or disk)
                    byte[] dataBytes;
                    if (file.Path != null && context != null && context.TryGetContent(file.Path, out var cachedData))
                    {
                        dataBytes = cachedData;
                    }
                    else
                    {
                        dataBytes = file.Read().ToArray();
                    }
                    
                    var content = DetectAndDecodeContent(dataBytes, file.Path!);

                    // Store backup using BackupService (First Touch strategy)
                    // IMPORTANT: BackupService will only create backup if one doesn't exist.
                    // Multiple patchers may modify the same file - backup contains ORIGINAL file.
                    if (BackupService != null)
                    {
                        // BackupFileAsync returns false if backup already exists (another patcher backed it up first)
                        // Note: For backup we always want the ORIGINAL disk content, but if we are in a chain of patches,
                        // we might not have easy access to original if it's not on disk yet.
                        // However, First Touch mostly assumes we are backing up what was on disk initially.
                        // If file was modified by previous patcher in context, we still want to back up the *original* state.
                        // But BackupService checks file existence on disk.
                        // Ideally backup happens before ANY modification.
                        // If BackupService checks internal state, it should be fine.
                        
                        // We pass dataBytes here. If dataBytes came from context (modified), that's technically wrong for a backup 
                        // IF this is the first time we see this file. 
                        // BUT: If dataBytes came from context, it means a previous patcher touched it.
                        // That previous patcher would have triggered the backup of the *original* data.
                        // So calling BackupFileAsync here is safe (it will skip if backup exists).
                        await BackupService.BackupFileAsync(file.Path!, dataBytes);
                    }
                    else if (!OriginalFiles.ContainsKey(file.Path!))
                    {
                        // Fallback to in-memory backup
                        // Logic similar to above: if modified in context, we might be backing up modified data 
                        // if we are not careful. But OriginalFiles check handles "First Touch".
                        OriginalFiles[file.Path!] = dataBytes;
                    }

                    // Apply replacements
                    var modifiedContent = ApplyReplacements(content);

                    if (modifiedContent != content)
                    {
                        // Encode back with same encoding
                        var encoding = DetectEncoding(dataBytes, file.Path!);
                        var newData = encoding.GetBytes(modifiedContent);

                        if (context != null)
                        {
                            // Deferred write: update context only
                            context.UpdateContent(file.Path!, newData);
                        }
                        else
                        {
                            // Direct write: write to archive
                            file.Write(newData);
                        }
                        
                        modifiedCount++;
                        PatcherLogger.LogService?.LogDebug($"[{patcherName}] Modified: {file.Path}");
                    }
                }

                PatcherLogger.LogService?.LogInfo($"[{patcherName}] Completed: {modifiedCount} files modified");
                return new PatchResult(true, modifiedCount);
            }
            catch (OperationCanceledException)
            {
                PatcherLogger.LogService?.LogWarning($"[{patcherName}] Cancelled after {modifiedCount} files");
                return new PatchResult(false, modifiedCount, "Operation cancelled");
            }
            catch (Exception ex)
            {
                PatcherLogger.LogService?.LogError($"[{patcherName}] Failed after {modifiedCount} files", ex);
                return new PatchResult(false, modifiedCount, ex.Message);
            }
        }, ct);
    }

    /// <summary>
    /// Revert this patcher's modifications.
    /// NOTE: With simplified backup system, this restores ORIGINAL files.
    /// If multiple patchers modified the same file, ALL patches will be removed.
    /// Other patchers will show as "not applied" after revert.
    /// </summary>
    public virtual async Task<PatchResult> RevertAsync(BundleIndex index, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var patcherName = GetType().Name;
        PatcherLogger.LogService?.LogInfo($"[{patcherName}] Starting revert");

        return await Task.Run(async () =>
        {
            var revertedCount = 0;

            try
            {
                if (BackupService != null)
                {
                    // Get all backup paths and check which files contain this patcher's marker
                    var backupPaths = BackupService.GetAllBackupPaths().ToList();

                    foreach (var virtualPath in backupPaths)
                    {
                        ct.ThrowIfCancellationRequested();

                        var file = FindFileByPath(index, virtualPath);
                        if (file == null) continue;

                        // Check if this file contains THIS patcher's marker
                        var currentData = file.Read();
                        var currentContent = DetectAndDecodeContent(currentData.Span, virtualPath);

                        if (!currentContent.Contains(Marker, StringComparison.Ordinal))
                            continue; // This patcher didn't modify this file

                        progress?.Report($"Reverting: {virtualPath}");

                        // Restore original file from backup
                        var originalData = await BackupService.GetBackupAsync(virtualPath);
                        if (originalData != null)
                        {
                            file.Write(originalData);
                            await BackupService.RemoveBackupAsync(virtualPath);
                            revertedCount++;
                        }
                    }
                }

                // Also revert from in-memory backups (for backwards compatibility)
                foreach (var (path, originalData) in OriginalFiles)
                {
                    ct.ThrowIfCancellationRequested();

                    progress?.Report($"Reverting: {path}");

                    var file = FindFileByPath(index, path);
                    if (file != null)
                    {
                        file.Write(originalData);
                        revertedCount++;
                    }
                }

                OriginalFiles.Clear();
                PatcherLogger.LogService?.LogInfo($"[{patcherName}] Revert completed: {revertedCount} files restored");
                return new PatchResult(true, revertedCount);
            }
            catch (Exception ex)
            {
                PatcherLogger.LogService?.LogError($"[{patcherName}] Revert failed after {revertedCount} files", ex);
                return new PatchResult(false, revertedCount, ex.Message);
            }
        }, ct);
    }

    protected virtual IEnumerable<FileRecord> GetTargetFiles(BundleIndex index)
    {
        var allFiles = GetAllFiles(index);

        foreach (var file in allFiles)
        {
            if (file.Path == null) continue;

            // Normalize path separators for comparison
            var normalizedPath = file.Path.Replace('\\', '/');

            // Check specific files
            if (Config.Targets.Files.Any(f =>
                normalizedPath.Equals(f.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)))
            {
                yield return file;
                continue;
            }

            // Check extensions
            var ext = Path.GetExtension(file.Path);
            if (Config.Targets.Extensions.Any(e => ext.Equals(e, StringComparison.OrdinalIgnoreCase)))
            {
                // Check base paths if specified
                if (Config.Targets.BasePaths.Count == 0 ||
                    Config.Targets.BasePaths.Any(bp => normalizedPath.StartsWith(bp.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)))
                {
                    yield return file;
                }
            }
        }
    }

    protected virtual string ApplyReplacements(string content)
    {
        var result = content;

        foreach (var replacement in Config.Replacements)
        {
            if (string.IsNullOrEmpty(replacement.Pattern) || string.IsNullOrEmpty(replacement.Replacement))
            {
                return result;
            }
            if (replacement.IsRegex)
            {
                result = System.Text.RegularExpressions.Regex.Replace(result, replacement.Pattern, replacement.Replacement);
            }
            else
            {
                result = result.Replace(replacement.Pattern, replacement.Replacement);
            }
        }

        return result;
    }

    protected static IEnumerable<FileRecord> GetAllFiles(BundleIndex index)
    {
        // LibBundle3.Index has a Files property that is a Dictionary<ulong, FileRecord>
        return index.Files.Values;
    }

    protected static FileRecord? FindFileByPath(BundleIndex index, string path)
    {
        return GetAllFiles(index).FirstOrDefault(f => f.Path?.Equals(path, StringComparison.OrdinalIgnoreCase) == true);
    }

    protected static string DetectAndDecodeContent(ReadOnlySpan<byte> data, string filePath)
    {
        var encoding = DetectEncoding(data, filePath);
        return encoding.GetString(data);
    }

    protected static Encoding DetectEncoding(ReadOnlySpan<byte> data, string filePath)
    {
        if (data.Length == 0) return Encoding.UTF8;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        // Check for UTF-16 LE BOM (FF FE)
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
        {
            return Encoding.Unicode;
        }

        // Check for UTF-16 BE BOM (FE FF)
        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode;
        }

        // .ot, .otc, .it, .itc, .ao, .aoc files are typically UTF-16 LE without BOM
        if (extension is ".ot" or ".otc" or ".it" or ".itc" or ".ao" or ".aoc")
        {
            if (data.Length >= 4)
            {
                int nullCount = 0;
                int sampleSize = Math.Min(100, data.Length / 2);

                for (int i = 1; i < sampleSize * 2; i += 2)
                {
                    if (data[i] == 0x00) nullCount++;
                }

                if (nullCount > sampleSize * 0.8)
                {
                    return Encoding.Unicode;
                }
            }
        }

        // Default to UTF-8
        return Encoding.UTF8;
    }
}
