using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using LibBundle3;
using BundleIndex = LibBundle3.Index;

namespace PoeEditor.Core.Patchers;

/// <summary>
/// Patcher that increases SDR scale for brighter output.
/// Modifies the OutputSDR function in oetf.hlsl.
/// </summary>
public class SdrScalePatcher : BasePatcher
{
    private float _multiplier = 1.25f;

    public float Multiplier
    {
        get => _multiplier;
        set => _multiplier = Math.Clamp(value, 1.0f, 2.0f);
    }

    public SdrScalePatcher(float multiplier = 1.25f)
    {
        _multiplier = multiplier;
        LoadExternalConfig();
    }

    private void LoadExternalConfig()
    {
        var multiplierStr = _multiplier.ToString("F2", CultureInfo.InvariantCulture);
        var percentageStr = ((_multiplier * 100) - 100).ToString("F0", CultureInfo.InvariantCulture);

        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patchers", "sdrscale.json");
        if (File.Exists(configPath))
        {
            var json = File.ReadAllText(configPath);
            json = json.Replace("{{MULTIPLIER}}", multiplierStr)
                       .Replace("{{PERCENTAGE}}", percentageStr);
            LoadConfigFromJson(json);
        }
    }

    public override string Name => $"SDR Scale x{_multiplier.ToString("F2", CultureInfo.InvariantCulture)}";

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

                    progress?.Report($"Patching SDR scale: {file.Path}");

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

                    // Store backup using BackupService (First Touch strategy)
                    if (BackupService != null)
                    {
                        // Check if we need backup (if no marker) - simplified logic relies on BackupService not overwriting
                        // But strictly we should check marker. ApplyReplacements does check, but here we do it manually?
                        // Base implementation checks marker. Here we don't seem to check marker before backup explicitly in original code?
                        // Original code: always calls BackupFileAsync. BackupService handles "don't overwrite original".
                        await BackupService.BackupFileAsync(file.Path, dataBytes);
                    }
                    else if (!OriginalFiles.ContainsKey(file.Path))
                    {
                        OriginalFiles[file.Path] = dataBytes;
                    }

                    var modifiedContent = ApplySdrScaleBoost(content);

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

    private string ApplySdrScaleBoost(string content)
    {
        var multiplierStr = _multiplier.ToString("F2", CultureInfo.InvariantCulture);
        var escapedMarker = Regex.Escape(Marker);

        // Check if already patched (marker present) - replace with new value
        if (content.Contains(Marker))
        {
            // Replace existing boost line
            var existingPattern = $@"return float4\(linCol\.xyz \* sdr_scale \* [\d\.]+f, linCol\.w\); //{escapedMarker}";
            var newLine = $"return float4(linCol.xyz * sdr_scale * {multiplierStr}f, linCol.w); //{Marker}";
            return Regex.Replace(content, existingPattern, newLine);
        }

        // First time applying - find and modify OutputSDR function
        const string originalPattern = "return float4(linCol.xyz * sdr_scale, linCol.w);";

        if (!content.Contains(originalPattern))
            return content;

        var replacement = $"return float4(linCol.xyz * sdr_scale * {multiplierStr}f, linCol.w); //{Marker}";
        return content.Replace(originalPattern, replacement);
    }
}
