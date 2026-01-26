using System.IO;

namespace PoeEditor.Core.Patchers;

/// <summary>
/// Patcher that disables bloom post-processing effect.
/// </summary>
public class BloomPatcher : BasePatcher
{
    public BloomPatcher()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patchers", "bloom.json");
        LoadConfig(configPath);
    }
}
