using System.IO;

namespace PoeEditor.Core.Patchers;

/// <summary>
/// Patcher that disables vignette (screen edge darkening) post-processing effect.
/// </summary>
public class VignettePatcher : BasePatcher
{
    public VignettePatcher()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patchers", "vignette.json");
        LoadConfig(configPath);
    }
}
