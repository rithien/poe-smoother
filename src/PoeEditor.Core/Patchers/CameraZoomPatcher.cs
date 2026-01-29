using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using LibBundle3;
using LibBundle3.Records;
using BundleIndex = LibBundle3.Index;

namespace PoeEditor.Core.Patchers;

/// <summary>
/// Patcher that modifies camera zoom level.
/// Adds CreateCameraZoomNode to character.ot and modifies existing zoom nodes in .otc files.
/// </summary>
public class CameraZoomPatcher : BasePatcher
{
    private int _zoomLevel = 2;

    public int ZoomLevel
    {
        get => _zoomLevel;
        set => _zoomLevel = Math.Clamp(value, 1, 3);
    }

    public CameraZoomPatcher(int zoomLevel = 2)
    {
        _zoomLevel = zoomLevel;

        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patchers", "camerazoom.json");
        if (File.Exists(configPath))
        {
            var json = File.ReadAllText(configPath);
            json = json.Replace("{{ZOOM_LEVEL}}", _zoomLevel.ToString());
            LoadConfigFromJson(json);
        }
    }

    public override async Task<PatchResult> ApplyAsync(BundleIndex index, IProgress<string>? progress = null, PatchContext? context = null, CancellationToken ct = default)
    {
        return await Task.Run(async () =>
        {
            var modifiedCount = 0;

            try
            {
                // Step 1: Check markerFile for marker and handle backup
                if (string.IsNullOrEmpty(MarkerFile))
                {
                     return new PatchResult(false, 0, "MarkerFile not defined in config");
                }

                var markerFileRecord = FindFileByPath(index, MarkerFile);
                if (markerFileRecord == null)
                {
                    return new PatchResult(false, 0, $"Marker file not found: {MarkerFile}");
                }

                // Read content (from context or disk)
                byte[] markerFileDataBytes;
                if (context != null && context.TryGetContent(MarkerFile, out var cachedData))
                {
                    markerFileDataBytes = cachedData;
                }
                else
                {
                    markerFileDataBytes = markerFileRecord.Read().ToArray();
                }

                var markerFileEncoding = DetectEncoding(markerFileDataBytes.AsSpan(), MarkerFile);
                var markerFileContent = markerFileEncoding.GetString(markerFileDataBytes.AsSpan());
                var hasMarker = markerFileContent.Contains(Marker, StringComparison.Ordinal);

                progress?.Report($"Marker file check: hasMarker={hasMarker}");

                // Handle backup according to CLAUDE.md
                if (!hasMarker)
                {
                    if (BackupService != null)
                    {
                        if (BackupService.HasBackup(MarkerFile))
                        {
                            // No marker but has backup - delete old backup
                            await BackupService.RemoveBackupAsync(MarkerFile);
                            progress?.Report("Removed stale backup (no marker found)");
                        }
                        // Will create new backup when patching
                    }
                }

                // Step 2: Patch character.ot (MarkerFile)
                progress?.Report($"Patching: {MarkerFile}");
                var markerFileModified = await PatchMarkerFileAsync(markerFileRecord, markerFileContent, markerFileEncoding, hasMarker, markerFileDataBytes, progress, context);
                if (markerFileModified)
                {
                    modifiedCount++;
                }

                // Step 3: Search and patch .otc files
                var targetFiles = GetTargetFiles(index);
                
                // Exclude MarkerFile from general search to avoid double processing
                var additionalFiles = targetFiles
                    .Where(f => !f.Path.Equals(MarkerFile, StringComparison.OrdinalIgnoreCase) && 
                                f.Path.EndsWith(".otc", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                progress?.Report($"Searching {additionalFiles.Count} target files for CreateCameraZoomNode...");

                foreach (var file in additionalFiles)
                {
                    ct.ThrowIfCancellationRequested();

                    if (file.Path == null) continue;

                    // Read content (from context or disk)
                    byte[] dataBytes;
                    if (context != null && context.TryGetContent(file.Path, out var cached))
                    {
                        dataBytes = cached;
                    }
                    else
                    {
                        dataBytes = file.Read().ToArray();
                    }

                    var encoding = DetectEncoding(dataBytes.AsSpan(), file.Path);
                    var content = encoding.GetString(dataBytes.AsSpan());

                    // Only process files that contain CreateCameraZoomNode
                    if (!content.Contains("CreateCameraZoomNode", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    progress?.Report($"Found CreateCameraZoomNode in: {file.Path}");

                    var fileHasMarker = content.Contains(Marker, StringComparison.Ordinal);

                    // Backup if no marker (first time patching this file)
                    if (!fileHasMarker && BackupService != null)
                    {
                        await BackupService.BackupFileAsync(file.Path, dataBytes);
                    }

                    // Apply patch
                    var modifiedContent = PatchCreateCameraZoomNode(content, fileHasMarker);

                    if (modifiedContent != content)
                    {
                        var newData = encoding.GetBytes(modifiedContent);
                        
                        if (context != null)
                        {
                            context.UpdateContent(file.Path, newData);
                        }
                        else
                        {
                            file.Write(newData);
                        }
                        
                        modifiedCount++;
                        progress?.Report($"Modified: {file.Path}");
                    }
                }

                return new PatchResult(true, modifiedCount);
            }
            catch (OperationCanceledException)
            {
                return new PatchResult(false, modifiedCount, "Operation cancelled");
            }
            catch (Exception ex)
            {
                return new PatchResult(false, modifiedCount, ex.Message);
            }
        }, ct);
    }

    private async Task<bool> PatchMarkerFileAsync(FileRecord file, string content, Encoding encoding, bool hasMarker, byte[] originalData, IProgress<string>? progress, PatchContext? context = null)
    {
        string modifiedContent;

        if (hasMarker)
        {
            // Re-patch: update existing zoom value
            modifiedContent = UpdateExistingZoomInCharacterOt(content);
            progress?.Report($"Re-patching {Path.GetFileName(MarkerFile)} (updating zoom value)");
        }
        else
        {
            // First patch: backup and add new line
            if (BackupService != null)
            {
                await BackupService.BackupFileAsync(file.Path!, originalData);
            }
            modifiedContent = AddZoomLineToCharacterOt(content);
            progress?.Report($"Adding zoom line to {Path.GetFileName(MarkerFile)}");
        }

        if (modifiedContent != content)
        {
            var newData = encoding.GetBytes(modifiedContent);
            
            if (context != null)
            {
                context.UpdateContent(file.Path!, newData);
            }
            else
            {
                file.Write(newData);
            }
            return true;
        }

        return false;
    }

    private string AddZoomLineToCharacterOt(string content)
    {
        // Find Positioned section
        var positionedStart = content.IndexOf("Positioned", StringComparison.OrdinalIgnoreCase);
        if (positionedStart == -1) return content;

        var openBrace = content.IndexOf('{', positionedStart);
        if (openBrace == -1) return content;

        // Find the matching closing brace for Positioned
        var braceCount = 1;
        var closeBrace = -1;
        for (var i = openBrace + 1; i < content.Length && braceCount > 0; i++)
        {
            if (content[i] == '{') braceCount++;
            else if (content[i] == '}') braceCount--;
            if (braceCount == 0) closeBrace = i;
        }

        if (closeBrace == -1) return content;

        // Detect indentation
        var lastNewLine = content.LastIndexOf('\n', closeBrace);
        var indentation = "\t"; // Default fallback
        if (lastNewLine != -1)
        {
            var closingLineStart = lastNewLine + 1;
            // Capture chars from new line up to brace
            var closingLineIndent = content[closingLineStart..closeBrace];
            if (string.IsNullOrWhiteSpace(closingLineIndent))
            {
                // Use closing brace indent + one tab (or 4 spaces if that's what is used)
                var step = closingLineIndent.Contains('\t') ? "\t" : "    ";
                if (closingLineIndent.Length == 0) step = "\t"; // Fallback if no indentation at all
                indentation = closingLineIndent + step;
            }
        }

        // Insert the zoom line before the closing brace
        // Use CRLF as it's Windows based, or detect? Defaulting to \r\n for consistency with previous code
        var zoomLine = $"\r\n{indentation}on_initial_position_set = \"CreateCameraZoomNode(1000000, 1000000, {_zoomLevel});\" //{Marker}";
        var newContent = content.Insert(closeBrace, zoomLine);

        return newContent;
    }

    private string UpdateExistingZoomInCharacterOt(string content)
    {
        var escapedMarker = Regex.Escape(Marker);
        // Pattern to match existing zoom line with marker
        var pattern = $@"on_initial_position_set\s*=\s*""CreateCameraZoomNode\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)\s*;""\s*//{escapedMarker}";
        var replacement = $"on_initial_position_set = \"CreateCameraZoomNode(1000000, 1000000, {_zoomLevel});\" //{Marker}";

        return Regex.Replace(content, pattern, replacement);
    }

    private string PatchCreateCameraZoomNode(string content, bool hasMarker)
    {
        string result;
        var escapedMarker = Regex.Escape(Marker);

        if (hasMarker)
        {
            // Re-patch: update existing patched lines
            var markerPattern = $@"CreateCameraZoomNode\s*\(\s*1\s*,\s*1\s*,\s*(\d+)\s*\)\s*;\s*//{escapedMarker}";
            result = Regex.Replace(content, markerPattern, m =>
                $"CreateCameraZoomNode(1, 1, {_zoomLevel});//{Marker}");
        }
        else
        {
            // First patch: replace original CreateCameraZoomNode calls
            var pattern = @"CreateCameraZoomNode\s*\(\s*(\d+(?:\.\d+)?)\s*,\s*(\d+(?:\.\d+)?)\s*,\s*(\d+(?:\.\d+)?)\s*\)\s*;";
            result = Regex.Replace(content, pattern, m =>
                $"CreateCameraZoomNode(1, 1, {_zoomLevel});//{Marker}");
        }

        return result;
    }

    protected override string ApplyReplacements(string content)
    {
        return content;
    }

    /// <summary>
    /// Checks the exact zoom level applied in the file.
    /// Returns 0 if not applied or unknown.
    /// </summary>
    public async Task<int> GetAppliedZoomLevelAsync(BundleIndex index)
    {
        return await Task.Run(() => 
        {
            if (string.IsNullOrEmpty(MarkerFile)) return 0;

            var markerFileRecord = FindFileByPath(index, MarkerFile);
            if (markerFileRecord == null) return 0;

            var data = markerFileRecord.Read();
            var content = DetectAndDecodeContent(data.Span, MarkerFile);
            
            var escapedMarker = Regex.Escape(Marker);
            var pattern = $@"on_initial_position_set\s*=\s*""CreateCameraZoomNode\s*\(\s*\d+\s*,\s*\d+\s*,\s*(\d+)\s*\)\s*;""\s*//{escapedMarker}";
            
            var match = Regex.Match(content, pattern);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int zoom))
            {
                return zoom;
            }
            
            return 0;
        });
    }
}
