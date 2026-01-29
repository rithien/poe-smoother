using System.Collections.Concurrent;
using LibBundle3;
using LibBundle3.Nodes;
using LibBundle3.Records;
using LibBundledGGPK3;
using PoeEditor.Core.Models;

using BundleIndex = LibBundle3.Index;
using BundleFileRecord = LibBundle3.Records.FileRecord;

namespace PoeEditor.Core.Services;

/// <summary>
/// Service for extracting files from Path of Exile archive files.
/// Supports both standalone (Content.ggpk) and Steam/Epic (_.index.bin) versions.
/// Optimized with parallel extraction and buffered I/O.
/// </summary>
public class ExtractionService : IExtractionService
{
    private BundledGGPK? _ggpk;
    private BundleIndex? _index;
    private bool _isStandalone;
    private bool _disposed;

    /// <summary>
    /// Number of parallel extraction threads. Adjust based on CPU cores.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Buffer size for file I/O operations (64KB default).
    /// </summary>
    public int BufferSize { get; set; } = 64 * 1024;

    /// <summary>
    /// How often to report progress (every N files).
    /// </summary>
    public int ProgressReportInterval { get; set; } = 100;

    /// <inheritdoc/>
    public BundleIndex? ActiveIndex => _isStandalone ? _ggpk?.Index : _index;

    /// <inheritdoc/>
    public bool IsOpen => ActiveIndex != null;

    /// <inheritdoc/>
    public ArchiveType ArchiveType
    {
        get
        {
            if (!IsOpen) return ArchiveType.Unknown;
            return _isStandalone ? ArchiveType.Ggpk : ArchiveType.Bundle;
        }
    }

    /// <inheritdoc/>
    public string? ArchivePath { get; private set; }

    /// <inheritdoc/>
    public async Task<bool> OpenArchiveAsync(string path, CancellationToken cancellationToken = default)
    {
        Close();

        return await Task.Run(() =>
        {
            try
            {
                if (path.EndsWith(".ggpk", StringComparison.OrdinalIgnoreCase))
                {
                    // Standalone version
                    _ggpk = new BundledGGPK(path, parsePathsInIndex: true);
                    _isStandalone = true;
                    ArchivePath = path;
                    return true;
                }
                else if (path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                {
                    // Steam/Epic version - direct index file
                    _index = new BundleIndex(path);
                    _index.ParsePaths();
                    _isStandalone = false;
                    ArchivePath = path;
                    return true;
                }
                else if (Directory.Exists(path))
                {
                    // Directory - try to find GGPK or index
                    var ggpkPath = Path.Combine(path, "Content.ggpk");
                    var indexPath = Path.Combine(path, "Bundles2", "_.index.bin");

                    if (File.Exists(ggpkPath))
                    {
                        _ggpk = new BundledGGPK(ggpkPath, parsePathsInIndex: true);
                        _isStandalone = true;
                        ArchivePath = ggpkPath;
                        return true;
                    }
                    else if (File.Exists(indexPath))
                    {
                        _index = new BundleIndex(indexPath);
                        _index.ParsePaths();
                        _isStandalone = false;
                        ArchivePath = indexPath;
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                Close();
                throw;
            }
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public IEnumerable<VirtualFileEntry> GetFileList(string? virtualPath = null)
    {
        var index = ActiveIndex;
        if (index == null)
            yield break;

        var tree = index.BuildTree();

        ITreeNode? targetNode = tree;

        if (!string.IsNullOrEmpty(virtualPath))
        {
            var parts = virtualPath.Split('/', '\\').Where(p => !string.IsNullOrEmpty(p));
            foreach (var part in parts)
            {
                if (targetNode is IDirectoryNode dir)
                {
                    targetNode = dir.Children.FirstOrDefault(c => 
                        c.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
                    if (targetNode == null)
                        yield break;
                }
                else
                {
                    yield break;
                }
            }
        }

        if (targetNode is IDirectoryNode directory)
        {
            foreach (var child in directory.Children)
            {
                yield return CreateVirtualEntry(child);
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<VirtualFileEntry> GetAllFiles()
    {
        var index = ActiveIndex;
        if (index == null)
            yield break;

        foreach (var kvp in index.Files)
        {
            var file = kvp.Value;
            if (file.Path != null)
            {
                yield return new VirtualFileEntry
                {
                    Name = Path.GetFileName(file.Path),
                    FullPath = file.Path,
                    IsDirectory = false,
                    Size = file.Size
                };
            }
        }
    }

    /// <inheritdoc/>
    public async Task ExtractFileAsync(string virtualPath, string outputPath, CancellationToken cancellationToken = default)
    {
        var index = ActiveIndex;
        if (index == null)
            throw new InvalidOperationException("No archive is open.");

        await Task.Run(() =>
        {
            var file = index.Files.Values.FirstOrDefault(f => f.Path == virtualPath);
            if (file == null)
                throw new FileNotFoundException($"File not found in archive: {virtualPath}");

            ExtractSingleFile(file, outputPath);
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ReadOnlyMemory<byte>> ReadFileContentAsync(string virtualPath, CancellationToken cancellationToken = default)
    {
        var index = ActiveIndex;
        if (index == null)
            throw new InvalidOperationException("No archive is open.");

        return await Task.Run(() =>
        {
            var file = index.Files.Values.FirstOrDefault(f => f.Path == virtualPath);
            if (file == null)
                throw new FileNotFoundException($"File not found in archive: {virtualPath}");

            return file.Read();
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task WriteFileContentAsync(string virtualPath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        var index = ActiveIndex;
        if (index == null)
            throw new InvalidOperationException("No archive is open.");

        await Task.Run(() =>
        {
            var file = index.Files.Values.FirstOrDefault(f => f.Path == virtualPath);
            if (file == null)
                throw new FileNotFoundException($"File not found in archive: {virtualPath}");

            file.Write(content.Span);

            // NOTE: Do NOT call index.Save() here!
            // After Save(), Bundle objects are rewritten and FileRecord offsets become invalid.
            // This causes ArgumentOutOfRangeException when other patchers try to read files.
            // Changes are kept in memory and saved by ApplyPatchesAsync or SaveArchiveAsync.
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SaveArchiveAsync(CancellationToken cancellationToken = default)
    {
        var index = ActiveIndex;
        if (index == null)
            throw new InvalidOperationException("No archive is open.");

        await Task.Run(() => index.Save(), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ExtractFilesAsync(
        IEnumerable<string> virtualPaths,
        string outputDirectory,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var index = ActiveIndex;
        if (index == null)
            throw new InvalidOperationException("No archive is open.");

        var pathList = virtualPaths.ToList();
        var total = pathList.Count;

        // Build file lookup map
        var fileMap = index.Files.Values
            .Where(f => f.Path != null)
            .ToDictionary(f => f.Path!, f => f);

        // Filter to only files that exist
        var filesToExtract = pathList
            .Where(p => fileMap.ContainsKey(p))
            .Select(p => fileMap[p])
            .ToList();

        await ExtractFilesParallelAsync(filesToExtract, outputDirectory, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ExtractAllAsync(
        string outputDirectory,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var index = ActiveIndex;
        if (index == null)
            throw new InvalidOperationException("No archive is open.");

        var files = index.Files.Values.Where(f => f.Path != null).ToList();
        
        await ExtractFilesParallelAsync(files, outputDirectory, progress, cancellationToken);
    }

    /// <summary>
    /// Extracts files with bundle-aware parallelism.
    /// Files from the same bundle are extracted sequentially to avoid file locking.
    /// Different bundles are processed in parallel.
    /// </summary>
    private async Task ExtractFilesParallelAsync(
        List<BundleFileRecord> files,
        string outputDirectory,
        IProgress<ExtractionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var total = files.Count;
        var processedCount = 0;
        var createdDirectories = new ConcurrentDictionary<string, byte>();

        // Pre-create common directories to reduce contention
        var directories = files
            .Select(f => Path.GetDirectoryName(Path.Combine(outputDirectory, f.Path!.Replace('/', Path.DirectorySeparatorChar))))
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct()
            .ToList();

        // Create directories in parallel
        Parallel.ForEach(directories, new ParallelOptions 
        { 
            MaxDegreeOfParallelism = MaxDegreeOfParallelism,
            CancellationToken = cancellationToken 
        }, dir =>
        {
            if (createdDirectories.TryAdd(dir!, 1))
            {
                Directory.CreateDirectory(dir!);
            }
        });

        // Group files by their bundle to avoid concurrent access to same bundle file
        var filesByBundle = files.GroupBy(f => f.BundleRecord).ToList();

        // Process bundles in parallel, but files within each bundle sequentially
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxDegreeOfParallelism,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(filesByBundle, options, async (bundleGroup, ct) =>
        {
            // Process files from this bundle sequentially
            foreach (var file in bundleGroup)
            {
                ct.ThrowIfCancellationRequested();

                var virtualPath = file.Path!;
                var outputPath = Path.Combine(outputDirectory, virtualPath.Replace('/', Path.DirectorySeparatorChar));

                // Extract file with buffered I/O
                ExtractSingleFile(file, outputPath);

                // Update progress (thread-safe)
                var current = Interlocked.Increment(ref processedCount);
                
                // Report progress every N files to reduce UI overhead
                if (progress != null && (current % ProgressReportInterval == 0 || current == total))
                {
                    progress.Report(new ExtractionProgress
                    {
                        CurrentFile = current,
                        TotalFiles = total,
                        FileName = virtualPath,
                        FileSize = file.Size
                    });
                }
            }

            await Task.CompletedTask; // Keep async signature
        });

        // Final progress report
        progress?.Report(new ExtractionProgress
        {
            CurrentFile = total,
            TotalFiles = total,
            FileName = "Extraction complete",
            FileSize = 0
        });
    }

    /// <summary>
    /// Extracts a single file with optimized buffered I/O.
    /// Handles overwriting existing files and retries on transient failures.
    /// </summary>
    private void ExtractSingleFile(BundleFileRecord file, string outputPath)
    {
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        // Read file data from archive
        var data = file.Read();

        // Retry logic for handling file locks in parallel scenarios
        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Remove read-only attribute if present
                if (File.Exists(outputPath))
                {
                    var attributes = File.GetAttributes(outputPath);
                    if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(outputPath, attributes & ~FileAttributes.ReadOnly);
                    }
                }

                // Write with buffered FileStream for better performance
                using var fileStream = new FileStream(
                    outputPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    BufferSize,
                    FileOptions.SequentialScan);
                
                fileStream.Write(data.Span);
                return; // Success, exit the method
            }
            catch (IOException) when (attempt < maxRetries - 1)
            {
                // Wait a bit and retry (file might be locked by another thread)
                Thread.Sleep(50 * (attempt + 1));
            }
        }
    }

    /// <inheritdoc/>
    public void Close()
    {
        _ggpk?.Dispose();
        _ggpk = null;
        _index?.Dispose();
        _index = null;
        ArchivePath = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            Close();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    private static VirtualFileEntry CreateVirtualEntry(ITreeNode node)
    {
        var isDir = node is IDirectoryNode;
        var path = ITreeNode.GetPath(node);
        
        var entry = new VirtualFileEntry
        {
            Name = node.Name,
            FullPath = path,
            IsDirectory = isDir,
            Size = isDir ? 0 : (node is IFileNode fn ? fn.Record.Size : 0)
        };

        if (isDir && node is IDirectoryNode dir)
        {
            foreach (var child in dir.Children)
            {
                entry.Children.Add(CreateVirtualEntry(child));
            }
        }

        return entry;
    }
}
