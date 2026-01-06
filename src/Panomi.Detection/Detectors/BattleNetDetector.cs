using Panomi.Core.Models;
using System.Text.Json;

namespace Panomi.Detection.Detectors;

/// <summary>
/// Detector for Battle.net games
/// </summary>
public class BattleNetDetector : BaseLauncherDetector
{
    public override LauncherType LauncherType => LauncherType.BattleNet;
    public override string LauncherName => "Battle.net";

    private const string BattleNetRegistryKey = @"SOFTWARE\WOW6432Node\Blizzard Entertainment\Battle.net";
    private const string BattleNetRegistryKey64 = @"SOFTWARE\Blizzard Entertainment\Battle.net";

    // Known Battle.net game codes
    private static readonly Dictionary<string, string> KnownGames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "wow", "World of Warcraft" },
        { "wow_classic", "World of Warcraft Classic" },
        { "d3", "Diablo III" },
        { "fen", "Diablo IV" },
        { "pro", "Overwatch 2" },
        { "hs", "Hearthstone" },
        { "hero", "Heroes of the Storm" },
        { "s2", "StarCraft II" },
        { "s1", "StarCraft Remastered" },
        { "w3", "Warcraft III: Reforged" },
        { "viper", "Call of Duty" },
        { "odin", "Call of Duty: Modern Warfare" },
        { "lazr", "Call of Duty: MW2" },
        { "fore", "Call of Duty: Black Ops 6" },
        { "zeus", "Call of Duty: Warzone" },
        { "anbs", "Diablo II: Resurrected" },
        { "rtro", "Blizzard Arcade Collection" },
        { "wlby", "Crash Bandicoot 4" },
    };

    public override Task<bool> IsInstalledAsync()
    {
        var installPath = GetBattleNetPath();
        return Task.FromResult(!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath));
    }

    public override Task<string?> GetInstallPathAsync()
    {
        return Task.FromResult(GetBattleNetPath());
    }

    public override Task<DetectionResult> DetectGamesAsync()
    {
        var result = new DetectionResult();
        
        var battleNetPath = GetBattleNetPath();
        if (string.IsNullOrEmpty(battleNetPath))
        {
            result.ErrorMessage = "Battle.net installation not found";
            return Task.FromResult(result);
        }

        result.IsInstalled = true;
        result.InstallPath = battleNetPath;

        // Try to read from product.db config
        var configGames = GetGamesFromConfig();
        foreach (var game in configGames)
        {
            if (!result.Games.Any(g => g.ExternalId == game.ExternalId))
                result.Games.Add(game);
        }

        // Fallback: scan registry for installed games
        var registryGames = GetGamesFromRegistry();
        foreach (var game in registryGames)
        {
            if (!result.Games.Any(g => g.ExternalId == game.ExternalId))
                result.Games.Add(game);
        }

        return Task.FromResult(result);
    }

    private List<DetectedGame> GetGamesFromConfig()
    {
        var games = new List<DetectedGame>();
        
        try
        {
            // Battle.net stores config in ProgramData
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Battle.net", "Agent", "product.db");

            if (!File.Exists(configPath))
            {
                // Try alternative location
                configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Blizzard Entertainment", "Battle.net", "Agent", "product.db");
            }

            if (File.Exists(configPath))
            {
                // product.db is a binary protobuf file, but we can scan for paths
                var content = File.ReadAllBytes(configPath);
                var textContent = System.Text.Encoding.UTF8.GetString(content);
                
                // Look for install paths in the binary
                foreach (var kvp in KnownGames)
                {
                    var gameCode = kvp.Key;
                    var gameName = kvp.Value;
                    
                    // Check if game code appears in config
                    if (textContent.Contains(gameCode, StringComparison.OrdinalIgnoreCase))
                    {
                        // Try to find install path from registry for this game
                        var installPath = GetGameInstallPath(gameCode, gameName);
                        if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                        {
                            var game = CreateDetectedGame(gameCode, gameName, installPath);
                            if (game != null)
                                games.Add(game);
                        }
                    }
                }
            }
        }
        catch
        {
            // Config parsing failed, will fall back to registry
        }

        return games;
    }

    private List<DetectedGame> GetGamesFromRegistry()
    {
        var games = new List<DetectedGame>();

        // Check each known game in registry
        foreach (var kvp in KnownGames)
        {
            var gameCode = kvp.Key;
            var gameName = kvp.Value;
            
            var installPath = GetGameInstallPath(gameCode, gameName);
            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
            {
                if (!games.Any(g => g.ExternalId == gameCode))
                {
                    var game = CreateDetectedGame(gameCode, gameName, installPath);
                    if (game != null)
                        games.Add(game);
                }
            }
        }

        return games;
    }

    private string? GetGameInstallPath(string gameCode, string gameName)
    {
        // Try various registry locations - by code and by name
        var registryPaths = new[]
        {
            $@"SOFTWARE\WOW6432Node\Blizzard Entertainment\{gameCode}",
            $@"SOFTWARE\Blizzard Entertainment\{gameCode}",
            $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{gameCode}",
            $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{gameName}",
        };

        foreach (var regPath in registryPaths)
        {
            var path = ReadRegistryValue(regPath, "InstallPath");
            if (!string.IsNullOrEmpty(path))
                return path;
            
            path = ReadRegistryValue(regPath, "InstallLocation");
            if (!string.IsNullOrEmpty(path))
                return path;
        }

        return null;
    }

    private DetectedGame? CreateDetectedGame(string gameCode, string gameName, string installPath)
    {
        try
        {
            if (!IsValidGameInstall(installPath))
                return null;

            var exePath = FindMainExecutable(installPath, gameCode);
            var launchCommand = $"battlenet://{gameCode}";

            return new DetectedGame
            {
                Name = gameName,
                ExternalId = gameCode,
                InstallPath = installPath,
                ExecutablePath = exePath,
                LaunchCommand = launchCommand
            };
        }
        catch
        {
            return null;
        }
    }

    private string? FindMainExecutable(string installPath, string gameCode)
    {
        try
        {
            // Known executable names for Blizzard games (as hints)
            var knownExes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "wow", new[] { "Wow.exe", "WowClassic.exe" } },
                { "wow_classic", new[] { "WowClassic.exe", "Wow.exe" } },
                { "d3", new[] { "Diablo III.exe", "Diablo III64.exe" } },
                { "fen", new[] { "Diablo IV.exe" } },
                { "pro", new[] { "Overwatch.exe" } },
                { "hs", new[] { "Hearthstone.exe" } },
                { "hero", new[] { "HeroesOfTheStorm.exe", "HeroesOfTheStorm_x64.exe" } },
                { "s2", new[] { "SC2.exe", "SC2_x64.exe", "StarCraft II.exe" } },
                { "s1", new[] { "StarCraft.exe" } },
                { "w3", new[] { "Warcraft III.exe", "war3.exe" } },
            };

            // Try known exe names first
            if (knownExes.TryGetValue(gameCode, out var exeNames))
            {
                foreach (var exeName in exeNames)
                {
                    var exePath = Path.Combine(installPath, exeName);
                    if (File.Exists(exePath))
                        return exePath;
                }
            }

            // Check root directory
            var rootExes = Directory.GetFiles(installPath, "*.exe", SearchOption.TopDirectoryOnly);
            if (rootExes.Length > 0)
            {
                var bestMatch = rootExes
                    .Where(f => !IsUtilityExe(Path.GetFileName(f)))
                    .OrderByDescending(f => new FileInfo(f).Length)
                    .FirstOrDefault();
                if (bestMatch != null)
                    return bestMatch;
            }

            // Check common subdirectories
            var subDirs = new[] { "x64", "_retail_", "bin", "Bin", "Win64", "Game", "Binaries" };
            foreach (var subDir in subDirs)
            {
                var subPath = Path.Combine(installPath, subDir);
                if (Directory.Exists(subPath))
                {
                    var subExes = Directory.GetFiles(subPath, "*.exe", SearchOption.TopDirectoryOnly);
                    var mainExe = subExes
                        .Where(f => !IsUtilityExe(Path.GetFileName(f)))
                        .OrderByDescending(f => new FileInfo(f).Length)
                        .FirstOrDefault();
                    if (mainExe != null)
                        return mainExe;
                }
            }

            // Check nested subdirectories
            var nestedSubDirs = new[]
            {
                new[] { "Game", "Bin" },
                new[] { "Game", "Bin", "x64" },
                new[] { "Game", "Bin", "Win64" },
                new[] { "Binaries", "Win64" },
                new[] { "Binaries", "Win32" },
                new[] { "_retail_", "x64" },
                new[] { "x64", "release" },
            };
            foreach (var nested in nestedSubDirs)
            {
                var subPath = Path.Combine(new[] { installPath }.Concat(nested).ToArray());
                if (Directory.Exists(subPath))
                {
                    var subExes = Directory.GetFiles(subPath, "*.exe", SearchOption.TopDirectoryOnly)
                        .Where(f => !IsUtilityExe(Path.GetFileName(f)))
                        .OrderByDescending(f => new FileInfo(f).Length)
                        .ToList();
                    if (subExes.Count > 0)
                        return subExes.First();
                }
            }

            return rootExes.FirstOrDefault();
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
            "agent", "helper", "browser", "cef"
        };
        return utilityPatterns.Any(p => lower.Contains(p));
    }

    private string? GetBattleNetPath()
    {
        // Try 32-bit registry (Battle.net is typically 32-bit)
        var path = ReadRegistryValue(BattleNetRegistryKey, "InstallPath");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        // Try 64-bit registry
        path = ReadRegistryValue(BattleNetRegistryKey64, "InstallPath");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        // Try 32-bit view explicitly
        path = ReadRegistryValue(BattleNetRegistryKey64, "InstallPath", use32BitView: true);
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        // Try HKCU
        path = ReadUserRegistryValue(BattleNetRegistryKey64, "InstallPath");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        // Common default paths
        var defaultPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Battle.net"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Battle.net"),
        };

        foreach (var defaultPath in defaultPaths)
        {
            if (Directory.Exists(defaultPath))
                return defaultPath;
        }

        return null;
    }
}
