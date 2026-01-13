using Microsoft.EntityFrameworkCore;
using Panomi.Core.Interfaces;
using Panomi.Core.Models;
using Panomi.Core.Services;
using Panomi.Detection;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Panomi.Data.Services;

/// <summary>
/// Service implementation for managing games
/// </summary>
public class GameService : IGameService
{
    private readonly IDbContextFactory<PanomiDbContext> _contextFactory;
    private readonly LauncherDetectorFactory _detectorFactory;
    private readonly IIconService _iconService;

    public GameService(
        IDbContextFactory<PanomiDbContext> contextFactory,
        LauncherDetectorFactory detectorFactory,
        IIconService iconService)
    {
        _contextFactory = contextFactory;
        _detectorFactory = detectorFactory;
        _iconService = iconService;
    }

    public async Task<IEnumerable<Game>> GetAllGamesAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var games = await context.Games.Include(g => g.Launcher).ToListAsync();
        
        foreach (var game in games)
        {
            if (string.IsNullOrEmpty(game.IconPath))
            {
                game.IconPath = _iconService.GetIconPath(game.Name) ?? _iconService.GetIconPath(game.Id);
            }
        }
        
        return games;
    }

    public async Task<IEnumerable<Game>> GetGamesByLauncherAsync(int launcherId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Games.Where(g => g.LauncherId == launcherId).ToListAsync();
    }

    public async Task<IEnumerable<Game>> GetRecentGamesAsync(int count = 10)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Games
            .Where(g => g.LastPlayed.HasValue)
            .OrderByDescending(g => g.LastPlayed)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<Game>> SearchGamesAsync(string searchText)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var lowerSearch = searchText.ToLowerInvariant();
        return await context.Games
            .Where(g => g.Name.ToLower().Contains(lowerSearch))
            .ToListAsync();
    }

    public async Task<Game?> GetGameByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Games.Include(g => g.Launcher).FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<Game> AddGameAsync(Game game)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        game.DateAdded = DateTime.UtcNow;
        context.Games.Add(game);
        await context.SaveChangesAsync();
        return game;
    }

    public async Task UpdateGameAsync(Game game)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.Games.Update(game);
        await context.SaveChangesAsync();
    }

    public async Task DeleteGameAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var game = await context.Games.FindAsync(id);
        if (game != null)
        {
            context.Games.Remove(game);
            await context.SaveChangesAsync();
        }
    }

    public async Task LaunchGameAsync(int gameId)
    {
        await TryLaunchGameAsync(gameId);
    }

    public async Task<LaunchResult> TryLaunchGameAsync(int gameId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var game = await context.Games.Include(g => g.Launcher).FirstOrDefaultAsync(g => g.Id == gameId);
        
        if (game == null)
        {
            Debug.WriteLine($"[Panomi] Launch failed: Game ID {gameId} not found");
            return LaunchResult.Failed(LaunchError.GameNotFound);
        }

        game.LastPlayed = DateTime.UtcNow;
        await context.SaveChangesAsync();

        // Try launch command first
        if (IsValidLaunchCommand(game.LaunchCommand))
        {
            var result = TryLaunchCommand(game.LaunchCommand!, game.Launcher?.Name);
            if (result.Success)
                return result;
            // Fall through to try executable path
        }

        // Fallback to executable path
        return TryLaunchExecutablePath(game.ExecutablePath, game.Launcher?.Name);
    }

    private LaunchResult TryLaunchCommand(string command, string? launcherName)
    {
        try
        {
            var (fileName, arguments) = ParseLaunchCommand(command);
            
            // Check if it's a URI scheme
            var colonIndex = fileName.IndexOf(':');
            if (colonIndex > 0)
            {
                var scheme = fileName.Substring(0, colonIndex);
                if (AllowedSchemes.Contains(scheme))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        UseShellExecute = true
                    });
                    return LaunchResult.Succeeded();
                }
            }
            
            // It's a file path - verify it exists
            var cleanPath = fileName.Trim('"');
            if (!File.Exists(cleanPath))
            {
                Debug.WriteLine($"[Panomi] Launch failed: Executable not found at {cleanPath}");
                return LaunchResult.Failed(LaunchError.ExecutableNotFound, cleanPath);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            });
            return LaunchResult.Succeeded();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5) // ACCESS_DENIED
        {
            Debug.WriteLine($"[Panomi] Launch failed: Permission denied - {command}");
            return LaunchResult.Failed(LaunchError.PermissionDenied, command);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2 || ex.NativeErrorCode == 3) // FILE_NOT_FOUND, PATH_NOT_FOUND
        {
            Debug.WriteLine($"[Panomi] Launch failed: File not found - {command}");
            return LaunchResult.Failed(LaunchError.ExecutableNotFound, command);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1155) // No app associated
        {
            Debug.WriteLine($"[Panomi] Launch failed: Launcher not installed - {launcherName}");
            return LaunchResult.Failed(LaunchError.LauncherNotInstalled, launcherName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Panomi] Launch failed: {ex.Message}");
            return LaunchResult.Failed(LaunchError.ProcessStartFailed, ex.Message);
        }
    }

    private LaunchResult TryLaunchExecutablePath(string? executablePath, string? launcherName)
    {
        if (string.IsNullOrEmpty(executablePath))
        {
            Debug.WriteLine("[Panomi] Launch failed: No executable path configured");
            return LaunchResult.Failed(LaunchError.InvalidCommand);
        }

        if (!File.Exists(executablePath))
        {
            Debug.WriteLine($"[Panomi] Launch failed: Executable not found at {executablePath}");
            return LaunchResult.Failed(LaunchError.ExecutableNotFound, executablePath);
        }

        if (!IsValidFilePath(executablePath))
        {
            Debug.WriteLine($"[Panomi] Launch failed: Invalid path - {executablePath}");
            return LaunchResult.Failed(LaunchError.InvalidCommand, executablePath);
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(executablePath)
            });
            return LaunchResult.Succeeded();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            Debug.WriteLine($"[Panomi] Launch failed: Permission denied - {executablePath}");
            return LaunchResult.Failed(LaunchError.PermissionDenied, executablePath);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2 || ex.NativeErrorCode == 3)
        {
            Debug.WriteLine($"[Panomi] Launch failed: File not found - {executablePath}");
            return LaunchResult.Failed(LaunchError.ExecutableNotFound, executablePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Panomi] Launch failed: {ex.Message}");
            return LaunchResult.Failed(LaunchError.ProcessStartFailed, ex.Message);
        }
    }

    private static (string fileName, string arguments) ParseLaunchCommand(string command)
    {
        // Handle quoted executable path with arguments
        // e.g., "C:\Path\To\Exe.exe" --arg1 --arg2
        if (command.StartsWith("\""))
        {
            var endQuote = command.IndexOf("\"", 1);
            if (endQuote > 0)
            {
                var fileName = command.Substring(1, endQuote - 1);
                var arguments = endQuote + 1 < command.Length 
                    ? command.Substring(endQuote + 1).TrimStart() 
                    : "";
                return (fileName, arguments);
            }
        }
        
        // No quotes - could be URI or plain path
        return (command, "");
    }

    // Allowed URI schemes for game launchers
    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "steam", "com.epicgames.launcher", "uplay", "origin", "origin2", 
        "battlenet", "gog", "rockstar", "riotclient", "minecraft", "roblox", "roblox-player"
    };

    // Dangerous executables that should never be launched
    private static readonly HashSet<string> DangerousExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd", "cmd.exe", "powershell", "powershell.exe", "pwsh", "pwsh.exe",
        "wscript", "cscript", "mshta", "rundll32", "regsvr32", "certutil", "bitsadmin"
    };

    private static bool IsValidLaunchCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        // Check if it's an allowed URI scheme
        var colonIndex = command.IndexOf(':');
        if (colonIndex > 0)
        {
            var scheme = command.Substring(0, colonIndex);
            if (AllowedSchemes.Contains(scheme))
                return true;
        }

        return IsValidFilePath(command);
    }

    private static bool IsValidFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            // Handle quoted paths
            var cleanPath = path.Trim();
            if (cleanPath.StartsWith("\""))
            {
                var endQuote = cleanPath.IndexOf("\"", 1);
                if (endQuote > 1)
                    cleanPath = cleanPath.Substring(1, endQuote - 1);
            }

            var fileName = Path.GetFileName(cleanPath);
            
            // Block dangerous executables
            if (DangerousExecutables.Contains(fileName))
                return false;

            // Block Windows system directory
            var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrEmpty(systemRoot) && cleanPath.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase))
                return false;

            return File.Exists(cleanPath);
        }
        catch
        {
            return false;
        }
    }

    public async Task ScanAllLaunchersAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var launchers = await context.Launchers.ToListAsync();
        
        foreach (var launcher in launchers.Where(l => l.Type != LauncherType.Manual))
        {
            var detector = _detectorFactory.GetDetector(launcher.Type);
            if (detector == null) continue;

            var result = await detector.DetectGamesAsync();
            
            launcher.IsInstalled = result.IsInstalled;
            launcher.InstallPath = result.InstallPath;
            launcher.LastScanned = DateTime.UtcNow;

            // Extract launcher icon if we have an install path
            if (result.IsInstalled && !string.IsNullOrEmpty(result.InstallPath))
            {
                var launcherExe = GetLauncherExecutable(launcher.Type, result.InstallPath);
                if (!string.IsNullOrEmpty(launcherExe))
                {
                    _iconService.ExtractLauncherIcon(launcherExe, launcher.Type);
                }
            }

            // Get all existing games for this launcher (excluding manual games)
            var existingGames = await context.Games
                .Where(g => g.LauncherId == launcher.Id)
                .ToListAsync();

            // Track which games were detected in this scan
            var detectedExternalIds = new HashSet<string>();

            if (result.IsInstalled)
            {
                foreach (var detectedGame in result.Games)
                {
                    if (!string.IsNullOrEmpty(detectedGame.ExternalId))
                    {
                        detectedExternalIds.Add(detectedGame.ExternalId);
                    }

                    var existingGame = existingGames
                        .FirstOrDefault(g => g.ExternalId == detectedGame.ExternalId);

                    // Extract game icon
                    string? iconPath = null;
                    if (!string.IsNullOrEmpty(detectedGame.ExternalId))
                    {
                        // Check for .ico files in game folder (developer-provided icons are reliable)
                        if (!string.IsNullOrEmpty(detectedGame.InstallPath))
                        {
                            iconPath = _iconService.ExtractIconFromFolder(
                                detectedGame.InstallPath,
                                launcher.Type,
                                detectedGame.ExternalId);
                        }
                        
                        // Fallback: exe extraction (can sometimes return generic Windows icon)
                        if (iconPath == null && !string.IsNullOrEmpty(detectedGame.ExecutablePath))
                        {
                            iconPath = _iconService.ExtractAndCacheIcon(
                                detectedGame.ExecutablePath, 
                                launcher.Type, 
                                detectedGame.ExternalId);
                        }
                    }

                    if (existingGame == null)
                    {
                        var game = new Game
                        {
                            LauncherId = launcher.Id,
                            Name = detectedGame.Name,
                            InstallPath = detectedGame.InstallPath,
                            ExecutablePath = detectedGame.ExecutablePath,
                            LaunchCommand = detectedGame.LaunchCommand,
                            ExternalId = detectedGame.ExternalId,
                            IconPath = iconPath,
                            DateAdded = DateTime.UtcNow
                        };
                        context.Games.Add(game);
                    }
                    else
                    {
                        existingGame.Name = detectedGame.Name;
                        existingGame.InstallPath = detectedGame.InstallPath;
                        existingGame.ExecutablePath = detectedGame.ExecutablePath;
                        existingGame.LaunchCommand = detectedGame.LaunchCommand;
                        if (iconPath != null)
                        {
                            existingGame.IconPath = iconPath;
                        }
                    }
                }
            }

            // Remove games that are no longer detected (uninstalled)
            // Only remove auto-detected games (those with ExternalId), not manually added ones
            var gamesToRemove = existingGames
                .Where(g => !string.IsNullOrEmpty(g.ExternalId) && !detectedExternalIds.Contains(g.ExternalId))
                .ToList();

            if (gamesToRemove.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[Scan] Removing {gamesToRemove.Count} uninstalled game(s) from {launcher.Name}");
                foreach (var game in gamesToRemove)
                {
                    System.Diagnostics.Debug.WriteLine($"[Scan] - Removing: {game.Name}");
                }
                context.Games.RemoveRange(gamesToRemove);
            }
        }
        
        await context.SaveChangesAsync();
        
        // Deduplicate games that have the same executable path across different launchers
        // This handles cases like RDR2 (Steam + Rockstar) or R6 Siege (Steam + Ubisoft)
        await DeduplicateGamesByExecutableAsync();
    }
    
    /// <summary>
    /// Removes duplicate games that share the same executable path across launchers.
    /// Prefers Steam version when duplicates are found, otherwise keeps first detected.
    /// </summary>
    private async Task DeduplicateGamesByExecutableAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        // Get all games with their launcher info
        var allGames = await context.Games
            .Include(g => g.Launcher)
            .Where(g => !string.IsNullOrEmpty(g.ExecutablePath))
            .ToListAsync();
        
        // Group by normalized executable path (case-insensitive)
        var duplicateGroups = allGames
            .GroupBy(g => g.ExecutablePath!.ToLowerInvariant().Trim())
            .Where(group => group.Count() > 1)
            .ToList();
        
        if (duplicateGroups.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("[Dedupe] No duplicate games found");
            return;
        }
        
        var gamesToRemove = new List<Game>();
        
        foreach (var group in duplicateGroups)
        {
            var games = group.ToList();
            
            // Priority: 1. Steam wins, 2. Newest DateAdded (freshest detection)
            var steamGame = games.FirstOrDefault(g => g.Launcher?.Type == LauncherType.Steam);
            var gameToKeep = steamGame ?? games.OrderByDescending(g => g.DateAdded).First();
            
            // Mark all others for removal
            var duplicates = games.Where(g => g.Id != gameToKeep.Id).ToList();
            
            System.Diagnostics.Debug.WriteLine($"[Dedupe] Found {games.Count} entries for '{gameToKeep.Name}'");
            System.Diagnostics.Debug.WriteLine($"[Dedupe] - Keeping: {gameToKeep.Launcher?.Name} version");
            foreach (var dupe in duplicates)
            {
                System.Diagnostics.Debug.WriteLine($"[Dedupe] - Removing: {dupe.Launcher?.Name} version");
            }
            
            gamesToRemove.AddRange(duplicates);
        }
        
        if (gamesToRemove.Count > 0)
        {
            context.Games.RemoveRange(gamesToRemove);
            await context.SaveChangesAsync();
            System.Diagnostics.Debug.WriteLine($"[Dedupe] Removed {gamesToRemove.Count} duplicate game(s)");
        }
    }

    /// <summary>
    /// Get the main executable for a launcher based on its type and install path
    /// </summary>
    private static string? GetLauncherExecutable(LauncherType type, string installPath)
    {
        var exeName = type switch
        {
            LauncherType.Steam => "steam.exe",
            LauncherType.EpicGames => "EpicGamesLauncher.exe",
            LauncherType.EAApp => "EADesktop.exe",
            LauncherType.UbisoftConnect => "upc.exe",
            LauncherType.GOGGalaxy => "GalaxyClient.exe",
            LauncherType.BattleNet => "Battle.net.exe",
            LauncherType.RockstarGames => "Launcher.exe",
            LauncherType.RiotGames => "RiotClientServices.exe",
            _ => null
        };

        if (exeName == null) return null;

        // Try direct path
        var directPath = Path.Combine(installPath, exeName);
        if (File.Exists(directPath)) return directPath;

        // Try searching in subdirectories for common patterns
        var searchPaths = new[]
        {
            Path.Combine(installPath, "Portal", exeName),
            Path.Combine(installPath, "Portal", "Binaries", "Win64", exeName),
            Path.Combine(installPath, "Portal", "Binaries", "Win32", exeName),
            Path.Combine(installPath, "Engine", "Binaries", "Win64", exeName),
            // Epic Games has Launcher subfolder
            Path.Combine(installPath, "Launcher", "Portal", "Binaries", "Win64", exeName),
            Path.Combine(installPath, "Launcher", "Portal", "Binaries", "Win32", exeName),
            Path.Combine(installPath, "Launcher", "Engine", "Binaries", "Win64", exeName),
        };

        foreach (var searchPath in searchPaths)
        {
            if (File.Exists(searchPath)) return searchPath;
        }

        return null;
    }
}
