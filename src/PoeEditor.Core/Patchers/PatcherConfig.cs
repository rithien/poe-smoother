using System.Text.Json.Serialization;

namespace PoeEditor.Core.Patchers;

/// <summary>
/// Configuration model for patcher JSON files.
/// </summary>
public class PatcherConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = "General";

    [JsonPropertyName("impactLevel")]
    public int ImpactLevel { get; set; } = 5;

    /// <summary>
    /// File used to detect if patch is applied. Must contain the marker when patched.
    /// Example: "metadata/characters/character.ot"
    /// </summary>
    [JsonPropertyName("markerFile")]
    public string MarkerFile { get; set; } = string.Empty;

    /// <summary>
    /// Marker pattern used to identify patched files. Format: {{RITHIEN_patchername}}
    /// Example: "{{RITHIEN_camerazoom}}"
    /// </summary>
    [JsonPropertyName("marker")]
    public string Marker { get; set; } = string.Empty;

    /// <summary>
    /// If true, patcher supports re-patching with different values (e.g., zoom level, brightness).
    /// If false, patcher is one-time only - skip if marker already present.
    /// Default: false (one-time patch)
    /// </summary>
    [JsonPropertyName("repatch")]
    public bool Repatch { get; set; } = false;

    [JsonPropertyName("targets")]
    public PatchTargets Targets { get; set; } = new();

    [JsonPropertyName("replacements")]
    public List<PatchReplacement> Replacements { get; set; } = new();
}

/// <summary>
/// Target files/extensions/paths for patching.
/// </summary>
public class PatchTargets
{
    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = new();
    
    [JsonPropertyName("extensions")]
    public List<string> Extensions { get; set; } = new();
    
    [JsonPropertyName("basePaths")]
    public List<string> BasePaths { get; set; } = new();
}

/// <summary>
/// A single text replacement operation.
/// </summary>
public class PatchReplacement
{
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = string.Empty;
    
    [JsonPropertyName("replacement")]
    public string Replacement { get; set; } = string.Empty;
    
    [JsonPropertyName("isRegex")]
    public bool IsRegex { get; set; } = false;
}
