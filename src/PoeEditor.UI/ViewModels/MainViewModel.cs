using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibBundle3.Records;
using PoeEditor.Core.Models;
using PoeEditor.Core.Patchers;
using PoeEditor.Core.Services;
using PoeEditor.UI.Services;

namespace PoeEditor.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IExtractionService _extractionService;
    private readonly ElasticsearchService _elasticsearchService;
    private readonly BackupService _backupService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _statusText = "Ready. Select a PoE archive file _index.bin or content.ggpk.";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private int _progressMax = 100;

    [ObservableProperty]
    private string _archivePath = "";

    [ObservableProperty]
    private string _outputPath = "";

    [ObservableProperty]
    private VirtualFileEntry? _selectedEntry;

    [ObservableProperty]
    private bool _defenderExclusionEnabled;

    // Elasticsearch settings
    [ObservableProperty]
    private string _elasticsearchUrl = "http://localhost:9200";

    [ObservableProperty]
    private string _elasticsearchUsername = "";

    [ObservableProperty]
    private string _elasticsearchPassword = "";

    [ObservableProperty]
    private bool _isElasticsearchConnected;

    [ObservableProperty]
    private string _elasticsearchIndexName = "poe-files";

    // Remembered paths
    [ObservableProperty]
    private string _lastFolderPath = "";

    public ObservableCollection<VirtualFileEntry> FileTree { get; } = [];
    public ObservableCollection<PatcherItemViewModel> Patchers { get; } = [];

    public MainViewModel() : this(new ExtractionService())
    {
    }

    public MainViewModel(IExtractionService extractionService)
    {
        _extractionService = extractionService;
        _elasticsearchService = new ElasticsearchService();

        // Initialize logging
        var logService = FileLogService.Instance;
        logService.CleanupOldLogs(7); // Keep logs for 7 days

        // Initialize backup service with logging
        _backupService = new BackupService(logService);

        // Set logger for all patchers
        PatcherLogger.SetLogService(logService);

        LoadSettings();
        InitializePatchers();

        logService.LogInfo($"MainViewModel initialized, backup dir: {_backupService.BackupDirectory}");
    }

    private void InitializePatchers()
    {
        // Load patchers from external JSON files
        var loaderService = new PatcherLoaderService();
        var externalPatchers = loaderService.LoadAllPatchers();

        foreach (var patcher in externalPatchers)
        {
            // Skip patchers with custom UI (sliders/radio buttons) in Visual Mods / Performance Mods / Particle Mods sections
            if (patcher.Name.Contains("Camera Zoom", StringComparison.OrdinalIgnoreCase) ||
                patcher.Name.Contains("Gamma", StringComparison.OrdinalIgnoreCase) ||
                patcher.Name.Contains("Brightness", StringComparison.OrdinalIgnoreCase) ||
                patcher.Name.Contains("SDR Scale", StringComparison.OrdinalIgnoreCase) ||
                patcher.Name.Contains("Global Illumination", StringComparison.OrdinalIgnoreCase) ||
                patcher.Name.Contains("Map Reveal", StringComparison.OrdinalIgnoreCase) ||
                patcher.Name.Contains("Vignette", StringComparison.OrdinalIgnoreCase) ||
                patcher.Name.Contains("Environmental Particles", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            else
            {
                Patchers.Add(new PatcherItemViewModel(patcher));
            }
        }

        // Initialize GI patcher (has dedicated UI with sliders, not in Patchers collection)
        _giPatcher = new GlobalIlluminationPatcher();

        // Camera Zoom patchers - only one should be enabled at a time
        _zoomPatcher1x = new CameraZoomPatcher(1) { IsEnabled = false };
        _zoomPatcher2x = new CameraZoomPatcher(2) { IsEnabled = false };
        _zoomPatcher3x = new CameraZoomPatcher(3) { IsEnabled = false };

        // Brightness patchers - only one should be enabled at a time
        _brightnessPatcher125 = new BrightnessPatcher(1.25f) { IsEnabled = false };
        _brightnessPatcher150 = new BrightnessPatcher(1.50f) { IsEnabled = false };
        _brightnessPatcher175 = new BrightnessPatcher(1.75f) { IsEnabled = false };

        // SDR Scale patchers - only one should be enabled at a time
        _sdrScalePatcher125 = new SdrScalePatcher(1.25f) { IsEnabled = false };
        _sdrScalePatcher150 = new SdrScalePatcher(1.50f) { IsEnabled = false };
        _sdrScalePatcher175 = new SdrScalePatcher(1.75f) { IsEnabled = false };

        // Gamma patchers - only one should be enabled at a time
        _gammaPatcher20 = new GammaPatcher(2.0f) { IsEnabled = false };
        _gammaPatcher18 = new GammaPatcher(1.8f) { IsEnabled = false };
        _gammaPatcher16 = new GammaPatcher(1.6f) { IsEnabled = false };

        // Map Reveal patcher
        _mapRevealPatcher = new MapRevealPatcher() { IsEnabled = false };

        // Vignette patcher
        _vignettePatcher = new VignettePatcher() { IsEnabled = false };

        // Environmental Particles patcher
        _envParticlesPatcher = new EnvironmentalParticlesPatcher() { IsEnabled = false };
    }

    private GlobalIlluminationPatcher? _giPatcher;
    private CameraZoomPatcher? _zoomPatcher1x;
    private CameraZoomPatcher? _zoomPatcher2x;
    private CameraZoomPatcher? _zoomPatcher3x;

    [ObservableProperty]
    private int _selectedZoomLevel = 0; // 0 = disabled, 1 = x1, 2 = x2, 3 = x3

    [ObservableProperty]
    private float _giEnvLight = 0.15f;

    [ObservableProperty]
    private float _giIndirectLight = 0.1f;

    [ObservableProperty]
    private bool _giEnabled = false;

    [ObservableProperty]
    private bool _isGiApplied;

    [ObservableProperty]
    private bool _isGiDirty;

    partial void OnGiEnvLightChanged(float value)
    {
        if (_giPatcher != null) _giPatcher.EnvLight = value;
    }

    partial void OnGiIndirectLightChanged(float value)
    {
        if (_giPatcher != null) _giPatcher.IndirectLight = value;
    }

    partial void OnGiEnabledChanged(bool value)
    {
        if (_giPatcher != null) _giPatcher.IsEnabled = value;
    }

    partial void OnMapRevealEnabledChanged(bool value)
    {
        if (_mapRevealPatcher != null) _mapRevealPatcher.IsEnabled = value;
    }

    partial void OnVignetteEnabledChanged(bool value)
    {
        if (_vignettePatcher != null) _vignettePatcher.IsEnabled = value;
    }

    partial void OnEnvParticlesEnabledChanged(bool value)
    {
        if (_envParticlesPatcher != null) _envParticlesPatcher.IsEnabled = value;
    }

    public CameraZoomPatcher? GetActiveZoomPatcher()
    {
        return SelectedZoomLevel switch
        {
            1 => _zoomPatcher1x,
            2 => _zoomPatcher2x,
            3 => _zoomPatcher3x,
            _ => null
        };
    }

    [ObservableProperty]
    private int _selectedBrightnessLevel = 0; // 0=disabled, 1=1.25, 2=1.50, 3=1.75

    private BrightnessPatcher? _brightnessPatcher125;
    private BrightnessPatcher? _brightnessPatcher150;
    private BrightnessPatcher? _brightnessPatcher175;

    // SDR Scale patchers
    private SdrScalePatcher? _sdrScalePatcher125;
    private SdrScalePatcher? _sdrScalePatcher150;
    private SdrScalePatcher? _sdrScalePatcher175;

    [ObservableProperty]
    private int _selectedSdrScaleLevel = 0; // 0=disabled, 1=1.25, 2=1.50, 3=1.75

    // Gamma patchers
    private GammaPatcher? _gammaPatcher20;
    private GammaPatcher? _gammaPatcher18;
    private GammaPatcher? _gammaPatcher16;

    [ObservableProperty]
    private int _selectedGammaLevel = 0; // 0=disabled, 1=2.0, 2=1.8, 3=1.6

    // Map Reveal patcher
    private MapRevealPatcher? _mapRevealPatcher;

    [ObservableProperty]
    private bool _mapRevealEnabled = false;

    // Vignette patcher
    private VignettePatcher? _vignettePatcher;

    [ObservableProperty]
    private bool _vignetteEnabled = false;

    // Environmental Particles patcher
    private EnvironmentalParticlesPatcher? _envParticlesPatcher;

    [ObservableProperty]
    private bool _envParticlesEnabled = false;

    // ===== SELECT ALL PROPERTIES =====

    [ObservableProperty]
    private bool _selectAllVisualMods = false;

    [ObservableProperty]
    private bool _selectAllPerformanceMods = false;

    [ObservableProperty]
    private bool _selectAllParticleMods = false;

    partial void OnSelectAllVisualModsChanged(bool value)
    {
        if (value)
        {
            // Set all visual mods to MAX values
            SelectedZoomLevel = 3;        // x3 (Max Zoom Out)
            SelectedBrightnessLevel = 3;  // x1.75 (+75%)
            SelectedSdrScaleLevel = 3;    // x1.75 (+75%)
            SelectedGammaLevel = 3;       // 1.6 (Very Bright)
            MapRevealEnabled = true;
            VignetteEnabled = true;
        }
        else
        {
            // Disable all visual mods
            SelectedZoomLevel = 0;
            SelectedBrightnessLevel = 0;
            SelectedSdrScaleLevel = 0;
            SelectedGammaLevel = 0;
            MapRevealEnabled = false;
            VignetteEnabled = false;
        }
    }

    partial void OnSelectAllPerformanceModsChanged(bool value)
    {
        if (value)
        {
            // Enable GI with brightest settings (max values)
            GiEnabled = true;
            GiEnvLight = 1f;
            GiIndirectLight = 1f;

            // Enable all patchers from ItemsControl
            foreach (var patcher in Patchers)
            {
                patcher.IsEnabled = true;
            }
        }
        else
        {
            // Disable all performance mods
            GiEnabled = false;
            GiEnvLight = 0.15f;  // Reset to defaults
            GiIndirectLight = 0.1f;

            foreach (var patcher in Patchers)
            {
                patcher.IsEnabled = false;
            }
        }
    }

    partial void OnSelectAllParticleModsChanged(bool value)
    {
        // Enable/disable all particle mods
        EnvParticlesEnabled = value;
        // Future particle patchers will be added here:
        // TorchFlamesEnabled = value;
        // SimplifySpellsEnabled = value;
        // SimplifyMonstersEnabled = value;
        // DecorativeBloodEnabled = value;
    }

    public BrightnessPatcher? GetActiveBrightnessPatcher()
    {
        return SelectedBrightnessLevel switch
        {
            1 => _brightnessPatcher125,
            2 => _brightnessPatcher150,
            3 => _brightnessPatcher175,
            _ => null
        };
    }

    public SdrScalePatcher? GetActiveSdrScalePatcher()
    {
        return SelectedSdrScaleLevel switch
        {
            1 => _sdrScalePatcher125,
            2 => _sdrScalePatcher150,
            3 => _sdrScalePatcher175,
            _ => null
        };
    }

    public GammaPatcher? GetActiveGammaPatcher()
    {
        return SelectedGammaLevel switch
        {
            1 => _gammaPatcher20,
            2 => _gammaPatcher18,
            3 => _gammaPatcher16,
            _ => null
        };
    }

    // Status properties for visual patchers
    [ObservableProperty]
    private bool _isBrightnessApplied;

    [ObservableProperty]
    private bool _isBrightnessDirty;

    [ObservableProperty]
    private bool _isSdrScaleApplied;

    [ObservableProperty]
    private bool _isSdrScaleDirty;

    [ObservableProperty]
    private bool _isGammaApplied;

    [ObservableProperty]
    private bool _isGammaDirty;

    // Map Reveal status
    [ObservableProperty]
    private bool _isMapRevealApplied;

    [ObservableProperty]
    private bool _isMapRevealDirty;

    // Vignette status
    [ObservableProperty]
    private bool _isVignetteApplied;

    [ObservableProperty]
    private bool _isVignetteDirty;

    // Environmental Particles status
    [ObservableProperty]
    private bool _isEnvParticlesApplied;

    [ObservableProperty]
    private bool _isEnvParticlesDirty;

    private void LoadSettings()
    {
        var settings = SettingsService.Load();

        ElasticsearchUrl = settings.ElasticsearchUrl;
        ElasticsearchIndexName = settings.ElasticsearchIndexName;
        ElasticsearchUsername = settings.ElasticsearchUsername;
        ElasticsearchPassword = settings.ElasticsearchPassword;

        ArchivePath = settings.LastArchivePath;
        LastFolderPath = settings.LastFolderPath;
        OutputPath = settings.LastOutputPath;
    }

    private void SaveSettings()
    {
        var settings = new AppSettings
        {
            ElasticsearchUrl = ElasticsearchUrl,
            ElasticsearchIndexName = ElasticsearchIndexName,
            ElasticsearchUsername = ElasticsearchUsername,
            ElasticsearchPassword = ElasticsearchPassword,

            LastArchivePath = ArchivePath,
            LastFolderPath = LastFolderPath,
            LastOutputPath = OutputPath
        };

        SettingsService.Save(settings);
    }

    /// <summary>
    /// Reads file content from the virtual archive without extracting to disk.
    /// </summary>
    public async Task<ReadOnlyMemory<byte>> ReadFileContentAsync(string virtualPath, CancellationToken cancellationToken = default)
    {
        if (!_extractionService.IsOpen)
            throw new InvalidOperationException("No archive is open.");

        return await _extractionService.ReadFileContentAsync(virtualPath, cancellationToken);
    }

    /// <summary>
    /// Writes file content directly to the virtual archive.
    /// </summary>
    public async Task WriteFileContentAsync(string virtualPath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        if (!_extractionService.IsOpen)
            throw new InvalidOperationException("No archive is open.");

        await _extractionService.WriteFileContentAsync(virtualPath, content, cancellationToken);
    }

    [RelayCommand]
    private void ToggleDefenderExclusion()
    {
        if (string.IsNullOrEmpty(OutputPath))
        {
            MessageBox.Show(
                "Please select an output folder first by starting an extraction.",
                "No Output Folder",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!DefenderExclusionEnabled)
        {
            // Add exclusion
            var result = MessageBox.Show(
                $"This will add the following folder to Windows Defender exclusions:\n\n{OutputPath}\n\nThis requires administrator privileges. Continue?",
                "Add Defender Exclusion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (DefenderExclusionService.AddExclusion(OutputPath))
                {
                    DefenderExclusionEnabled = true;
                    StatusText = $"Defender exclusion added for: {OutputPath}";
                }
                else
                {
                    StatusText = "Failed to add Defender exclusion. UAC may have been cancelled.";
                }
            }
        }
        else
        {
            // Remove exclusion
            var result = MessageBox.Show(
                $"Remove Windows Defender exclusion for:\n\n{OutputPath}?",
                "Remove Defender Exclusion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (DefenderExclusionService.RemoveExclusion(OutputPath))
                {
                    DefenderExclusionEnabled = false;
                    StatusText = $"Defender exclusion removed for: {OutputPath}";
                }
                else
                {
                    StatusText = "Failed to remove Defender exclusion.";
                }
            }
        }
    }

    partial void OnOutputPathChanged(string value)
    {
        // Check if the new output path is already excluded
        if (!string.IsNullOrEmpty(value))
        {
            DefenderExclusionEnabled = DefenderExclusionService.IsExcluded(value);
        }
    }

    [RelayCommand]
    private async Task OpenArchiveAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "PoE Archives (*.ggpk;*.bin)|*.ggpk;*.bin|All Files (*.*)|*.*",
            Title = "Select PoE Archive File",
            InitialDirectory = !string.IsNullOrEmpty(ArchivePath)
                ? System.IO.Path.GetDirectoryName(ArchivePath)
                : ""
        };

        if (dialog.ShowDialog() == true)
        {
            await LoadArchiveAsync(dialog.FileName);
            SaveSettings();
        }
    }

    [RelayCommand]
    private void CloseArchive()
    {
        if (!_extractionService.IsOpen)
        {
            StatusText = "No archive is currently open.";
            return;
        }

        _extractionService.Close();
        FileTree.Clear();
        ArchivePath = "";
        SelectedEntry = null;
        StatusText = "Archive closed. Ready to open a new file.";
    }

    private async Task LoadArchiveAsync(string path)
    {
        try
        {
            IsLoading = true;
            StatusText = $"Loading archive: {path}...";
            FileTree.Clear();

            var success = await _extractionService.OpenArchiveAsync(path);

            if (success)
            {
                ArchivePath = _extractionService.ArchivePath ?? path;
                StatusText = $"Loaded: {_extractionService.ArchiveType} archive";

                // Load root entries
                var entries = _extractionService.GetFileList().ToList();
                foreach (var entry in entries.OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name))
                {
                    FileTree.Add(entry);
                }

                // Check patch status for all patchers
                await CheckPatchStatusAsync();

                StatusText = $"Loaded {entries.Count} root entries from {_extractionService.ArchiveType} archive";
            }
            else
            {
                StatusText = "Failed to open archive. Make sure the file is valid.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Checks if patches are already applied by looking for markers in markerFiles.
    /// Updates IsApplied and IsDirty status for each patcher.
    /// According to CLAUDE.md:
    /// - Green (IsApplied): marker exists AND backup exists
    /// - Orange (IsDirty): marker exists but NO backup
    /// - No status: no marker
    /// </summary>
    [ObservableProperty]
    private bool _isZoomApplied;

    [ObservableProperty]
    private bool _isZoomDirty;

    private async Task CheckPatchStatusAsync()
    {
        var index = GetUnderlyingIndex();
        if (index == null) return;

        StatusText = "Checking patch status...";

        // Initialize backup service
        await _backupService.InitializeAsync();

        // Set backup service for all patchers
        foreach (var patcherVm in Patchers)
        {
            patcherVm.Patcher.SetBackupService(_backupService);
        }

        // Check and cleanup stale backups (if no markers found but backups exist)
        await CheckAndCleanupBackupsAsync(index);

        // Check external patchers
        foreach (var patcherVm in Patchers)
        {
            try
            {
                var isApplied = await patcherVm.Patcher.IsAppliedAsync(index);

                if (isApplied)
                {
                    // Check if backup exists
                    var markerFile = patcherVm.Patcher.MarkerFile;
                    var hasBackup = !string.IsNullOrEmpty(markerFile) && _backupService.HasBackup(markerFile);

                    if (hasBackup)
                    {
                        // Green: marker + backup
                        patcherVm.IsApplied = true;
                        patcherVm.IsDirty = false;
                    }
                    else
                    {
                        // Orange: marker but no backup
                        patcherVm.IsApplied = false;
                        patcherVm.IsDirty = true;
                    }
                }
                else
                {
                    patcherVm.IsApplied = false;
                    patcherVm.IsDirty = false;
                }

                patcherVm.IsFailed = false;
            }
            catch
            {
                patcherVm.IsApplied = false;
                patcherVm.IsDirty = false;
            }
        }

        // Set backup service for zoom patchers
        _zoomPatcher1x?.SetBackupService(_backupService);
        _zoomPatcher2x?.SetBackupService(_backupService);
        _zoomPatcher3x?.SetBackupService(_backupService);

        // Set backup service for brightness patchers
        _brightnessPatcher125?.SetBackupService(_backupService);
        _brightnessPatcher150?.SetBackupService(_backupService);
        _brightnessPatcher175?.SetBackupService(_backupService);

        // Set backup service for SDR scale patchers
        _sdrScalePatcher125?.SetBackupService(_backupService);
        _sdrScalePatcher150?.SetBackupService(_backupService);
        _sdrScalePatcher175?.SetBackupService(_backupService);

        // Set backup service for gamma patchers
        _gammaPatcher20?.SetBackupService(_backupService);
        _gammaPatcher18?.SetBackupService(_backupService);
        _gammaPatcher16?.SetBackupService(_backupService);

        // Set backup service for Map Reveal patcher
        _mapRevealPatcher?.SetBackupService(_backupService);

        // Set backup service for Vignette patcher
        _vignettePatcher?.SetBackupService(_backupService);

        // Set backup service for Environmental Particles patcher
        _envParticlesPatcher?.SetBackupService(_backupService);

        // Set backup service for GI patcher
        _giPatcher?.SetBackupService(_backupService);

        // Check CameraZoom patcher status
        // Use _zoomPatcher2x as reference since they share the same marker logic
        if (_zoomPatcher2x != null)
        {
            try
            {
                var appliedLevel = await _zoomPatcher2x.GetAppliedZoomLevelAsync(index);

                if (appliedLevel > 0)
                {
                    SelectedZoomLevel = appliedLevel;

                    var markerFile = _zoomPatcher2x.MarkerFile;
                    var hasBackup = !string.IsNullOrEmpty(markerFile) && _backupService.HasBackup(markerFile);

                    if (hasBackup)
                    {
                        IsZoomApplied = true;
                        IsZoomDirty = false;
                    }
                    else
                    {
                        IsZoomApplied = false;
                        IsZoomDirty = true;
                    }
                }
                else
                {
                    IsZoomApplied = false;
                    IsZoomDirty = false;
                    // Keep SelectedZoomLevel as is (or reset to 0? defaults to keeping selection)
                }
            }
            catch
            {
                IsZoomApplied = false;
                IsZoomDirty = false;
            }
        }

        // Check Brightness patcher status
        if (_brightnessPatcher125 != null)
        {
            try
            {
                var isApplied = await _brightnessPatcher125.IsAppliedAsync(index);
                if (isApplied)
                {
                    var markerFile = _brightnessPatcher125.MarkerFile;
                    var hasBackup = !string.IsNullOrEmpty(markerFile) && _backupService.HasBackup(markerFile);

                    if (hasBackup)
                    {
                        IsBrightnessApplied = true;
                        IsBrightnessDirty = false;
                    }
                    else
                    {
                        IsBrightnessApplied = false;
                        IsBrightnessDirty = true;
                    }
                }
                else
                {
                    IsBrightnessApplied = false;
                    IsBrightnessDirty = false;
                }
            }
            catch
            {
                IsBrightnessApplied = false;
                IsBrightnessDirty = false;
            }
        }

        // Check SDR Scale patcher status
        if (_sdrScalePatcher125 != null)
        {
            try
            {
                var isApplied = await _sdrScalePatcher125.IsAppliedAsync(index);
                if (isApplied)
                {
                    var markerFile = "shaders/include/oetf.hlsl";
                    var hasBackup = _backupService.HasBackup(markerFile);

                    if (hasBackup)
                    {
                        IsSdrScaleApplied = true;
                        IsSdrScaleDirty = false;
                    }
                    else
                    {
                        IsSdrScaleApplied = false;
                        IsSdrScaleDirty = true;
                    }
                }
                else
                {
                    IsSdrScaleApplied = false;
                    IsSdrScaleDirty = false;
                }
            }
            catch
            {
                IsSdrScaleApplied = false;
                IsSdrScaleDirty = false;
            }
        }

        // Check Gamma patcher status
        if (_gammaPatcher20 != null)
        {
            try
            {
                var isApplied = await _gammaPatcher20.IsAppliedAsync(index);
                if (isApplied)
                {
                    var markerFile = "shaders/include/oetf.hlsl";
                    var hasBackup = _backupService.HasBackup(markerFile);

                    if (hasBackup)
                    {
                        IsGammaApplied = true;
                        IsGammaDirty = false;
                    }
                    else
                    {
                        IsGammaApplied = false;
                        IsGammaDirty = true;
                    }
                }
                else
                {
                    IsGammaApplied = false;
                    IsGammaDirty = false;
                }
            }
            catch
            {
                IsGammaApplied = false;
                IsGammaDirty = false;
            }
        }

        // Check Map Reveal patcher status
        if (_mapRevealPatcher != null)
        {
            try
            {
                var isApplied = await _mapRevealPatcher.IsAppliedAsync(index);
                if (isApplied)
                {
                    var markerFile = _mapRevealPatcher.MarkerFile;
                    var hasBackup = !string.IsNullOrEmpty(markerFile) && _backupService.HasBackup(markerFile);

                    if (hasBackup)
                    {
                        IsMapRevealApplied = true;
                        IsMapRevealDirty = false;
                        MapRevealEnabled = true; // Auto-check the checkbox if already applied
                    }
                    else
                    {
                        IsMapRevealApplied = false;
                        IsMapRevealDirty = true;
                    }
                }
                else
                {
                    IsMapRevealApplied = false;
                    IsMapRevealDirty = false;
                }
            }
            catch
            {
                IsMapRevealApplied = false;
                IsMapRevealDirty = false;
            }
        }

        // Check Vignette patcher status
        if (_vignettePatcher != null)
        {
            try
            {
                var isApplied = await _vignettePatcher.IsAppliedAsync(index);
                if (isApplied)
                {
                    var markerFile = _vignettePatcher.MarkerFile;
                    var hasBackup = !string.IsNullOrEmpty(markerFile) && _backupService.HasBackup(markerFile);

                    if (hasBackup)
                    {
                        IsVignetteApplied = true;
                        IsVignetteDirty = false;
                        VignetteEnabled = true; // Auto-check the checkbox if already applied
                    }
                    else
                    {
                        IsVignetteApplied = false;
                        IsVignetteDirty = true;
                    }
                }
                else
                {
                    IsVignetteApplied = false;
                    IsVignetteDirty = false;
                }
            }
            catch
            {
                IsVignetteApplied = false;
                IsVignetteDirty = false;
            }
        }

        // Check Environmental Particles patcher status
        if (_envParticlesPatcher != null)
        {
            try
            {
                var isApplied = await _envParticlesPatcher.IsAppliedAsync(index);
                if (isApplied)
                {
                    var markerFile = _envParticlesPatcher.MarkerFile;
                    var hasBackup = !string.IsNullOrEmpty(markerFile) && _backupService.HasBackup(markerFile);

                    if (hasBackup)
                    {
                        IsEnvParticlesApplied = true;
                        IsEnvParticlesDirty = false;
                        EnvParticlesEnabled = true; // Auto-check the checkbox if already applied
                    }
                    else
                    {
                        IsEnvParticlesApplied = false;
                        IsEnvParticlesDirty = true;
                    }
                }
                else
                {
                    IsEnvParticlesApplied = false;
                    IsEnvParticlesDirty = false;
                }
            }
            catch
            {
                IsEnvParticlesApplied = false;
                IsEnvParticlesDirty = false;
            }
        }

        // Check GI patcher status
        if (_giPatcher != null)
        {
            try
            {
                var isApplied = await _giPatcher.IsAppliedAsync(index);

                if (isApplied)
                {
                    var markerFile = _giPatcher.MarkerFile;
                    var hasBackup = !string.IsNullOrEmpty(markerFile) && _backupService.HasBackup(markerFile);

                    if (hasBackup)
                    {
                        IsGiApplied = true;
                        IsGiDirty = false;
                    }
                    else
                    {
                        IsGiApplied = false;
                        IsGiDirty = true;
                    }
                }
                else
                {
                    IsGiApplied = false;
                    IsGiDirty = false;
                }
            }
            catch
            {
                IsGiApplied = false;
                IsGiDirty = false;
            }
        }
    }

    /// <summary>
    /// Check if backups exist but no markers found in files - indicates clean game files.
    /// In this case, clear all stale backups.
    /// </summary>
    private async Task CheckAndCleanupBackupsAsync(LibBundle3.Index index)
    {
        // If no backups exist, nothing to clean up
        if (!_backupService.HasAnyBackups())
            return;

        // Check if any patcher has markers in the files
        bool anyMarkerFound = false;

        foreach (var patcherVm in Patchers)
        {
            if (await patcherVm.Patcher.IsAppliedAsync(index))
            {
                anyMarkerFound = true;
                break;
            }
        }

        // Also check special patchers
        if (!anyMarkerFound && _zoomPatcher2x != null && await _zoomPatcher2x.IsAppliedAsync(index))
            anyMarkerFound = true;
        if (!anyMarkerFound && _brightnessPatcher125 != null && await _brightnessPatcher125.IsAppliedAsync(index))
            anyMarkerFound = true;
        if (!anyMarkerFound && _gammaPatcher20 != null && await _gammaPatcher20.IsAppliedAsync(index))
            anyMarkerFound = true;
        if (!anyMarkerFound && _sdrScalePatcher125 != null && await _sdrScalePatcher125.IsAppliedAsync(index))
            anyMarkerFound = true;
        if (!anyMarkerFound && _mapRevealPatcher != null && await _mapRevealPatcher.IsAppliedAsync(index))
            anyMarkerFound = true;
        if (!anyMarkerFound && _vignettePatcher != null && await _vignettePatcher.IsAppliedAsync(index))
            anyMarkerFound = true;
        if (!anyMarkerFound && _envParticlesPatcher != null && await _envParticlesPatcher.IsAppliedAsync(index))
            anyMarkerFound = true;
        if (!anyMarkerFound && _giPatcher != null && await _giPatcher.IsAppliedAsync(index))
            anyMarkerFound = true;

        // If no markers found but backups exist - game files were restored externally (e.g., Steam verify)
        // Clear stale backups
        if (!anyMarkerFound)
        {
            await _backupService.ClearAllBackupsAsync();
            StatusText = "Stale backups cleared (clean game files detected)";
        }
    }

    /// <summary>
    /// Restore all backed up files at once.
    /// This will restore ALL files to their original state, removing all patches.
    /// </summary>
    public async Task RestoreAllAsync()
    {
        var index = GetUnderlyingIndex();
        if (index == null)
        {
            StatusText = "No archive loaded";
            return;
        }

        var backupPaths = _backupService.GetAllBackupPaths().ToList();
        if (backupPaths.Count == 0)
        {
            StatusText = "No backups to restore";
            return;
        }

        StatusText = $"Restoring {backupPaths.Count} files...";
        var restoredCount = 0;
        var restoredPaths = new List<string>();

        // Phase 1: Restore all files to memory
        foreach (var virtualPath in backupPaths)
        {
            var data = await _backupService.GetBackupAsync(virtualPath);
            if (data != null)
            {
                var file = index.Files.Values.FirstOrDefault(f =>
                    f.Path?.Equals(virtualPath, StringComparison.OrdinalIgnoreCase) == true);

                if (file != null)
                {
                    file.Write(data);
                    restoredCount++;
                    restoredPaths.Add(virtualPath);
                }
            }
        }

        // Phase 2: Save changes to disk BEFORE removing backups
        // This ensures backups remain if save fails
        if (restoredCount > 0)
        {
            StatusText = "Saving restored files to archive...";
            await Task.Run(() => index.Save());
        }

        // Phase 3: Remove backups only after successful save
        foreach (var virtualPath in restoredPaths)
        {
            await _backupService.RemoveBackupAsync(virtualPath);
        }

        StatusText = $"Restored {restoredCount} files to original state";

        // Refresh all patcher statuses
        await CheckPatchStatusAsync();
    }

    [RelayCommand]
    private async Task ExtractSelectedAsync()
    {
        if (SelectedEntry == null)
        {
            StatusText = "Please select a file or folder to extract.";
            return;
        }

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Output Folder",
            InitialDirectory = !string.IsNullOrEmpty(OutputPath) ? OutputPath : ""
        };

        if (dialog.ShowDialog() != true)
            return;

        OutputPath = dialog.FolderName;
        SaveSettings();

        try
        {
            IsLoading = true;
            _cancellationTokenSource = new CancellationTokenSource();

            var virtualPaths = GetVirtualPaths(SelectedEntry);
            var pathList = virtualPaths.ToList();
            ProgressMax = pathList.Count;
            ProgressValue = 0;

            var progress = new Progress<ExtractionProgress>(p =>
            {
                ProgressValue = p.CurrentFile;
                StatusText = $"Extracting ({p.CurrentFile}/{p.TotalFiles}): {p.FileName}";
            });

            await _extractionService.ExtractFilesAsync(
                pathList,
                OutputPath,
                progress,
                _cancellationTokenSource.Token);

            StatusText = $"Extracted {pathList.Count} files to {OutputPath}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Extraction cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand]
    private async Task ExtractAllAsync()
    {
        if (!_extractionService.IsOpen)
        {
            StatusText = "Please open an archive first.";
            return;
        }

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Output Folder for All Files",
            InitialDirectory = !string.IsNullOrEmpty(OutputPath) ? OutputPath : ""
        };

        if (dialog.ShowDialog() != true)
            return;

        OutputPath = dialog.FolderName;
        SaveSettings();

        try
        {
            IsLoading = true;
            _cancellationTokenSource = new CancellationTokenSource();

            var files = _extractionService.GetAllFiles().ToList();
            ProgressMax = files.Count;
            ProgressValue = 0;

            var progress = new Progress<ExtractionProgress>(p =>
            {
                ProgressValue = p.CurrentFile;
                StatusText = $"Extracting ({p.CurrentFile}/{p.TotalFiles}): {p.FileName}";
            });

            await _extractionService.ExtractAllAsync(
                OutputPath,
                progress,
                _cancellationTokenSource.Token);

            StatusText = $"Extracted {files.Count} files to {OutputPath}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Extraction cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand]
    private void CancelExtraction()
    {
        _cancellationTokenSource?.Cancel();
    }

    private static IEnumerable<string> GetVirtualPaths(VirtualFileEntry entry)
    {
        if (!entry.IsDirectory)
        {
            yield return entry.FullPath;
        }
        else
        {
            foreach (var child in entry.Children)
            {
                foreach (var path in GetVirtualPaths(child))
                {
                    yield return path;
                }
            }
        }
    }

    [RelayCommand]
    private async Task ConnectToElasticsearchAsync()
    {
        try
        {
            StatusText = $"Connecting to Elasticsearch at {ElasticsearchUrl}...";

            _elasticsearchService.IndexName = ElasticsearchIndexName;

            var connected = await _elasticsearchService.ConnectAsync(
                ElasticsearchUrl,
                string.IsNullOrEmpty(ElasticsearchUsername) ? null : ElasticsearchUsername,
                string.IsNullOrEmpty(ElasticsearchPassword) ? null : ElasticsearchPassword);

            if (connected)
            {
                IsElasticsearchConnected = true;
                await _elasticsearchService.EnsureIndexExistsAsync();
                var count = await _elasticsearchService.GetDocumentCountAsync();
                StatusText = $"Connected to Elasticsearch. Index '{ElasticsearchIndexName}' has {count:N0} documents.";
                SaveSettings(); // Save connection settings
            }
            else
            {
                IsElasticsearchConnected = false;
                StatusText = "Failed to connect to Elasticsearch. Check URL and credentials.";
            }
        }
        catch (Exception ex)
        {
            IsElasticsearchConnected = false;
            StatusText = $"Elasticsearch error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task IndexToElasticsearchAsync()
    {
        if (!_extractionService.IsOpen)
        {
            StatusText = "Please open an archive first.";
            return;
        }

        if (!IsElasticsearchConnected)
        {
            StatusText = "Please connect to Elasticsearch first.";
            return;
        }

        try
        {
            IsLoading = true;
            _cancellationTokenSource = new CancellationTokenSource();

            // Get all files from the extraction service
            var files = GetAllFileRecords().ToList();
            var indexableCount = ElasticsearchService.GetIndexableFileCount(files);

            var result = MessageBox.Show(
                $"Found {indexableCount:N0} text files to index out of {files.Count:N0} total files.\n\n" +
                $"Supported extensions:\n{string.Join(", ", ElasticsearchService.SupportedExtensions)}\n\n" +
                $"Index to '{ElasticsearchIndexName}'?",
                "Index to Elasticsearch",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                StatusText = "Indexing cancelled.";
                return;
            }

            ProgressMax = indexableCount;
            ProgressValue = 0;

            var progress = new Progress<ExtractionProgress>(p =>
            {
                ProgressValue = p.CurrentFile;
                StatusText = $"Indexing ({p.CurrentFile}/{p.TotalFiles}): {p.FileName}";
            });

            await _elasticsearchService.IndexFilesAsync(
                files,
                _extractionService.ArchivePath ?? "unknown",
                progress,
                _cancellationTokenSource.Token);

            var totalCount = await _elasticsearchService.GetDocumentCountAsync();
            StatusText = $"Indexed {indexableCount:N0} files. Total documents in index: {totalCount:N0}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Indexing cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Indexing error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    /// <summary>
    /// Gets all FileRecords from the extraction service's index.
    /// </summary>
    private IEnumerable<FileRecord> GetAllFileRecords()
    {
        var activeIndex = _extractionService.ActiveIndex;
        if (activeIndex != null)
        {
            return activeIndex.Files.Values.Where(f => f.Path != null);
        }

        return [];
    }

    /// <summary>
    /// Gets FileRecords by their virtual paths.
    /// </summary>
    private IEnumerable<FileRecord> GetFileRecordsByPaths(IEnumerable<string> paths)
    {
        var pathSet = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return GetAllFileRecords().Where(f => f.Path != null && pathSet.Contains(f.Path));
    }

    [RelayCommand]
    private async Task IndexSelectedToElasticsearchAsync()
    {
        if (!_extractionService.IsOpen)
        {
            StatusText = "Please open an archive first.";
            return;
        }

        if (!IsElasticsearchConnected)
        {
            StatusText = "Please connect to Elasticsearch first.";
            return;
        }

        if (SelectedEntry == null)
        {
            StatusText = "Please select a file or folder to index.";
            return;
        }

        try
        {
            IsLoading = true;
            _cancellationTokenSource = new CancellationTokenSource();

            // Get paths for selected entry
            var virtualPaths = GetVirtualPaths(SelectedEntry).ToList();
            var files = GetFileRecordsByPaths(virtualPaths).ToList();
            var indexableCount = ElasticsearchService.GetIndexableFileCount(files);

            if (indexableCount == 0)
            {
                StatusText = "No indexable files found in selection. Check supported extensions.";
                return;
            }

            var result = MessageBox.Show(
                $"Found {indexableCount:N0} text files to index from '{SelectedEntry.Name}'.\n\n" +
                $"Total files in selection: {files.Count:N0}\n\n" +
                $"Index to '{ElasticsearchIndexName}'?",
                "Index Selected to Elasticsearch",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                StatusText = "Indexing cancelled.";
                return;
            }

            ProgressMax = indexableCount;
            ProgressValue = 0;

            var progress = new Progress<ExtractionProgress>(p =>
            {
                ProgressValue = p.CurrentFile;
                StatusText = $"Indexing ({p.CurrentFile}/{p.TotalFiles}): {p.FileName}";
            });

            await _elasticsearchService.IndexFilesAsync(
                files,
                _extractionService.ArchivePath ?? "unknown",
                progress,
                _cancellationTokenSource.Token);

            var totalCount = await _elasticsearchService.GetDocumentCountAsync();
            StatusText = $"Indexed {indexableCount:N0} files from '{SelectedEntry.Name}'. Total in index: {totalCount:N0}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Indexing cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Indexing error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand]
    private async Task ApplyPatchesAsync()
    {
        if (!_extractionService.IsOpen)
        {
            StatusText = "Please open an archive first.";
            return;
        }

        var enabledPatchers = Patchers.Where(p => p.IsEnabled).ToList();
        var zoomPatcher = GetActiveZoomPatcher();
        var giPatcherActive = GiEnabled && _giPatcher != null;
        var brightnessPatcher = GetActiveBrightnessPatcher();
        var sdrScalePatcher = GetActiveSdrScalePatcher();
        var gammaPatcher = GetActiveGammaPatcher();
        var mapRevealPatcherActive = MapRevealEnabled && _mapRevealPatcher != null;
        var vignettePatcherActive = VignetteEnabled && _vignettePatcher != null;
        var envParticlesPatcherActive = EnvParticlesEnabled && _envParticlesPatcher != null;

        if (enabledPatchers.Count == 0 && zoomPatcher == null && !giPatcherActive && brightnessPatcher == null && sdrScalePatcher == null && gammaPatcher == null && !mapRevealPatcherActive && !vignettePatcherActive && !envParticlesPatcherActive)
        {
            StatusText = "Please select at least one optimization to apply.";
            return;
        }

        var confirmMessage = $"Apply {enabledPatchers.Count} selected optimization(s)";
        if (zoomPatcher != null)
        {
            confirmMessage += $" + Camera Zoom x{SelectedZoomLevel}";
        }
        if (giPatcherActive)
        {
            confirmMessage += $" + GI (env={GiEnvLight:F2}, ind={GiIndirectLight:F2})";
        }
        if (brightnessPatcher != null)
        {
            confirmMessage += $" + Brightness x{brightnessPatcher.Multiplier:F2}";
        }
        if (sdrScalePatcher != null)
        {
            confirmMessage += $" + SDR Scale x{sdrScalePatcher.Multiplier:F2}";
        }
        if (gammaPatcher != null)
        {
            confirmMessage += $" + Gamma {gammaPatcher.Gamma:F1}";
        }
        if (mapRevealPatcherActive)
        {
            confirmMessage += " + Map Reveal";
        }
        if (vignettePatcherActive)
        {
            confirmMessage += " + Disable Vignette";
        }
        if (envParticlesPatcherActive)
        {
            confirmMessage += " + Disable Environmental Particles";
        }
        confirmMessage += "?\n\n";
        confirmMessage += string.Join("\n", enabledPatchers.Select(p => $"• {p.Name}"));
        if (zoomPatcher != null)
        {
            confirmMessage += $"\n• Camera Zoom x{SelectedZoomLevel}";
        }
        if (giPatcherActive)
        {
            confirmMessage += $"\n• Global Illumination";
        }
        if (brightnessPatcher != null)
        {
            confirmMessage += $"\n• Brightness Boost x{brightnessPatcher.Multiplier:F2}";
        }
        if (sdrScalePatcher != null)
        {
            confirmMessage += $"\n• SDR Scale x{sdrScalePatcher.Multiplier:F2}";
        }
        if (gammaPatcher != null)
        {
            confirmMessage += $"\n• Gamma {gammaPatcher.Gamma:F1}";
        }
        if (mapRevealPatcherActive)
        {
            confirmMessage += "\n• Map Reveal";
        }
        if (vignettePatcherActive)
        {
            confirmMessage += "\n• Disable Vignette";
        }
        if (envParticlesPatcherActive)
        {
            confirmMessage += "\n• Disable Environmental Particles";
        }

        var result = MessageBox.Show(
            confirmMessage,
            "Apply Patches",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            IsLoading = true;
            _cancellationTokenSource = new CancellationTokenSource();

            var index = GetUnderlyingIndex();
            if (index == null)
            {
                StatusText = "Failed to access archive index.";
                return;
            }

            var totalModified = 0;

            // Apply regular patchers
            foreach (var patcherVm in enabledPatchers)
            {
                var progress = new Progress<string>(msg => StatusText = msg);
                var patchResult = await patcherVm.Patcher.ApplyAsync(index, progress, _cancellationTokenSource.Token);

                if (patchResult.Success)
                {
                    patcherVm.IsApplied = true;
                    patcherVm.IsFailed = false;
                    totalModified += patchResult.FilesModified;
                    // Note: Don't save between patchers - Save() invalidates FileRecord offsets
                    // causing ArgumentOutOfRangeException when next patcher reads. Save once at end.
                }
                else
                {
                    patcherVm.IsFailed = true;
                    patcherVm.IsApplied = false;
                    StatusText = $"Failed to apply {patcherVm.Name}: {patchResult.ErrorMessage}";
                }
            }

            // Apply zoom patcher if selected
            if (zoomPatcher != null)
            {
                var progress = new Progress<string>(msg => StatusText = msg);
                var zoomResult = await zoomPatcher.ApplyAsync(index, progress, _cancellationTokenSource.Token);

                if (zoomResult.Success)
                {
                    totalModified += zoomResult.FilesModified;
                    IsZoomApplied = true;
                    IsZoomDirty = false;
                }
                else
                {
                    StatusText = $"Failed to apply zoom: {zoomResult.ErrorMessage}";
                }
            }

            // Apply GI patcher if enabled
            if (giPatcherActive)
            {
                var progress = new Progress<string>(msg => StatusText = msg);
                var giResult = await _giPatcher!.ApplyAsync(index, progress, _cancellationTokenSource.Token);

                if (giResult.Success)
                {
                    totalModified += giResult.FilesModified;
                    IsGiApplied = true;
                    IsGiDirty = false;
                }
                else
                {
                    StatusText = $"Failed to apply GI: {giResult.ErrorMessage}";
                }
            }


            // Apply brightness patcher if selected (brightnessPatcher already declared above)
            if (brightnessPatcher != null)
            {
                var progress = new Progress<string>(msg => StatusText = msg);
                var brightnessResult = await brightnessPatcher.ApplyAsync(index, progress, _cancellationTokenSource.Token);

                if (brightnessResult.Success)
                {
                    totalModified += brightnessResult.FilesModified;
                    IsBrightnessApplied = true;
                    IsBrightnessDirty = false;
                }
                else
                {
                    StatusText = $"Failed to apply brightness: {brightnessResult.ErrorMessage}";
                }
            }

            // Apply SDR Scale patcher if selected
            if (sdrScalePatcher != null)
            {
                var progress = new Progress<string>(msg => StatusText = msg);
                var sdrScaleResult = await sdrScalePatcher.ApplyAsync(index, progress, _cancellationTokenSource.Token);

                if (sdrScaleResult.Success)
                {
                    totalModified += sdrScaleResult.FilesModified;
                    IsSdrScaleApplied = true;
                    IsSdrScaleDirty = false;
                }
                else
                {
                    StatusText = $"Failed to apply SDR scale: {sdrScaleResult.ErrorMessage}";
                }
            }

            // Apply Gamma patcher if selected
            if (gammaPatcher != null)
            {
                var progress = new Progress<string>(msg => StatusText = msg);
                var gammaResult = await gammaPatcher.ApplyAsync(index, progress, _cancellationTokenSource.Token);

                if (gammaResult.Success)
                {
                    totalModified += gammaResult.FilesModified;
                    IsGammaApplied = true;
                    IsGammaDirty = false;
                }
                else
                {
                    StatusText = $"Failed to apply gamma: {gammaResult.ErrorMessage}";
                }
            }

            // Apply Map Reveal patcher if enabled
            if (mapRevealPatcherActive)
            {
                var progress = new Progress<string>(msg => StatusText = msg);
                var mapRevealResult = await _mapRevealPatcher!.ApplyAsync(index, progress, _cancellationTokenSource.Token);

                if (mapRevealResult.Success)
                {
                    totalModified += mapRevealResult.FilesModified;
                    IsMapRevealApplied = true;
                    IsMapRevealDirty = false;
                }
                else
                {
                    StatusText = $"Failed to apply Map Reveal: {mapRevealResult.ErrorMessage}";
                }
            }

            // Apply Vignette patcher if enabled
            if (vignettePatcherActive)
            {
                var progress = new Progress<string>(msg => StatusText = msg);
                var vignetteResult = await _vignettePatcher!.ApplyAsync(index, progress, _cancellationTokenSource.Token);

                if (vignetteResult.Success)
                {
                    totalModified += vignetteResult.FilesModified;
                    IsVignetteApplied = true;
                    IsVignetteDirty = false;
                }
                else
                {
                    StatusText = $"Failed to apply Vignette: {vignetteResult.ErrorMessage}";
                }
            }

            // Apply Environmental Particles patcher if enabled
            if (envParticlesPatcherActive)
            {
                var progress = new Progress<string>(msg => StatusText = msg);
                var envParticlesResult = await _envParticlesPatcher!.ApplyAsync(index, progress, _cancellationTokenSource.Token);

                if (envParticlesResult.Success)
                {
                    totalModified += envParticlesResult.FilesModified;
                    IsEnvParticlesApplied = true;
                    IsEnvParticlesDirty = false;
                }
                else
                {
                    StatusText = $"Failed to apply Environmental Particles: {envParticlesResult.ErrorMessage}";
                }
            }

            // Save changes to disk
            if (totalModified > 0)
            {
                StatusText = "Saving changes to archive...";
                await Task.Run(() => index.Save());
            }

            var patchCount = enabledPatchers.Count + (zoomPatcher != null ? 1 : 0) + (giPatcherActive ? 1 : 0) + (brightnessPatcher != null ? 1 : 0) + (sdrScalePatcher != null ? 1 : 0) + (gammaPatcher != null ? 1 : 0) + (mapRevealPatcherActive ? 1 : 0) + (vignettePatcherActive ? 1 : 0) + (envParticlesPatcherActive ? 1 : 0);
            StatusText = $"Applied {patchCount} patches. Modified {totalModified} files.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Patching cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Patching error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand]
    private async Task RevertPatchesAsync()
    {
        if (!_extractionService.IsOpen)
        {
            StatusText = "Please open an archive first.";
            return;
        }

        var appliedPatchers = Patchers.Where(p => p.IsApplied).ToList();

        var patchCount = appliedPatchers.Count;
        if (IsZoomApplied) patchCount++;
        if (IsGiApplied) patchCount++;
        if (IsMapRevealApplied) patchCount++;
        if (IsVignetteApplied) patchCount++;
        if (IsEnvParticlesApplied) patchCount++;

        if (patchCount == 0)
        {
            StatusText = "No patches have been applied yet.";
            return;
        }

        var result = MessageBox.Show(
            $"Revert {patchCount} applied patch(es)?",
            "Revert Patches",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            IsLoading = true;

            var index = GetUnderlyingIndex();
            if (index == null)
            {
                StatusText = "Failed to access archive index.";
                return;
            }

            var totalReverted = 0;
            foreach (var patcherVm in appliedPatchers)
            {
                var progress = new Progress<string>(msg => StatusText = msg);
                var revertResult = await patcherVm.Patcher.RevertAsync(index, progress);

                if (revertResult.Success)
                {
                    patcherVm.IsApplied = false;
                    totalReverted += revertResult.FilesModified;
                }
            }

            // Revert Zoom if applied
            if (IsZoomApplied && _zoomPatcher2x != null)
            {
                var progress = new Progress<string>(msg => StatusText = msg);
                // Can use any zoom patcher instance as they share the same marker/backup logic
                var revertResult = await _zoomPatcher2x.RevertAsync(index, progress);

                if (revertResult.Success)
                {
                    IsZoomApplied = false;
                    IsZoomDirty = false;
                    SelectedZoomLevel = 0;
                    totalReverted += revertResult.FilesModified;
                }
            }

            // Revert Map Reveal if applied
            if (IsMapRevealApplied && _mapRevealPatcher != null)
            {
                var progress = new Progress<string>(msg => StatusText = msg);
                var revertResult = await _mapRevealPatcher.RevertAsync(index, progress);

                if (revertResult.Success)
                {
                    IsMapRevealApplied = false;
                    IsMapRevealDirty = false;
                    MapRevealEnabled = false;
                    totalReverted += revertResult.FilesModified;
                }
            }

            // Revert Vignette if applied
            if (IsVignetteApplied && _vignettePatcher != null)
            {
                var progress = new Progress<string>(msg => StatusText = msg);
                var revertResult = await _vignettePatcher.RevertAsync(index, progress);

                if (revertResult.Success)
                {
                    IsVignetteApplied = false;
                    IsVignetteDirty = false;
                    VignetteEnabled = false;
                    totalReverted += revertResult.FilesModified;
                }
            }

            // Revert Environmental Particles if applied
            if (IsEnvParticlesApplied && _envParticlesPatcher != null)
            {
                var progress = new Progress<string>(msg => StatusText = msg);
                var revertResult = await _envParticlesPatcher.RevertAsync(index, progress);

                if (revertResult.Success)
                {
                    IsEnvParticlesApplied = false;
                    IsEnvParticlesDirty = false;
                    EnvParticlesEnabled = false;
                    totalReverted += revertResult.FilesModified;
                }
            }

            // Revert GI if applied
            if (IsGiApplied && _giPatcher != null)
            {
                var progress = new Progress<string>(msg => StatusText = msg);
                var revertResult = await _giPatcher.RevertAsync(index, progress);

                if (revertResult.Success)
                {
                    IsGiApplied = false;
                    IsGiDirty = false;
                    GiEnabled = false;
                    totalReverted += revertResult.FilesModified;
                }
            }

            // Save changes to disk
            if (totalReverted > 0)
            {
                StatusText = "Saving reverted changes to archive...";
                await Task.Run(() => index.Save());
            }

            StatusText = $"Reverted {appliedPatchers.Count} patches. Restored {totalReverted} files.";
        }
        catch (Exception ex)
        {
            StatusText = $"Revert error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private LibBundle3.Index? GetUnderlyingIndex()
    {
        return _extractionService.ActiveIndex;
    }

}
