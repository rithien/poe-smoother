using System.IO;

namespace PoeEditor.Core.Patchers;

/// <summary>
/// Patcher that reveals unexplored areas on the minimap.
/// Modifies minimap blending and visibility shaders.
/// </summary>
public class MapRevealPatcher : BasePatcher
{
    public MapRevealPatcher()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patchers", "mapreveal.json");
        LoadConfig(configPath);
    }
}
