using System.Globalization;
using System.Text.RegularExpressions;
using LibBundle3;
using BundleIndex = LibBundle3.Index;

namespace PoeEditor.Core.Patchers;

/// <summary>
/// Patcher that modifies gamma curve for brighter/darker output.
/// Lower gamma value = brighter midtones.
/// Modifies OETF_REC709 function in oetf.hlsl (float m = 1.0f / X).
/// </summary>
public class GammaPatcher : BasePatcher
{
    private float _gamma = 2.0f;

    /// <summary>
    /// Gamma value. Lower = brighter, Higher = darker.
    /// Default in game: 2.4 (sRGB), Bright: 2.0, Very Bright: 1.8
    /// </summary>
    public float Gamma
    {
        get => _gamma;
        set => _gamma = Math.Clamp(value, 1.4f, 2.6f);
    }

    public GammaPatcher(float gamma = 2.0f)
    {
        _gamma = gamma;
        UpdateConfig();
        // Load config initially to get marker/markerFile if redundant
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patchers", "gamma.json");
        LoadConfig(configPath);
    }

    public override string Marker => base.Marker;

    private void UpdateConfig()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patchers", "gamma.json");
        if (File.Exists(configPath))
        {
            var gammaStr = _gamma.ToString("F1", CultureInfo.InvariantCulture);
            var description = _gamma switch
            {
                <= 1.8f => "Significantly brighter midtones",
                <= 2.0f => "Brighter midtones",
                >= 2.4f => "Darker midtones",
                _ => "Slightly adjusted midtones"
            };

            var json = File.ReadAllText(configPath);
            json = json.Replace("{{GAMMA_VALUE}}", gammaStr)
                       .Replace("{{DESCRIPTION}}", description);

            LoadConfigFromJson(json);
        }
    }

    public override string Name => $"Gamma {_gamma.ToString("F1", CultureInfo.InvariantCulture)}";

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

                    progress?.Report($"Patching gamma: {file.Path}");

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

                    var modifiedContent = ApplyGammaChange(content);

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

    private string ApplyGammaChange(string content)
    {
        var gammaStr = _gamma.ToString("F1", CultureInfo.InvariantCulture);

        // Regex pattern that matches both original and already patched versions:
        // float m = 1.0f / 2.4f;  (original)
        // float m = 1.0f / 2.0f; //{{RITHIEN_gamma}}  (patched)
        var markerEscaped = Regex.Escape(Marker);
        var pattern = $@"float m = 1\.0f / [\d\.]+f;(\s*//{markerEscaped})?";

        var replacement = $"float m = 1.0f / {gammaStr}f; //{Marker}";

        // Check if pattern exists in content
        if (!Regex.IsMatch(content, pattern))
        {
            // Fallback: try original pattern without marker
            const string originalPattern = "float m = 1.0f / 2.4f;";
            if (!content.Contains(originalPattern))
                return content;

            return content.Replace(originalPattern, replacement);
        }

        return Regex.Replace(content, pattern, replacement);
    }
}
