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

    /// <summary>
    /// Cached result of IsApplied check, set during CheckPatchStatusAsync.
    /// Used to avoid repeated Bundle reads which can cause cache issues in LibBundle3.
    /// </summary>
    public bool? CachedIsApplied { get; set; }

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
    public virtual async Task<bool> IsAppliedAsync(BundleIndex index, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            // If markerFile is defined, check only that file for the marker
            if (!string.IsNullOrEmpty(MarkerFile))
            {
                var markerFileRecord = FindFileByPath(index, MarkerFile);
                if (markerFileRecord != null)
                {
                    var data = markerFileRecord.Read();
                    var content = DetectAndDecodeContent(data.Span, MarkerFile);
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

                var data = file.Read();
                var content = DetectAndDecodeContent(data.Span, file.Path);

                // Check if this patcher's marker is present
                if (content.Contains(Marker, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }, ct);
    }

    public virtual async Task<PatchResult> ApplyAsync(BundleIndex index, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        // Use Name property for logging (ExternalPatcher overrides this with config name)
        var patcherName = Name ?? GetType().Name;
        PatcherLogger.LogService?.LogInfo($"[{patcherName}] Starting patch (repatch={Config.Repatch})");

        return await Task.Run(async () =>
        {
            var modifiedCount = 0;
            string? currentFilePath = null;

            try
            {
                // Check if already applied and repatch is disabled (one-time patch)
                if (!Config.Repatch)
                {
                    // Use cached result if available (from CheckPatchStatusAsync)
                    // to avoid repeated Bundle reads which can cause LibBundle3 cache issues
                    bool isApplied;
                    if (CachedIsApplied.HasValue)
                    {
                        isApplied = CachedIsApplied.Value;
                        PatcherLogger.LogService?.LogDebug($"[{patcherName}] Using cached IsApplied={isApplied}");
                    }
                    else
                    {
                        isApplied = await IsAppliedAsync(index, ct);
                    }

                    if (isApplied)
                    {
                        // One-time patch already applied - return success without modifications
                        progress?.Report($"Patch already applied (one-time patch, skipping)");
                        PatcherLogger.LogService?.LogInfo($"[{patcherName}] Already applied (one-time), skipping");
                        return new PatchResult(true, 0, "Already applied");
                    }
                }

                var targetFiles = GetTargetFiles(index).ToList();
                PatcherLogger.LogService?.LogDebug($"[{patcherName}] Found {targetFiles.Count} target files");

                foreach (var file in targetFiles)
                {
                    ct.ThrowIfCancellationRequested();
                    currentFilePath = file.Path;

                    progress?.Report($"Patching: {file.Path}");
                    PatcherLogger.LogService?.LogDebug($"[{patcherName}] Reading file: {file.Path}, Size={file.Size}, BundleIndex={file.BundleRecord?.BundleIndex}");

                    // Read original content
                    var data = file.Read();
                    var content = DetectAndDecodeContent(data.Span, file.Path!);

                    // Store backup using BackupService (First Touch strategy)
                    // IMPORTANT: BackupService will only create backup if one doesn't exist.
                    // Multiple patchers may modify the same file - backup contains ORIGINAL file.
                    if (BackupService != null)
                    {
                        // BackupFileAsync returns false if backup already exists (another patcher backed it up first)
                        await BackupService.BackupFileAsync(file.Path!, data.ToArray());
                    }
                    else if (!OriginalFiles.ContainsKey(file.Path!))
                    {
                        // Fallback to in-memory backup
                        OriginalFiles[file.Path!] = data.ToArray();
                    }

                    // Apply replacements
                    var modifiedContent = ApplyReplacements(content);

                    if (modifiedContent != content)
                    {
                        // Encode back with same encoding
                        var encoding = DetectEncoding(data.Span, file.Path!);
                        var newData = encoding.GetBytes(modifiedContent);

                        // Write back to archive
                        file.Write(newData);
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
                PatcherLogger.LogService?.LogError($"[{patcherName}] Failed after {modifiedCount} files at file: {currentFilePath ?? "unknown"}", ex);
                return new PatchResult(false, modifiedCount, $"{ex.Message} (file: {currentFilePath ?? "unknown"})");
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
