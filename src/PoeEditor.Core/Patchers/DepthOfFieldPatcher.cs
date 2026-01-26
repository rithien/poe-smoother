using System.IO;

namespace PoeEditor.Core.Patchers;

/// <summary>
/// Patcher that disables Depth of Field effect.
/// </summary>
public class DepthOfFieldPatcher : BasePatcher
{
    public DepthOfFieldPatcher()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patchers", "depthoffield.json");
        LoadConfig(configPath);
    }
}
