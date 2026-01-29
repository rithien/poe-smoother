using LibBundle3;
using PoeEditor.Core.Models;

using BundleIndex = LibBundle3.Index;

namespace PoeEditor.Core.Services;

/// <summary>
/// Interface for extraction operations on Path of Exile archive files.
/// </summary>
public interface IExtractionService : IDisposable
{
    /// <summary>
    /// Gets whether an archive is currently open.
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// Gets the underlying bundle index for patching operations.
    /// Returns null if no archive is open.
    /// </summary>
    BundleIndex? ActiveIndex { get; }

    /// <summary>
    /// Gets the type of the currently open archive.
    /// </summary>
    ArchiveType ArchiveType { get; }

    /// <summary>
    /// Gets the path to the currently open archive.
    /// </summary>
    string? ArchivePath { get; }

    /// <summary>
    /// Opens an archive file (Content.ggpk or _.index.bin).
    /// </summary>
    /// <param name="path">Path to the archive file or PoE installation directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully opened, false otherwise.</returns>
    Task<bool> OpenArchiveAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of files and directories at the specified path.
    /// </summary>
    /// <param name="virtualPath">Virtual path within the archive (null or empty for root).</param>
    /// <returns>List of file entries.</returns>
    IEnumerable<VirtualFileEntry> GetFileList(string? virtualPath = null);

    /// <summary>
    /// Gets all files in the archive recursively.
    /// </summary>
    /// <returns>List of all file entries (files only, not directories).</returns>
    IEnumerable<VirtualFileEntry> GetAllFiles();

    /// <summary>
    /// Extracts a single file from the archive.
    /// </summary>
    /// <param name="virtualPath">Virtual path of the file within the archive.</param>
    /// <param name="outputPath">Output path on the file system.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExtractFileAsync(string virtualPath, string outputPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts multiple files from the archive.
    /// </summary>
    /// <param name="virtualPaths">Virtual paths of files to extract.</param>
    /// <param name="outputDirectory">Output directory on the file system.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExtractFilesAsync(
        IEnumerable<string> virtualPaths,
        string outputDirectory,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts all files from the archive.
    /// </summary>
    /// <param name="outputDirectory">Output directory on the file system.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExtractAllAsync(
        string outputDirectory,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads file content from the archive without extracting to disk.
    /// </summary>
    /// <param name="virtualPath">Virtual path of the file within the archive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raw file content as bytes.</returns>
    Task<ReadOnlyMemory<byte>> ReadFileContentAsync(string virtualPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes file content directly to the archive.
    /// Note: This does NOT automatically save to disk. Call SaveArchiveAsync to persist changes.
    /// </summary>
    /// <param name="virtualPath">Virtual path of the file within the archive.</param>
    /// <param name="content">Raw file content as bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteFileContentAsync(string virtualPath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all pending changes to the archive on disk.
    /// IMPORTANT: After calling this, FileRecord offsets may be invalidated.
    /// Do not read files after saving without reopening the archive.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveArchiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the currently open archive.
    /// </summary>
    void Close();
}
