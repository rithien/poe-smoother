using System.Globalization;
using System.IO;
using LibBundle3;
using BundleIndex = LibBundle3.Index;

namespace PoeEditor.Core.Patchers;

/// <summary>
/// Patcher that increases global brightness by multiplying final color.
/// </summary>
public class BrightnessPatcher : BasePatcher
{
    private float _multiplier = 1.25f;

    public float Multiplier
    {
        get => _multiplier;
        set => _multiplier = Math.Clamp(value, 1.0f, 2.0f);
    }

    public BrightnessPatcher(float multiplier = 1.25f)
    {
        _multiplier = multiplier;
        UpdateConfig();
        // Load config initially to get marker/markerFile if redundant
        //var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patchers", "brightness.json");
        //LoadConfig(configPath);
    }

    // Explicitly override to ensure we use the config's marker or default
    public override string Marker => base.Marker; // Uses logic from BasePatcher

    private void UpdateConfig()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patchers", "brightness.json");
        if (File.Exists(configPath))
        {
            var multiplierStr = _multiplier.ToString("F2", CultureInfo.InvariantCulture);
            var percentageStr = ((_multiplier * 100) - 100).ToString("F0", CultureInfo.InvariantCulture);

            var json = File.ReadAllText(configPath);
            json = json.Replace("{{MULTIPLIER}}", multiplierStr)
                       .Replace("{{PERCENTAGE}}", percentageStr);

            LoadConfigFromJson(json);
        }
    }

    public override string Name => $"Brightness x{_multiplier.ToString("F2", CultureInfo.InvariantCulture)}";

    public override async Task<PatchResult> ApplyAsync(BundleIndex index, IProgress<string>? progress = null, PatchContext? context = null, CancellationToken ct = default)
    {
        return await Task.Run(async () =>
        {
            var modifiedCount = 0;

            try
            {
                var targetFiles = GetTargetFiles(index).ToList();

                foreach (var file in targetFiles)
                {
                    ct.ThrowIfCancellationRequested();

                    if (file.Path == null) continue;

                    progress?.Report($"Patching brightness: {file.Path}");

                    // Read content (from context or disk)
                    byte[] dataBytes;
                    if (context != null && context.TryGetContent(file.Path, out var cachedData))
                    {
                        dataBytes = cachedData;
                    }
                    else
                    {
                        dataBytes = file.Read().ToArray();
                    }

                    var encoding = DetectEncoding(dataBytes.AsSpan(), file.Path);
                    var content = encoding.GetString(dataBytes.AsSpan());

                    // Check for marker existence
                    bool hasMarker = content.Contains(Marker);


                    // "jak brak markera to robimy backup i patchujemy, jak jest marker to robimy repatch bez backupu"

                    if (!hasMarker)
                    {
                        // Store backup using BackupService (First Touch strategy)
                        if (BackupService != null)
                        {
                            await BackupService.BackupFileAsync(file.Path, dataBytes);
                        }
                        else if (!OriginalFiles.ContainsKey(file.Path))
                        {
                            OriginalFiles[file.Path] = dataBytes;
                        }
                    }

                    var modifiedContent = ApplyBrightnessBoost(content);

                    if (modifiedContent != content)
                    {
                        var newData = encoding.GetBytes(modifiedContent);
                        
                        if (context != null)
                        {
                            // Deferred write
                            context.UpdateContent(file.Path, newData);
                        }
                        else
                        {
                            // Direct write
                            file.Write(newData);
                        }
                        
                        modifiedCount++;
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

    private string ApplyBrightnessBoost(string content)
    {
        const string pattern = "return colour * hdr_scale;";

        // Use dynamic marker
        string currentMarker = $"{Marker}";
        // Note: The marker from config is usually just "{{RITHIEN_brightness}}", checking usage in other files
        // BasePatcher.Marker returns "{{RITHIEN_brightness}}". 
        // CLAUDE.md example: " //{{RITHIEN_camerazoom}}"
        // So we should append it as a comment.

        // Format multiplier with invariant culture to ensure dot as decimal separator (HLSL requirement)
        var multiplierStr = _multiplier.ToString("F2", CultureInfo.InvariantCulture);

        // Check if brightness boost already applied - replace with new value
        if (content.Contains(Marker))
        {
            // Regex to find existing line with our marker
            // We look for the specific marker string literally
            var markerEscaped = System.Text.RegularExpressions.Regex.Escape(Marker);
            // Match "colour.rgb *= [number]f; // [marker]"
            var existingPattern = $@"colour\.rgb \*= [\d\.,]+f; //\s*{markerEscaped}";

            var newBoostLine = $"colour.rgb *= {multiplierStr}f; //{Marker}";
            return System.Text.RegularExpressions.Regex.Replace(content, existingPattern, newBoostLine);
        }

        // First time applying - insert before return statement
        if (!content.Contains(pattern))
            return content;

        var replacement = $"colour.rgb *= {multiplierStr}f; //{Marker}\r\n\treturn colour * hdr_scale;";

        // If the marker was NOT found, we just do a simple replace of the target pattern
        return content.Replace(pattern, replacement);
    }
}
