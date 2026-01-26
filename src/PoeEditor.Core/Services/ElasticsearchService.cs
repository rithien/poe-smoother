using System.Security.Cryptography;
using System.Text;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using LibBundle3.Records;
using PoeEditor.Core.Models;

namespace PoeEditor.Core.Services;

/// <summary>
/// Service for indexing PoE files to Elasticsearch.
/// </summary>
public class ElasticsearchService : IDisposable
{
    private ElasticsearchClient? _client;
    private bool _disposed;

    /// <summary>
    /// Supported file extensions for full-text indexing.
    /// </summary>
    public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".hlsl", ".inc", ".ffx", ".ot", ".it", ".ao",
        ".otc", ".itc", ".aoc", ".txt", ".mat", ".mtp", ".sm", ".amd", ".json"
    };

    /// <summary>
    /// Default index name for PoE files.
    /// </summary>
    public string IndexName { get; set; } = "poe-files";

    /// <summary>
    /// Batch size for bulk indexing.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Whether the service is connected to Elasticsearch.
    /// </summary>
    public bool IsConnected => _client != null;

    /// <summary>
    /// Connects to Elasticsearch server.
    /// </summary>
    /// <param name="url">Elasticsearch URL (e.g., http://localhost:9200)</param>
    /// <param name="username">Optional username for authentication.</param>
    /// <param name="password">Optional password for authentication.</param>
    /// <returns>True if connection successful.</returns>
    public async Task<bool> ConnectAsync(string url, string? username = null, string? password = null)
    {
        try
        {
            var settings = new ElasticsearchClientSettings(new Uri(url));

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                settings = settings.Authentication(new BasicAuthentication(username, password));
            }

            settings = settings
                .RequestTimeout(TimeSpan.FromMinutes(2))
                .DisableDirectStreaming();

            _client = new ElasticsearchClient(settings);

            // Test connection
            var pingResponse = await _client.PingAsync();
            return pingResponse.IsValidResponse;
        }
        catch
        {
            _client = null;
            return false;
        }
    }

    /// <summary>
    /// Creates the index with appropriate mappings if it doesn't exist.
    /// </summary>
    public async Task<bool> EnsureIndexExistsAsync()
    {
        if (_client == null) return false;

        var existsResponse = await _client.Indices.ExistsAsync(IndexName);
        if (existsResponse.Exists) return true;

        var createResponse = await _client.Indices.CreateAsync(IndexName, c => c
            .Settings(s => s
                .NumberOfShards(1)
                .NumberOfReplicas(0)
                .Analysis(a => a
                    .Analyzers(an => an
                        .Custom("code_analyzer", ca => ca
                            .Tokenizer("standard")
                            .Filter(["lowercase", "asciifolding"])
                        )
                    )
                )
            )
            .Mappings(m => m
                .Properties<FileDocument>(p => p
                    .Keyword(k => k.Path)
                    .Keyword(k => k.FileName)
                    .Keyword(k => k.Extension)
                    .Text(t => t.Content, t => t.Analyzer("code_analyzer"))
                    .LongNumber(l => l.Size)
                    .Keyword(k => k.SourceArchive)
                    .Date(d => d.IndexedAt)
                )
            )
        );

        return createResponse.IsValidResponse;
    }

    /// <summary>
    /// Indexes files from the archive to Elasticsearch.
    /// Only indexes files with supported extensions.
    /// </summary>
    public async Task IndexFilesAsync(
        IEnumerable<FileRecord> files,
        string sourceArchive,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected to Elasticsearch.");

        // Filter to supported extensions
        var filesToIndex = files
            .Where(f => f.Path != null && IsSupportedFile(f.Path))
            .ToList();

        var total = filesToIndex.Count;
        var processed = 0;
        var batch = new List<FileDocument>(BatchSize);
        var batchCount = 0;

        foreach (var file in filesToIndex)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Read file content
                var data = file.Read();
                
                // Detect encoding - POE .ot files are typically UTF-16 LE
                var content = DetectAndDecodeContent(data.Span, file.Path!);

                var doc = new FileDocument
                {
                    Id = ComputeHash(file.Path!),
                    Path = file.Path!,
                    FileName = Path.GetFileName(file.Path!),
                    Extension = Path.GetExtension(file.Path!).ToLowerInvariant(),
                    Content = content,
                    Size = file.Size,
                    SourceArchive = sourceArchive,
                    IndexedAt = DateTime.UtcNow
                };

                batch.Add(doc);

                if (batch.Count >= BatchSize)
                {
                    await IndexBatchAsync(batch, cancellationToken).ConfigureAwait(false);
                    batch.Clear();
                    batchCount++;
                    
                    // Allow UI to update and release memory every 10 batches
                    if (batchCount % 10 == 0)
                    {
                        await Task.Yield();
                        GC.Collect(0, GCCollectionMode.Optimized, false);
                    }
                }
            }
            catch
            {
                // Skip files that can't be read as text
            }

            processed++;
            if (progress != null && (processed % 100 == 0 || processed == total))
            {
                progress.Report(new ExtractionProgress
                {
                    CurrentFile = processed,
                    TotalFiles = total,
                    FileName = file.Path ?? "",
                    FileSize = file.Size
                });
                
                // Yield to keep UI responsive
                if (processed % 500 == 0)
                {
                    await Task.Yield();
                }
            }
        }

        // Index remaining batch
        if (batch.Count > 0)
        {
            await IndexBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        }

        // Final cleanup
        batch.Clear();
        GC.Collect(0, GCCollectionMode.Optimized, false);
    }

    private async Task IndexBatchAsync(List<FileDocument> documents, CancellationToken cancellationToken)
    {
        if (_client == null) return;

        try
        {
            var bulkResponse = await _client.BulkAsync(b => b
                .Index(IndexName)
                .IndexMany(documents, (descriptor, doc) => descriptor.Id(doc.Id)),
                cancellationToken
            ).ConfigureAwait(false);

            if (!bulkResponse.IsValidResponse)
            {
                // Log error but don't throw - continue with next batch
                System.Diagnostics.Debug.WriteLine($"Bulk indexing warning: {bulkResponse.DebugInformation}");
            }
        }
        catch (Exception ex)
        {
            // Log but continue - don't stop entire indexing for one failed batch
            System.Diagnostics.Debug.WriteLine($"Batch indexing error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the count of documents in the index.
    /// </summary>
    public async Task<long> GetDocumentCountAsync()
    {
        if (_client == null) return 0;

        var response = await _client.CountAsync<FileDocument>(c => c.Indices(IndexName));
        return response.IsValidResponse ? response.Count : 0;
    }

    /// <summary>
    /// Gets the count of files that would be indexed (files with supported extensions).
    /// </summary>
    public static int GetIndexableFileCount(IEnumerable<FileRecord> files)
    {
        return files.Count(f => f.Path != null && IsSupportedFile(f.Path));
    }

    /// <summary>
    /// Checks if a file has a supported extension for indexing.
    /// </summary>
    public static bool IsSupportedFile(string path)
    {
        var ext = Path.GetExtension(path);
        return SupportedExtensions.Contains(ext);
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Detects and decodes content, handling both UTF-8 and UTF-16 encodings.
    /// POE .ot, .it, .ao files are typically UTF-16 LE.
    /// </summary>
    private static string DetectAndDecodeContent(ReadOnlySpan<byte> data, string filePath)
    {
        if (data.Length == 0) return string.Empty;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        // Check for UTF-16 LE BOM (FF FE)
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(data);
        }
        
        // Check for UTF-16 BE BOM (FE FF)
        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(data);
        }
        
        // .ot, .otc, .it, .itc, .ao, .aoc files are typically UTF-16 LE without BOM
        if (extension is ".ot" or ".otc" or ".it" or ".itc" or ".ao" or ".aoc")
        {
            // Heuristic: if every second byte is 0x00, it's likely UTF-16 LE
            if (data.Length >= 4)
            {
                int nullCount = 0;
                int sampleSize = Math.Min(100, data.Length / 2);
                
                for (int i = 1; i < sampleSize * 2; i += 2)
                {
                    if (data[i] == 0x00) nullCount++;
                }
                
                // If more than 80% of sampled odd bytes are null, it's UTF-16 LE
                if (nullCount > sampleSize * 0.8)
                {
                    return Encoding.Unicode.GetString(data);
                }
            }
        }
        
        // Default to UTF-8
        return Encoding.UTF8.GetString(data);
    }

    public void Disconnect()
    {
        _client = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Disconnect();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
