using System.IO;

namespace PoeEditor.Core.Patchers;

/// <summary>
/// Patcher that disables Screen Space Ambient Occlusion (SSAO).
/// </summary>
public class SSAOPatcher : BasePatcher
{
    public SSAOPatcher()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patchers", "ssao.json");
        LoadConfig(configPath);
    }
}
