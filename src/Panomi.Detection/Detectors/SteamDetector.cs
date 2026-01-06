using Panomi.Core.Models;
using System.Text.RegularExpressions;

namespace Panomi.Detection.Detectors;

/// <summary>
/// Detector for Steam games using Registry and VDF/ACF file parsing
/// </summary>
public class SteamDetector : BaseLauncherDetector
{
    public override LauncherType LauncherType => LauncherType.Steam;
    public override string LauncherName => "Steam";

    private const string SteamRegistryKey = @"SOFTWARE\Valve\Steam";
    private const string SteamRegistryKey32 = @"SOFTWARE\WOW6432Node\Valve\Steam";

    public override Task<bool> IsInstalledAsync()
    {
        var installPath = GetSteamPath();
        return Task.FromResult(!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath));
    }

    public override Task<string?> GetInstallPathAsync()
    {
        return Task.FromResult(GetSteamPath());
    }

    public override async Task<DetectionResult> DetectGamesAsync()
    {
        var result = new DetectionResult();
        
        var steamPath = GetSteamPath();
        if (string.IsNullOrEmpty(steamPath))
        {
            result.ErrorMessage = "Steam installation not found";
            return result;
        }

        result.IsInstalled = true;
        result.InstallPath = steamPath;

        // Get all library folders
        var libraryFolders = await GetLibraryFoldersAsync(steamPath);
        
        // Scan each library folder for installed games
        foreach (var libraryPath in libraryFolders)
        {
            var steamAppsPath = Path.Combine(libraryPath, "steamapps");
            if (!Directory.Exists(steamAppsPath)) continue;

            var manifestFiles = FindFiles(steamAppsPath, "appmanifest_*.acf");
            foreach (var manifestFile in manifestFiles)
            {
                var game = await ParseAppManifestAsync(manifestFile, steamAppsPath);
                if (game != null)
                {
                    result.Games.Add(game);
                }
            }
        }

        return result;
    }

    private string? GetSteamPath()
    {
        // Try reading with 32-bit registry view (Steam is a 32-bit app)
        var path = ReadRegistryValue(SteamRegistryKey, "InstallPath", use32BitView: true);
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        // Try 64-bit registry 
        path = ReadRegistryValue(SteamRegistryKey, "InstallPath");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        // Try HKCU
        path = ReadUserRegistryValue(SteamRegistryKey, "SteamPath");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        // Try common installation paths as fallback (using environment variables for portability)
        var commonPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"),
        };

        foreach (var commonPath in commonPaths)
        {
            if (Directory.Exists(commonPath))
                return commonPath;
        }

        return null;
    }

    private async Task<List<string>> GetLibraryFoldersAsync(string steamPath)
    {
        var libraries = new List<string> { steamPath };

        var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersPath))
            return libraries;

        var content = await ReadFileAsync(libraryFoldersPath);
        if (string.IsNullOrEmpty(content))
            return libraries;

        // Parse VDF format to extract library paths
        // Look for "path" values in the VDF structure
        var pathMatches = Regex.Matches(content, @"""path""\s*""([^""]+)""", RegexOptions.IgnoreCase);
        foreach (Match match in pathMatches)
        {
            var path = match.Groups[1].Value.Replace(@"\\", @"\");
            if (Directory.Exists(path) && !libraries.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                libraries.Add(path);
            }
        }

        return libraries;
    }

    private async Task<DetectedGame?> ParseAppManifestAsync(string manifestPath, string steamAppsPath)
    {
        var content = await ReadFileAsync(manifestPath);
        if (string.IsNullOrEmpty(content))
            return null;

        // Extract AppId
        var appIdMatch = Regex.Match(content, @"""appid""\s*""(\d+)""", RegexOptions.IgnoreCase);
        if (!appIdMatch.Success)
            return null;

        var appId = appIdMatch.Groups[1].Value;

        // Skip Steamworks Common Redistributables (has .exe installers but isn't a game)
        if (appId == "228980")
            return null;

        // Extract name
        var nameMatch = Regex.Match(content, @"""name""\s*""([^""]+)""", RegexOptions.IgnoreCase);
        if (!nameMatch.Success)
            return null;

        var name = nameMatch.Groups[1].Value;

        // Extract install directory
        var installDirMatch = Regex.Match(content, @"""installdir""\s*""([^""]+)""", RegexOptions.IgnoreCase);
        var installDir = installDirMatch.Success ? installDirMatch.Groups[1].Value : null;

        var installPath = !string.IsNullOrEmpty(installDir) 
            ? Path.Combine(steamAppsPath, "common", installDir) 
            : null;

        // Only include games that are actually installed with executable files
        if (!IsValidGameInstall(installPath))
            return null;

        // Find the main executable for icon extraction
        string? executablePath = null;
        if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
        {
            // Look for exe files in root directory first
            var exeFiles = Directory.GetFiles(installPath, "*.exe", SearchOption.TopDirectoryOnly)
                .Where(f => !IsUtilityExe(Path.GetFileName(f)))
                .ToList();

            // Prefer exe matching game name or installdir
            var gameLower = name.ToLowerInvariant().Replace(" ", "");
            executablePath = exeFiles.FirstOrDefault(f => 
                Path.GetFileNameWithoutExtension(f).ToLowerInvariant().Replace(" ", "").Contains(gameLower)) 
                ?? exeFiles.FirstOrDefault();
            
            // If no exe found in root, check common subdirectories
            if (executablePath == null)
            {
                var subDirs = new[] { "Game", "Bin", "Binaries", "Win64", "x64", "Win32", "bin" };
                foreach (var subDir in subDirs)
                {
                    var subPath = Path.Combine(installPath, subDir);
                    if (Directory.Exists(subPath))
                    {
                        var subExes = Directory.GetFiles(subPath, "*.exe", SearchOption.TopDirectoryOnly)
                            .Where(f => !IsUtilityExe(Path.GetFileName(f)))
                            .ToList();
                        executablePath = subExes.FirstOrDefault();
                        if (executablePath != null) break;
                    }
                }
            }
            
            // Check nested subdirectories (The Sims 4 uses Game/Bin structure)
            if (executablePath == null)
            {
                var nestedSubDirs = new[]
                {
                    new[] { "Game", "Bin" },
                    new[] { "Game", "Bin", "x64" },
                    new[] { "Game", "Bin", "Win64" },
                    new[] { "Binaries", "Win64" },
                    new[] { "Binaries", "Win32" },
                };
                foreach (var nested in nestedSubDirs)
                {
                    var subPath = Path.Combine(new[] { installPath }.Concat(nested).ToArray());
                    if (Directory.Exists(subPath))
                    {
                        var subExes = Directory.GetFiles(subPath, "*.exe", SearchOption.TopDirectoryOnly)
                            .OrderByDescending(f => new FileInfo(f).Length)
                            .Where(f => !IsUtilityExe(Path.GetFileName(f)))
                            .ToList();
                        executablePath = subExes.FirstOrDefault();
                        if (executablePath != null) break;
                    }
                }
            }
        }

        return new DetectedGame
        {
            Name = name,
            ExternalId = appId,
            InstallPath = installPath,
            ExecutablePath = executablePath,
            LaunchCommand = $"steam://rungameid/{appId}"
        };
    }

    private bool IsUtilityExe(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        var utilityPatterns = new[]
        {
            "unins", "crash", "report", "update", "patch",
            "redist", "vcredist", "dxsetup", "dotnet",
            "installer", "setup", "helper",
            "unitycrashhandler", "ue4prereqsetup", "crashreportclient",
            "eaanticheat", "easyanticheat"
        };
        return utilityPatterns.Any(p => lower.Contains(p));
    }
}
