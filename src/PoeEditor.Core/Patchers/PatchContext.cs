using System.Collections.Concurrent;

namespace PoeEditor.Core.Patchers;

/// <summary>
/// Holds the state of file modifications during a batch patch operation.
/// Allows patchers to share modified content in memory before writing to disk/archive.
/// </summary>
public class PatchContext
{
    private readonly ConcurrentDictionary<string, byte[]> _modifiedFiles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Dictionary mapping virtual file paths to their modified content bytes.
    /// Keys are case-insensitive virtual paths (e.g., "metadata/characters/character.ot").
    /// </summary>
    public ConcurrentDictionary<string, byte[]> ModifiedFiles => _modifiedFiles;
    
    /// <summary>
    /// Gets the modified content for a file if it exists in the context.
    /// </summary>
    public bool TryGetContent(string path, out byte[]? content)
    {
        return _modifiedFiles.TryGetValue(path, out content);
    }

    /// <summary>
    /// Adds or updates the modified content for a file.
    /// </summary>
    public void UpdateContent(string path, byte[] content)
    {
        _modifiedFiles[path] = content;
    }
}
