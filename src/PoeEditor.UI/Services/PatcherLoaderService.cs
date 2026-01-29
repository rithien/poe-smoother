using System.IO;
using System.Text.Json;
using PoeEditor.Core.Patchers;

namespace PoeEditor.UI.Services;

/// <summary>
/// Service for loading patcher configurations from external JSON files.
/// </summary>
public class PatcherLoaderService
{
    private readonly string _patchersDirectory;
    
    public PatcherLoaderService(string? patchersDirectory = null)
    {
        _patchersDirectory = patchersDirectory ?? GetDefaultPatchersDirectory();
    }
    
    private static string GetDefaultPatchersDirectory()
    {
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(exeDir, "patchers");
    }
    
    /// <summary>
    /// Loads all patcher configurations from the patchers directory.
    /// </summary>
    public List<BasePatcher> LoadAllPatchers()
    {
        var patchers = new List<BasePatcher>();
        
        if (!Directory.Exists(_patchersDirectory))
        {
            System.Diagnostics.Debug.WriteLine($"Patchers directory not found: {_patchersDirectory}");
            return patchers;
        }
        
        foreach (var jsonFile in Directory.GetFiles(_patchersDirectory, "*.json"))
        {
            try
            {
                var patcher = LoadPatcherFromFile(jsonFile);
                if (patcher != null)
                {
                    patchers.Add(patcher);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load patcher from {jsonFile}: {ex.Message}");
            }
        }
        
        return patchers;
    }
    
    /// <summary>
    /// Loads a single patcher configuration from a JSON file.
    /// </summary>
    public BasePatcher? LoadPatcherFromFile(string jsonFilePath)
    {
        if (!File.Exists(jsonFilePath))
            return null;
            
        var json = File.ReadAllText(jsonFilePath);
        var config = JsonSerializer.Deserialize<PatcherConfig>(json);
        
        if (config == null)
            return null;
            
        return new ExternalPatcher(config, jsonFilePath);
    }
}

/// <summary>
/// A patcher that loads its configuration from an external JSON file.
/// </summary>
public class ExternalPatcher : BasePatcher
{
    private readonly PatcherConfig _config;
    private readonly string _sourceFile;

    public ExternalPatcher(PatcherConfig config, string sourceFile)
    {
        _config = config;
        _sourceFile = sourceFile;
        LoadConfigFromJson(JsonSerializer.Serialize(config));
    }

    /// <summary>
    /// Returns the patcher name from config for better logging.
    /// This is used by BasePatcher.ApplyAsync for logging (GetType().Name would return "ExternalPatcher").
    /// </summary>
    public string PatcherIdentifier => !string.IsNullOrEmpty(_config.Name)
        ? _config.Name
        : Path.GetFileNameWithoutExtension(_sourceFile);

    public override string Name => PatcherIdentifier;
    public override string Category => _config.Category;
    public override int ImpactLevel => _config.ImpactLevel;
    public override string MarkerFile => _config.MarkerFile;
    public override string Marker => !string.IsNullOrEmpty(_config.Marker)
        ? _config.Marker
        : $"{{{{RITHIEN_{Path.GetFileNameWithoutExtension(_sourceFile).ToLowerInvariant()}}}}}";

    public string SourceFile => _sourceFile;
}
