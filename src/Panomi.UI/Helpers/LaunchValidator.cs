using System.Text.RegularExpressions;

namespace Panomi.UI.Helpers;

/// <summary>
/// Validates launch commands for security before execution
/// </summary>
public static class LaunchValidator
{
    // Allowed URI schemes for game launchers
    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "steam",           // steam://run/12345
        "com.epicgames.launcher", // Epic Games
        "uplay",           // Ubisoft Connect
        "origin",          // EA/Origin
        "origin2",         // EA Desktop
        "battlenet",       // Battle.net
        "gog",             // GOG Galaxy
        "rockstar",        // Rockstar Games
        "riotclient",      // Riot Games
        "minecraft",       // Minecraft
        "roblox",          // Roblox
        "roblox-player",   // Roblox
        "shell",           // Windows shell protocol (for Store apps)
    };

    // Dangerous patterns that should never be executed
    private static readonly string[] DangerousPatterns = new[]
    {
        "cmd",
        "cmd.exe",
        "powershell",
        "powershell.exe",
        "pwsh",
        "pwsh.exe",
        "wscript",
        "cscript",
        "mshta",
        "rundll32",
        "regsvr32",
        "certutil",
        "bitsadmin",
    };

    /// <summary>
    /// Validates if a launch command is safe to execute
    /// </summary>
    public static bool IsValidLaunchCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        command = command.Trim();

        // Check if it's a URI scheme (steam://, etc.)
        if (IsAllowedUriScheme(command))
            return true;

        // Check if it's a file path
        if (IsValidFilePath(command))
            return true;

        return false;
    }

    /// <summary>
    /// Checks if command is an allowed URI scheme
    /// </summary>
    private static bool IsAllowedUriScheme(string command)
    {
        // Check for URI pattern
        var colonIndex = command.IndexOf(':');
        if (colonIndex <= 0)
            return false;

        var scheme = command.Substring(0, colonIndex).ToLowerInvariant();
        return AllowedSchemes.Contains(scheme);
    }

    /// <summary>
    /// Validates file path is a real executable and not a dangerous command
    /// </summary>
    private static bool IsValidFilePath(string command)
    {
        try
        {
            // Handle paths with arguments
            var path = command;
            
            // If path is quoted, extract it
            if (command.StartsWith("\""))
            {
                var endQuote = command.IndexOf("\"", 1);
                if (endQuote > 1)
                    path = command.Substring(1, endQuote - 1);
            }
            else if (command.Contains(" "))
            {
                // Try to find a valid file path before arguments
                var parts = command.Split(' ');
                var testPath = "";
                foreach (var part in parts)
                {
                    testPath = string.IsNullOrEmpty(testPath) ? part : testPath + " " + part;
                    if (File.Exists(testPath))
                    {
                        path = testPath;
                        break;
                    }
                }
            }

            // Get just the filename for dangerous pattern check
            var fileName = Path.GetFileName(path).ToLowerInvariant();
            
            // Block dangerous executables
            foreach (var dangerous in DangerousPatterns)
            {
                if (fileName.Equals(dangerous, StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals(dangerous + ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Verify file actually exists
            if (!File.Exists(path))
                return false;

            // Verify it's in a reasonable location (not temp, system32, etc.)
            var fullPath = Path.GetFullPath(path).ToLowerInvariant();
            
            // Block system directories
            var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows).ToLowerInvariant();
            if (fullPath.StartsWith(systemRoot))
                return false;

            // Allow common game locations
            var allowedRoots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).ToLowerInvariant(),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).ToLowerInvariant(),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).ToLowerInvariant(),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).ToLowerInvariant(),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData).ToLowerInvariant(),
            };

            // Also allow drive roots for common game folders (D:\Games, E:\SteamLibrary, etc.)
            var driveRoots = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed)
                .Select(d => d.RootDirectory.FullName.ToLowerInvariant())
                .ToArray();

            // Check if path is in allowed location
            if (allowedRoots.Any(root => !string.IsNullOrEmpty(root) && fullPath.StartsWith(root)))
                return true;

            // Allow files on any fixed drive (games can be installed anywhere)
            if (driveRoots.Any(root => fullPath.StartsWith(root)))
                return true;

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sanitizes a launch command for safe display in error messages
    /// </summary>
    public static string SanitizeForDisplay(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return "(empty)";

        // Truncate long paths
        if (command.Length > 100)
            return command.Substring(0, 97) + "...";

        return command;
    }
}
