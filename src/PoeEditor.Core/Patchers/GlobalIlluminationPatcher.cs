using System.Globalization;
using System.Text.RegularExpressions;
using LibBundle3.Records;
using BundleIndex = LibBundle3.Index;

namespace PoeEditor.Core.Patchers;

/// <summary>
/// Patcher that simplifies Global Illumination calculations with configurable values.
/// </summary>
public class GlobalIlluminationPatcher : BasePatcher
{
    private float _envLight = 0.15f;
    private float _indirectLight = 0.1f;

    public float EnvLight
    {
        get => _envLight;
        set => _envLight = Math.Clamp(value, 0f, 1f);
    }

    public float IndirectLight
    {
        get => _indirectLight;
        set => _indirectLight = Math.Clamp(value, 0f, 1f);
    }
    
    public GlobalIlluminationPatcher()
    {
        _envLight = 0.15f;
        _indirectLight = 0.1f;
        UpdateConfig();
    }
    
    public override string Marker => base.Marker;

    /// <summary>
    /// Override to refresh config with current slider values before applying patches.
    /// This ensures repatch uses updated EnvLight/IndirectLight values.
    /// </summary>
    public override async Task<PatchResult> ApplyAsync(
        BundleIndex index,
        IProgress<string>? progress = null,
        PatchContext? context = null,
        CancellationToken ct = default)
    {
        UpdateConfig();  // Refresh config with current slider values
        return await base.ApplyAsync(index, progress, context, ct);
    }

    private void UpdateConfig()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patchers", "globalillumination.json");
        if (File.Exists(configPath))
        {
            var envStr = _envLight.ToString("F2", CultureInfo.InvariantCulture);
            var indStr = _indirectLight.ToString("F2", CultureInfo.InvariantCulture);
            
            var json = File.ReadAllText(configPath);
            
            // We need to know the marker to escape it for regex replacement
            // Hack: Extract marker from JSON first? Or assume standard convention?
            // Since we haven't loaded config yet, we can't use base.Marker safely if it relies on loaded config.
            // But we can parse the JSON once to get the marker, or assume the placeholder {{MARKER}} resolves to what we want.
            // For the regex pattern in JSON, it expects {{MARKER_ESCAPED}}.
            
            // Let's rely on the fact the marker is {{RITHIEN_globalillumination}}.
            var marker = "{{RITHIEN_globalillumination}}";
            var markerEscaped = Regex.Escape(marker).Replace("{", "\\{").Replace("}", "\\}"); 
            // Note: Regex.Escape escapes { to \{ but in JSON string literal \\ matches backslash.
            // If JSON has "pattern": "... {{MARKER_ESCAPED}} ...", and we use parsed JSON string,
            // we just need to replace it with regex-safe string.
            // Since we are replacing into the JSON string content (before parse), we need to be careful about JSON escaping.
            // In JSON file: "pattern": "... \\{\\{ ..." implies literal curly braces in regex.
            
            // Simpler approach:
            // 1. Load basic struct to get marker? No, simpler to just assume the tag based on the file.
            // But to be proper, let's do replacement.
            
            json = json.Replace("{{ENV_LIGHT}}", envStr)
                       .Replace("{{INDIRECT_LIGHT}}", indStr)
                       .Replace("{{MARKER}}", marker)
                       // Verify this escaping logic
                       // If marker is {{A}}, Regex.Escape gives \{\{A\}\}
                       // For JSON string, we need \\{\\{A\\}\\} because \ must be escaped?
                       // The target pattern inside JSON file is likely using double backslashes already if it intends to be a regex.
                       // Actually, if I write to file using replace_file_content:
                       // "pattern": "... {{MARKER_ESCAPED}} ..."
                       // I should replace {{MARKER_ESCAPED}} with the regex-ready string.
                       // Regex.Escape("{{RITHIEN}}") -> "\{\{RITHIEN\}\}"
                       // When putting this into a JSON string value, backslashes must be escaped: "\\{\\{RITHIEN\\}\\}"
                       .Replace("{{MARKER_ESCAPED}}", Regex.Escape(marker).Replace("\\", "\\\\"));

            LoadConfigFromJson(json);
        }
    }

    /// <summary>
    /// Override to fix repatch bug: apply only the appropriate replacement based on patch state.
    /// - If file is already patched (has marker): use ONLY regex replacement to update values
    /// - If file is not patched: use ONLY non-regex replacement for initial patch
    /// </summary>
    protected override string ApplyReplacements(string content)
    {
        bool isAlreadyPatched = content.Contains(Marker, StringComparison.Ordinal);

        if (isAlreadyPatched)
        {
            // Use ONLY the regex replacement for repatch (updates values without duplication)
            var repatchReplacement = Config.Replacements.FirstOrDefault(r => r.IsRegex);
            if (repatchReplacement != null)
            {
                return Regex.Replace(content, repatchReplacement.Pattern, repatchReplacement.Replacement);
            }
            return content;
        }
        else
        {
            // Use ONLY the non-regex replacement for initial patch
            var initialReplacement = Config.Replacements.FirstOrDefault(r => !r.IsRegex);
            if (initialReplacement != null)
            {
                return content.Replace(initialReplacement.Pattern, initialReplacement.Replacement);
            }
            return content;
        }
    }
}
