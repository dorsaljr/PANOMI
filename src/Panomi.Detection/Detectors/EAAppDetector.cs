using GameFinder.StoreHandlers.EADesktop;
using GameFinder.StoreHandlers.EADesktop.Crypto.Windows;
using NexusMods.Paths;
using Panomi.Core.Models;

namespace Panomi.Detection.Detectors;

/// <summary>
/// Detector for EA App (formerly Origin) games
/// </summary>
public class EAAppDetector : BaseLauncherDetector
{
    public override LauncherType LauncherType => LauncherType.EAApp;
    public override string LauncherName => "EA App";

    private const string EARegistryKey = @"SOFTWARE\Electronic Arts\EA Desktop";
    private const string EARegistryKey32 = @"SOFTWARE\WOW6432Node\Electronic Arts\EA Desktop";

    public override Task<bool> IsInstalledAsync()
    {
        var installPath = GetEAAppPath();
        return Task.FromResult(!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath));
    }

    public override Task<string?> GetInstallPathAsync()
    {
        return Task.FromResult(GetEAAppPath());
    }

    public override Task<DetectionResult> DetectGamesAsync()
    {
        var result = new DetectionResult();
        
        var eaPath = GetEAAppPath();
        if (string.IsNullOrEmpty(eaPath))
        {
            result.ErrorMessage = "EA App installation not found";
            return Task.FromResult(result);
        }

        result.IsInstalled = true;
        result.InstallPath = eaPath;

        // Primary: Use GameFinder to decrypt EA's install path database (finds games on any drive)
        var gameFinderGames = GetGamesFromGameFinder();
        foreach (var game in gameFinderGames)
        {
            if (!result.Games.Any(g => g.Name == game.Name))
                result.Games.Add(game);
        }

        // Secondary: Read game paths from registry
        var registryGames = GetGamesFromRegistry();
        foreach (var game in registryGames)
        {
            if (!result.Games.Any(g => g.Name == game.Name))
                result.Games.Add(game);
        }

        // Fallback: Scan common folders for games not in registry
        var gameInstallPaths = GetGameInstallPaths();
        foreach (var gamesRoot in gameInstallPaths)
        {
            if (!Directory.Exists(gamesRoot))
                continue;

            try
            {
                var gameFolders = Directory.GetDirectories(gamesRoot);
                foreach (var gameFolder in gameFolders)
                {
                    var game = ParseGameFolder(gameFolder);
                    if (game != null && !result.Games.Any(g => g.Name == game.Name))
                    {
                        result.Games.Add(game);
                    }
                }
            }
            catch
            {
                // Continue to next path if access denied
            }
        }

        // Final fallback: Scan drive roots for EA games installed directly (e.g., D:\The Sims 4\)
        var driveRootGames = ScanDriveRootsForEAGames();
        foreach (var game in driveRootGames)
        {
            if (!result.Games.Any(g => g.Name == game.Name))
                result.Games.Add(game);
        }

        return Task.FromResult(result);
    }

    private List<DetectedGame> GetGamesFromGameFinder()
    {
        var games = new List<DetectedGame>();
        
        try
        {
            var fileSystem = FileSystem.Shared;
            var hardwareInfoProvider = new HardwareInfoProvider();
            var handler = new EADesktopHandler(fileSystem, hardwareInfoProvider);
            var results = handler.FindAllGames();
            
            foreach (var result in results)
            {
                // GameFinder returns OneOf<EADesktopGame, ErrorMessage>
                // Check if it's a game (not an error)
                if (result.IsT0)
                {
                    var eaGame = result.AsT0;
                    var installDir = eaGame.BaseInstallPath.ToString();
                    if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                    {
                        // Find the main executable using our existing logic
                        var exePath = FindMainExecutable(installDir);
                    
                        // Build launch command using EA's content ID
                        var contentId = eaGame.EADesktopGameId.Value;
                        string? launchCommand = null;
                        if (!string.IsNullOrEmpty(contentId))
                            launchCommand = $"origin://launchgame/{contentId}";
                        else if (!string.IsNullOrEmpty(exePath))
                            launchCommand = exePath;
                    
                        // BaseSlug is the game's identifier (e.g., "the-sims-4")
                        var gameName = CleanGameTitle(eaGame.BaseSlug.Replace("-", " "));
                            
                        games.Add(new DetectedGame
                        {
                            Name = gameName,
                            ExternalId = contentId ?? eaGame.BaseSlug,
                            InstallPath = installDir,
                            ExecutablePath = exePath,
                            LaunchCommand = launchCommand
                        });
                    }
                }
            }
        }
        catch
        {
            // GameFinder may fail if EA Desktop isn't installed or decryption fails
            // Fall back to other detection methods
        }
        
        return games;
    }

    private List<DetectedGame> ScanDriveRootsForEAGames()
    {
        var games = new List<DetectedGame>();
        
        try
        {
            // Scan all fixed drives
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
            {
                try
                {
                    var driveRoot = drive.RootDirectory.FullName;
                    
                    // Scan up to 3 levels deep from drive root
                    // Level 1: D:\The Sims 4\
                    // Level 2: D:\EA\The Sims 4\
                    // Level 3: D:\Games\EA\The Sims 4\
                    ScanFolderForEAGames(driveRoot, games, maxDepth: 3, currentDepth: 0);
                }
                catch
                {
                    // Skip drives we can't access
                }
            }
        }
        catch
        {
            // Ignore drive enumeration errors
        }
        
        return games;
    }

    private void ScanFolderForEAGames(string folderPath, List<DetectedGame> games, int maxDepth, int currentDepth)
    {
        if (currentDepth >= maxDepth)
            return;
        
        try
        {
            foreach (var subfolder in Directory.GetDirectories(folderPath))
            {
                // Skip system/hidden folders for performance
                var folderName = Path.GetFileName(subfolder);
                if (IsSystemFolder(folderName))
                    continue;
                
                // Check if this folder is an EA game
                var game = CheckFolderForEAGame(subfolder);
                if (game != null)
                {
                    games.Add(game);
                    // Don't recurse into game folders
                    continue;
                }
                
                // Recurse deeper
                ScanFolderForEAGames(subfolder, games, maxDepth, currentDepth + 1);
            }
        }
        catch
        {
            // Skip folders we can't access
        }
    }

    private static bool IsSystemFolder(string folderName)
    {
        // Skip folders that definitely don't contain games
        var skipFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "$Recycle.Bin", "$WinREAgent", "Config.Msi", "Documents and Settings",
            "PerfLogs", "Program Files", "Program Files (x86)", "ProgramData",
            "Recovery", "System Volume Information", "Users", "Windows",
            "msdownld.tmp", "Boot", "ESD"
        };
        return skipFolders.Contains(folderName) || folderName.StartsWith("$");
    }

    private DetectedGame? CheckFolderForEAGame(string folderPath)
    {
        try
        {
            // EA games have __Installer/installerdata.xml
            var installerDataPath = Path.Combine(folderPath, "__Installer", "installerdata.xml");
            if (!File.Exists(installerDataPath))
                return null;
            
            // Validate it's actually an EA game by checking XML content
            var xmlContent = File.ReadAllText(installerDataPath);
            if (!xmlContent.Contains("<DiPManifest") && !xmlContent.Contains("<contentIDs>"))
                return null;
            
            // Use existing ParseGameFolder which handles validation and exe finding
            return ParseGameFolder(folderPath);
        }
        catch
        {
            return null;
        }
    }

    private List<DetectedGame> GetGamesFromRegistry()
    {
        var games = new List<DetectedGame>();
        
        // EA games register under HKLM\SOFTWARE\WOW6432Node\EA Games\{GameTitle}
        var registryPaths = new[]
        {
            @"SOFTWARE\WOW6432Node\EA Games",
            @"SOFTWARE\EA Games"
        };

        foreach (var regPath in registryPaths)
        {
            try
            {
                // Check HKLM first
                var gameKeys = GetRegistrySubKeyNames(regPath);
                foreach (var gameKey in gameKeys)
                {
                    var game = ParseGameFromRegistry(regPath, gameKey, useHKCU: false);
                    if (game != null && !games.Any(g => g.Name == game.Name))
                        games.Add(game);
                }
                
                // Also check HKCU (some EA installations use this)
                var userGameKeys = GetRegistrySubKeyNames(regPath, useHKCU: true);
                foreach (var gameKey in userGameKeys)
                {
                    var game = ParseGameFromRegistry(regPath, gameKey, useHKCU: true);
                    if (game != null && !games.Any(g => g.Name == game.Name))
                        games.Add(game);
                }
            }
            catch
            {
                // Continue if registry access fails
            }
        }

        return games;
    }

    private DetectedGame? ParseGameFromRegistry(string basePath, string gameKey, bool useHKCU = false)
    {
        try
        {
            var fullPath = $@"{basePath}\{gameKey}";
            
            // Read install directory from appropriate registry hive
            var installDir = useHKCU 
                ? ReadUserRegistryValue(fullPath, "Install Dir")
                : ReadRegistryValue(fullPath, "Install Dir");
            if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
                return null;

            // Skip non-game entries
            if (IsSkippableFolder(gameKey))
                return null;

            // Validate it's a real game install
            if (!IsValidGameInstall(installDir))
                return null;

            // Find executable
            var exePath = FindMainExecutable(installDir);
            
            // Get content ID for launch command
            var contentId = GetContentIdForGame(gameKey);

            string? launchCommand = null;
            if (!string.IsNullOrEmpty(contentId))
                launchCommand = $"origin://launchgame/{contentId}";
            else if (!string.IsNullOrEmpty(exePath))
                launchCommand = exePath;

            return new DetectedGame
            {
                Name = CleanGameTitle(gameKey),
                ExternalId = contentId ?? gameKey,
                InstallPath = installDir,
                ExecutablePath = exePath,
                LaunchCommand = launchCommand
            };
        }
        catch
        {
            return null;
        }
    }

    private List<string> GetGameInstallPaths()
    {
        var paths = new List<string>();
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        // Standard EA Games folders
        paths.Add(Path.Combine(programFiles, "EA Games"));
        paths.Add(Path.Combine(programFilesX86, "EA Games"));
        
        // Origin legacy paths
        paths.Add(Path.Combine(programFiles, "Origin Games"));
        paths.Add(Path.Combine(programFilesX86, "Origin Games"));
        
        // Electronic Arts paths
        paths.Add(Path.Combine(programFiles, "Electronic Arts"));
        paths.Add(Path.Combine(programFilesX86, "Electronic Arts"));

        // EA Desktop InstallData paths (contains game install locations)
        paths.AddRange(GetPathsFromEAInstallData(Path.Combine(programData, "EA Desktop", "InstallData")));
        paths.AddRange(GetPathsFromEAInstallData(Path.Combine(localAppData, "Electronic Arts", "EA Desktop", "InstallData")));
        
        // Get paths from Origin manifest files
        paths.AddRange(GetPathsFromOriginManifests(Path.Combine(programData, "Origin", "LocalContent")));
        paths.AddRange(GetPathsFromOriginManifests(Path.Combine(localAppData, "Origin", "LocalContent")));

        // Check all fixed drives for EA Games folders
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
            {
                var driveRoot = drive.RootDirectory.FullName;
                paths.Add(Path.Combine(driveRoot, "EA Games"));
                paths.Add(Path.Combine(driveRoot, "Origin Games"));
                paths.Add(Path.Combine(driveRoot, "Games", "EA Games"));
                paths.Add(Path.Combine(driveRoot, "Games", "Origin Games"));
                paths.Add(Path.Combine(driveRoot, "Games", "Electronic Arts"));
            }
        }
        catch
        {
            // Ignore drive enumeration errors
        }

        return paths.Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
    }

    private List<string> GetPathsFromEAInstallData(string installDataPath)
    {
        var paths = new List<string>();
        try
        {
            if (Directory.Exists(installDataPath))
            {
                // Each game has a subfolder containing install location
                foreach (var gameDir in Directory.GetDirectories(installDataPath))
                {
                    var installPath = Path.GetDirectoryName(gameDir);
                    if (!string.IsNullOrEmpty(installPath))
                    {
                        // The parent directory often contains multiple games
                        var parentOfGame = Path.GetDirectoryName(installPath);
                        if (!string.IsNullOrEmpty(parentOfGame))
                            paths.Add(parentOfGame);
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return paths;
    }

    private List<string> GetPathsFromOriginManifests(string manifestPath)
    {
        var paths = new List<string>();
        try
        {
            if (Directory.Exists(manifestPath))
            {
                foreach (var mfstFile in Directory.GetFiles(manifestPath, "*.mfst"))
                {
                    try
                    {
                        var content = File.ReadAllText(mfstFile);
                        // Parse dipinstallpath from manifest (URL-encoded format)
                        var match = System.Text.RegularExpressions.Regex.Match(
                            content, @"dipinstallpath=([^&\r\n]+)");
                        if (match.Success)
                        {
                            var installPath = Uri.UnescapeDataString(match.Groups[1].Value);
                            if (Directory.Exists(installPath))
                            {
                                paths.Add(installPath);
                                // Also add parent folder as potential games root
                                var parent = Path.GetDirectoryName(installPath);
                                if (!string.IsNullOrEmpty(parent))
                                    paths.Add(parent);
                            }
                        }
                    }
                    catch
                    {
                        // Ignore individual manifest parse errors
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return paths;
    }

    private DetectedGame? ParseGameFolder(string gameFolder)
    {
        try
        {
            var folderName = Path.GetFileName(gameFolder);
            
            // Skip EA Desktop app folder itself
            if (folderName.Equals("EA Desktop", StringComparison.OrdinalIgnoreCase))
                return null;

            // Skip common non-game folders
            if (IsSkippableFolder(folderName))
                return null;

            // Must have at least one executable
            if (!IsValidGameInstall(gameFolder))
                return null;

            // Find the main game executable
            var exePath = FindMainExecutable(gameFolder);
            
            // Get content ID from ProgramData if available
            var contentId = GetContentIdForGame(folderName);
            
            // Build launch command
            string? launchCommand = null;
            if (!string.IsNullOrEmpty(contentId))
            {
                launchCommand = $"origin://launchgame/{contentId}";
            }
            else if (!string.IsNullOrEmpty(exePath))
            {
                launchCommand = exePath;
            }

            return new DetectedGame
            {
                Name = CleanGameTitle(folderName),
                ExternalId = contentId ?? folderName,
                InstallPath = gameFolder,
                ExecutablePath = exePath,
                LaunchCommand = launchCommand
            };
        }
        catch
        {
            return null;
        }
    }

    private string? FindMainExecutable(string gameFolder)
    {
        try
        {
            var folderName = Path.GetFileName(gameFolder)?.ToLowerInvariant() ?? "";
            
            // First check root directory
            var rootExes = Directory.GetFiles(gameFolder, "*.exe", SearchOption.TopDirectoryOnly);
            
            if (rootExes.Length > 0)
            {
                // Prefer exe matching folder name
                var bestMatch = rootExes
                    .OrderByDescending(f => Path.GetFileNameWithoutExtension(f).ToLowerInvariant().Contains(folderName))
                    .ThenByDescending(f => !IsUtilityExe(Path.GetFileName(f)))
                    .ThenByDescending(f => new FileInfo(f).Length)
                    .FirstOrDefault();

                if (bestMatch != null && !IsUtilityExe(Path.GetFileName(bestMatch)))
                    return bestMatch;
            }

            // Check one level deep (common for EA games)
            var subDirs = new[] { "Game", "Bin", "Binaries", "Win64", "x64" };
            foreach (var subDir in subDirs)
            {
                var subPath = Path.Combine(gameFolder, subDir);
                if (Directory.Exists(subPath))
                {
                    var subExes = Directory.GetFiles(subPath, "*.exe", SearchOption.TopDirectoryOnly);
                    var mainExe = subExes.FirstOrDefault(f => !IsUtilityExe(Path.GetFileName(f)));
                    if (mainExe != null)
                        return mainExe;
                }
            }

            // Check nested subdirectories (The Sims 4 uses Game/Bin structure)
            var nestedSubDirs = new[]
            {
                new[] { "Game", "Bin" },
                new[] { "Game", "Bin", "x64" },
                new[] { "Game", "Bin", "Win64" },
                new[] { "__Installer", "Bin" },
                new[] { "Binaries", "Win64" },
            };
            foreach (var nested in nestedSubDirs)
            {
                var subPath = Path.Combine(new[] { gameFolder }.Concat(nested).ToArray());
                if (Directory.Exists(subPath))
                {
                    var subExes = Directory.GetFiles(subPath, "*.exe", SearchOption.TopDirectoryOnly);
                    var mainExe = subExes
                        .OrderByDescending(f => new FileInfo(f).Length)
                        .FirstOrDefault(f => !IsUtilityExe(Path.GetFileName(f)));
                    if (mainExe != null)
                        return mainExe;
                }
            }

            return rootExes.FirstOrDefault(f => !IsUtilityExe(Path.GetFileName(f)));
        }
        catch
        {
            return null;
        }
    }

    private bool IsUtilityExe(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        var utilityPatterns = new[]
        {
            "unins", "crash", "report", "update", "patch",
            "redist", "vcredist", "dxsetup", "dotnet",
            "installer", "setup", "launcher", "helper",
            "eaanticheat", "easyanticheat"
        };
        return utilityPatterns.Any(p => lower.Contains(p));
    }

    private bool IsSkippableFolder(string folderName)
    {
        var lower = folderName.ToLowerInvariant();
        var skipPatterns = new[]
        {
            "directx", "redist", "vcredist", "_commonredist",
            "support", "tools", "sdk", "__installer"
        };
        return skipPatterns.Any(p => lower.Contains(p));
    }

    private string? GetContentIdForGame(string gameName)
    {
        try
        {
            // Check ProgramData for content ID
            var installDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "EA Desktop", "InstallData", gameName);

            if (Directory.Exists(installDataPath))
            {
                // Content ID is in subfolder name (e.g., "base-Origin.SFT.50.0001545")
                var subFolders = Directory.GetDirectories(installDataPath);
                var contentFolder = subFolders.FirstOrDefault();
                if (contentFolder != null)
                {
                    var contentId = Path.GetFileName(contentFolder);
                    // Strip "base-" prefix if present
                    if (contentId.StartsWith("base-", StringComparison.OrdinalIgnoreCase))
                        contentId = contentId.Substring(5);
                    return contentId;
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    private string? GetEAAppPath()
    {
        // Try HKLM registry first
        var path = ReadRegistryValue(EARegistryKey, "DesktopAppPath", use32BitView: true);
        if (!string.IsNullOrEmpty(path))
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                return dir;
        }

        path = ReadRegistryValue(EARegistryKey, "InstallLocation", use32BitView: true);
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        path = ReadRegistryValue(EARegistryKey32, "DesktopAppPath");
        if (!string.IsNullOrEmpty(path))
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                return dir;
        }
        
        // Try HKCU registry (some installations use this)
        path = ReadUserRegistryValue(EARegistryKey, "DesktopAppPath");
        if (!string.IsNullOrEmpty(path))
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                return dir;
        }
        
        path = ReadUserRegistryValue(EARegistryKey, "InstallLocation");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        // Common default paths
        var defaultPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Electronic Arts", "EA Desktop", "EA Desktop"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Electronic Arts", "EA Desktop", "EA Desktop"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Origin"),
        };

        foreach (var defaultPath in defaultPaths)
        {
            if (Directory.Exists(defaultPath))
                return defaultPath;
        }

        return null;
    }

    private string CleanGameTitle(string title)
    {
        return title
            .Replace("™", "")
            .Replace("®", "")
            .Replace("  ", " ")
            .Trim();
    }
}
