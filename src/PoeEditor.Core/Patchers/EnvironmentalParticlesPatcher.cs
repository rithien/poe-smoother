using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LibBundle3;
using LibBundle3.Records;
using PoeEditor.Core.Services;
using BundleIndex = LibBundle3.Index;

namespace PoeEditor.Core.Patchers;

/// <summary>
/// Patcher that disables environmental particle effects (fog, mist, dust, glowworms).
/// Targets .aoc files in metadata/terrain/ matching specific patterns.
/// Comments out continuous_effect lines in ParticleEffects sections.
/// </summary>
public class EnvironmentalParticlesPatcher : BasePatcher
{
    private readonly List<string> _patterns = new();
    private readonly List<string> _excludePaths = new();

    public EnvironmentalParticlesPatcher()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patchers", "envparticles.json");
        LoadConfig(configPath);
        LoadExtendedConfig(configPath);
    }

    private void LoadExtendedConfig(string configPath)
    {
        if (!File.Exists(configPath)) return;

        try
        {
            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Load patterns array
            if (root.TryGetProperty("patterns", out var patternsElement))
            {
                foreach (var pattern in patternsElement.EnumerateArray())
                {
                    var patternStr = pattern.GetString();
                    if (!string.IsNullOrEmpty(patternStr))
                    {
                        _patterns.Add(patternStr.ToLowerInvariant());
                    }
                }
            }

            // Load excludePaths array
            if (root.TryGetProperty("excludePaths", out var excludeElement))
            {
                foreach (var exclude in excludeElement.EnumerateArray())
                {
                    var excludeStr = exclude.GetString();
                    if (!string.IsNullOrEmpty(excludeStr))
                    {
                        _excludePaths.Add(excludeStr.ToLowerInvariant().Replace('\\', '/'));
                    }
                }
            }
        }
        catch
        {
            // Fall back to defaults if parsing fails
            _patterns.AddRange(new[] { "*fog*.aoc", "*mist*.aoc", "*dust*.aoc", "*glowworm*.aoc" });
        }
    }

    /// <summary>
    /// Override GetTargetFiles to implement wildcard pattern matching.
    /// </summary>
    protected override IEnumerable<FileRecord> GetTargetFiles(BundleIndex index)
    {
        var allFiles = GetAllFiles(index);
        var basePaths = Config.Targets.BasePaths
            .Select(bp => bp.ToLowerInvariant().Replace('\\', '/'))
            .ToList();

        foreach (var file in allFiles)
        {
            if (file.Path == null) continue;

            var normalizedPath = file.Path.ToLowerInvariant().Replace('\\', '/');
            var fileName = Path.GetFileName(normalizedPath);
            var extension = Path.GetExtension(normalizedPath);

            // Must be .aoc file
            if (!extension.Equals(".aoc", StringComparison.OrdinalIgnoreCase))
                continue;

            // Must be within base paths (metadata/terrain/)
            if (basePaths.Count > 0 && !basePaths.Any(bp => normalizedPath.StartsWith(bp)))
                continue;

            // Must NOT be in excluded paths
            if (_excludePaths.Any(ep => normalizedPath.StartsWith(ep)))
                continue;

            // Must match one of the patterns
            if (!MatchesAnyPattern(fileName))
                continue;

            yield return file;
        }
    }

    private bool MatchesAnyPattern(string fileName)
    {
        foreach (var pattern in _patterns)
        {
            if (WildcardMatch(fileName, pattern))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Simple wildcard matching (* matches any characters).
    /// </summary>
    private static bool WildcardMatch(string input, string pattern)
    {
        // Convert wildcard pattern to regex
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Override ApplyReplacements to comment out continuous_effect lines in ParticleEffects section.
    /// </summary>
    protected override string ApplyReplacements(string content)
    {
        // Pattern to find continuous_effect lines (with optional existing marker)
        // Matches: continuous_effect = "..." (optionally already commented with our marker)
        var pattern = @"(\s*)(//\{\{RITHIEN_envparticles\}\}\s*)?(continuous_effect\s*=\s*""[^""]*"")";

        var result = Regex.Replace(content, pattern, match =>
        {
            var whitespace = match.Groups[1].Value;
            var existingMarker = match.Groups[2].Value;
            var effectLine = match.Groups[3].Value;

            // If already has our marker, don't double-comment
            if (!string.IsNullOrEmpty(existingMarker))
            {
                return match.Value;
            }

            // Add marker comment
            return $"{whitespace}//{{{{RITHIEN_envparticles}}}} {effectLine}";
        });

        return result;
    }

    /// <summary>
    /// Override ApplyAsync to provide custom patching logic with parallel processing.
    /// Uses Parallel.ForEachAsync for better performance on large file sets.
    /// </summary>
    public override async Task<PatchResult> ApplyAsync(BundleIndex index, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        PatcherLogger.LogService?.LogInfo("[EnvParticles] Starting patch");

        return await Task.Run(async () =>
        {
            int modifiedCount = 0;
            int processedCount = 0;
            int skippedCount = 0;
            var indexLock = new object();

            try
            {
                var targetFiles = GetTargetFiles(index).ToList();
                var totalFiles = targetFiles.Count;

                PatcherLogger.LogService?.LogInfo($"[EnvParticles] Found {totalFiles} files matching patterns");
                progress?.Report($"Found {totalFiles} environmental particle files to patch...");

                // Parallel processing with controlled degree of parallelism
                await Parallel.ForEachAsync(
                    targetFiles,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
                        CancellationToken = ct
                    },
                    async (file, token) =>
                    {
                        if (file.Path == null) return;

                        int currentProcessed = Interlocked.Increment(ref processedCount);

                        // Read original content (lock for index thread safety)
                        byte[] dataArray;
                        string content;
                        lock (indexLock)
                        {
                            var data = file.Read();
                            dataArray = data.ToArray();
                            content = DetectAndDecodeContent(data.Span, file.Path);
                        }

                        // Skip if no continuous_effect to patch
                        if (!content.Contains("continuous_effect", StringComparison.OrdinalIgnoreCase))
                        {
                            Interlocked.Increment(ref skippedCount);
                            return;
                        }

                        // Store backup using BackupService (First Touch strategy, thread-safe)
                        if (BackupService != null && !content.Contains(Marker))
                        {
                            await BackupService.BackupFileAsync(file.Path, dataArray);
                        }

                        // Apply replacements (thread-safe - string operations)
                        var modifiedContent = ApplyReplacements(content);

                        if (modifiedContent != content)
                        {
                            // Encode back with same encoding (UTF-16 LE for .aoc files)
                            var encoding = DetectEncoding(dataArray.AsSpan(), file.Path);
                            var newData = encoding.GetBytes(modifiedContent);

                            // Write back to archive (lock for index thread safety)
                            lock (indexLock)
                            {
                                file.Write(newData);
                            }

                            int currentModified = Interlocked.Increment(ref modifiedCount);

                            if (currentModified % 20 == 0 || currentModified == 1)
                            {
                                progress?.Report($"Patched {currentModified} files... ({currentProcessed}/{totalFiles})");
                            }
                        }
                    });

                PatcherLogger.LogService?.LogInfo($"[EnvParticles] Completed: {modifiedCount} modified, {skippedCount} skipped (no effects)");
                progress?.Report($"Environmental particles: Patched {modifiedCount} files");
                return new PatchResult(true, modifiedCount);
            }
            catch (OperationCanceledException)
            {
                PatcherLogger.LogService?.LogWarning($"[EnvParticles] Cancelled after {modifiedCount} files");
                return new PatchResult(false, modifiedCount, "Operation cancelled");
            }
            catch (Exception ex)
            {
                PatcherLogger.LogService?.LogError($"[EnvParticles] Failed after {modifiedCount} files", ex);
                return new PatchResult(false, modifiedCount, ex.Message);
            }
        }, ct);
    }

    /// <summary>
    /// Override RevertAsync to restore original files from backup.
    /// NOTE: With simplified backup system, this restores ORIGINAL files.
    /// Uses parallel processing for better performance.
    /// </summary>
    public override async Task<PatchResult> RevertAsync(BundleIndex index, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        PatcherLogger.LogService?.LogInfo("[EnvParticles] Starting revert");

        return await Task.Run(async () =>
        {
            int revertedCount = 0;
            var indexLock = new object();

            try
            {
                if (BackupService == null)
                {
                    PatcherLogger.LogService?.LogError("[EnvParticles] No backup service available");
                    return new PatchResult(false, 0, "No backup service available");
                }

                var backupPaths = BackupService.GetAllBackupPaths().ToList();

                PatcherLogger.LogService?.LogDebug($"[EnvParticles] Checking {backupPaths.Count} backup files");
                progress?.Report($"Checking {backupPaths.Count} backup files...");

                // Parallel processing for revert
                await Parallel.ForEachAsync(
                    backupPaths,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
                        CancellationToken = ct
                    },
                    async (virtualPath, token) =>
                    {
                        FileRecord? file;
                        string currentContent;

                        lock (indexLock)
                        {
                            file = FindFileByPath(index, virtualPath);
                            if (file == null) return;

                            // Check if this file contains THIS patcher's marker
                            var currentData = file.Read();
                            currentContent = DetectAndDecodeContent(currentData.Span, virtualPath);
                        }

                        if (!currentContent.Contains(Marker, StringComparison.Ordinal))
                            return; // This patcher didn't modify this file

                        var originalData = await BackupService.GetBackupAsync(virtualPath);
                        if (originalData != null)
                        {
                            lock (indexLock)
                            {
                                file.Write(originalData);
                            }

                            await BackupService.RemoveBackupAsync(virtualPath);

                            int currentReverted = Interlocked.Increment(ref revertedCount);
                            if (currentReverted % 10 == 0 || currentReverted == 1)
                            {
                                progress?.Report($"Reverted {currentReverted} files...");
                            }
                        }
                    });

                PatcherLogger.LogService?.LogInfo($"[EnvParticles] Revert completed: {revertedCount} files restored");
                progress?.Report($"Environmental particles: Restored {revertedCount} files");
                return new PatchResult(true, revertedCount);
            }
            catch (OperationCanceledException)
            {
                PatcherLogger.LogService?.LogWarning($"[EnvParticles] Revert cancelled after {revertedCount} files");
                return new PatchResult(false, revertedCount, "Operation cancelled");
            }
            catch (Exception ex)
            {
                PatcherLogger.LogService?.LogError($"[EnvParticles] Revert failed after {revertedCount} files", ex);
                return new PatchResult(false, revertedCount, ex.Message);
            }
        }, ct);
    }
}
