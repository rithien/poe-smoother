using System.IO;

namespace PoeEditor.Core.Patchers;

/// <summary>
/// Patcher that disables fog effects by modifying shader files.
/// Targets fog.ffx, postprocessuber.hlsl, and related shaders.
/// </summary>
public class FogPatcher : BasePatcher
{
    public FogPatcher()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patchers", "fog.json");
        LoadConfig(configPath);
    }
}
