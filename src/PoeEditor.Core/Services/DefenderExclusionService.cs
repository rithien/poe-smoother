using System.Diagnostics;

namespace PoeEditor.Core.Services;

/// <summary>
/// Service for managing Windows Defender exclusions.
/// </summary>
public static class DefenderExclusionService
{
    /// <summary>
    /// Adds a folder exclusion to Windows Defender.
    /// Requires administrator privileges (will trigger UAC prompt).
    /// </summary>
    /// <param name="folderPath">Path to exclude from scanning.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool AddExclusion(string folderPath)
    {
        try
        {
            var escapedPath = folderPath.Replace("'", "''");
            var command = $"Add-MpPreference -ExclusionPath '{escapedPath}'";
            
            return RunElevatedPowerShell(command);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Removes a folder exclusion from Windows Defender.
    /// Requires administrator privileges (will trigger UAC prompt).
    /// </summary>
    /// <param name="folderPath">Path to remove from exclusions.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool RemoveExclusion(string folderPath)
    {
        try
        {
            var escapedPath = folderPath.Replace("'", "''");
            var command = $"Remove-MpPreference -ExclusionPath '{escapedPath}'";
            
            return RunElevatedPowerShell(command);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a folder is currently excluded from Windows Defender.
    /// </summary>
    /// <param name="folderPath">Path to check.</param>
    /// <returns>True if excluded, false otherwise.</returns>
    public static bool IsExcluded(string folderPath)
    {
        try
        {
            var escapedPath = folderPath.Replace("'", "''");
            var command = $"(Get-MpPreference).ExclusionPath -contains '{escapedPath}'";
            
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{command}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;
            
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            
            return output.Equals("True", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Temporarily disables Windows Defender real-time protection.
    /// Requires administrator privileges (will trigger UAC prompt).
    /// </summary>
    /// <param name="disable">True to disable, false to enable.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool SetRealtimeProtection(bool disable)
    {
        try
        {
            var value = disable ? "$true" : "$false";
            var command = $"Set-MpPreference -DisableRealtimeMonitoring {value}";
            
            return RunElevatedPowerShell(command);
        }
        catch
        {
            return false;
        }
    }

    private static bool RunElevatedPowerShell(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -Command \"{command}\"",
            UseShellExecute = true,
            Verb = "runas" // This triggers UAC
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null) return false;
            
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            // User cancelled UAC or other error
            return false;
        }
    }
}
