using LibBundle3;
using PoeEditor.Core.Services;
using BundleIndex = LibBundle3.Index;

namespace PoeEditor.Core.Patchers;

/// <summary>
/// Result of a patching operation.
/// </summary>
public record PatchResult(bool Success, int FilesModified, string? ErrorMessage = null);

/// <summary>
/// Result of a pre-patch check.
/// </summary>
public record PrePatchCheckResult(bool CanProceed, List<DirtyStateResult> DirtyFiles, string? WarningMessage = null);

/// <summary>
/// Interface for all patcher modules.
/// </summary>
public interface IPatcher
{
    /// <summary>
    /// Display name of the patcher.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of what this patcher does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Whether this patcher is enabled by the user.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Category for grouping in UI (e.g., "Shaders", "Particles", "Effects").
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Estimated performance impact (1-10).
    /// </summary>
    int ImpactLevel { get; }

    /// <summary>
    /// File used to detect if patch is applied. Contains the marker when patched.
    /// </summary>
    string MarkerFile { get; }

    /// <summary>
    /// Marker comment used by this patcher to identify its modifications.
    /// Format: {{RITHIEN_patchername}}
    /// </summary>
    string Marker { get; }

    /// <summary>
    /// Set the backup service for persistent backups.
    /// </summary>
    void SetBackupService(IBackupService? backupService);

    /// <summary>
    /// Check for dirty state before applying patches.
    /// Returns warning if files have markers but no backup exists.
    /// </summary>
    Task<PrePatchCheckResult> CheckPrePatchStateAsync(BundleIndex index, CancellationToken ct = default);

    /// <summary>
    /// Check if this patcher's modifications are already present in the archive.
    /// Searches for the patcher's marker in target files.
    /// </summary>
    Task<bool> IsAppliedAsync(BundleIndex index, PatchContext? context = null, CancellationToken ct = default);

    /// <summary>
    /// Apply the patch to the archive.
    /// </summary>
    Task<PatchResult> ApplyAsync(BundleIndex index, IProgress<string>? progress = null, PatchContext? context = null, CancellationToken ct = default);

    /// <summary>
    /// Revert the patch (restore original files).
    /// </summary>
    Task<PatchResult> RevertAsync(BundleIndex index, IProgress<string>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Load configuration from JSON file.
    /// </summary>
    void LoadConfig(string configPath);
}
