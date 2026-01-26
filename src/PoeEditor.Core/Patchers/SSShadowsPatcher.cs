using System.IO;

namespace PoeEditor.Core.Patchers;

/// <summary>
/// Patcher that disables Screen Space Shadows.
/// </summary>
public class SSShadowsPatcher : BasePatcher
{
    public SSShadowsPatcher()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patchers", "ssshadows.json");
        LoadConfig(configPath);
    }
}
