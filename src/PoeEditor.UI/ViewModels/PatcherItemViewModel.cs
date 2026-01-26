using CommunityToolkit.Mvvm.ComponentModel;
using PoeEditor.Core.Patchers;

namespace PoeEditor.UI.ViewModels;

/// <summary>
/// ViewModel for a single patcher item in the UI.
/// </summary>
public partial class PatcherItemViewModel : ObservableObject
{
    private readonly IPatcher _patcher;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private bool _isApplied;

    [ObservableProperty]
    private bool _isFailed;

    /// <summary>
    /// True when marker exists but no backup - indicates dirty state (orange warning).
    /// </summary>
    [ObservableProperty]
    private bool _isDirty;
    
    public string Name => _patcher.Name;
    public string Description => _patcher.Description;
    public string Category => _patcher.Category;
    public int ImpactLevel => _patcher.ImpactLevel;
    
    public string ImpactBadge => ImpactLevel switch
    {
        >= 8 => "🔴 High",
        >= 5 => "🟡 Medium",
        _ => "🟢 Low"
    };
    
    public IPatcher Patcher => _patcher;
    
    public PatcherItemViewModel(IPatcher patcher)
    {
        _patcher = patcher;
        _isEnabled = false; // Default to unchecked
    }
    
    partial void OnIsEnabledChanged(bool value)
    {
        _patcher.IsEnabled = value;
    }
}
