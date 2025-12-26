using Microsoft.UI.Xaml.Media.Imaging;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Panomi.UI.Helpers;

/// <summary>
/// Extracts icons from executable files
/// </summary>
public static class IconExtractor
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static readonly Dictionary<string, BitmapImage?> _iconCache = new();
    private static readonly object _cacheLock = new();

    /// <summary>
    /// Gets the executable path for a launcher type
    /// </summary>
    public static string? GetLauncherExePath(Panomi.Core.Models.LauncherType launcherType, string? installPath)
    {
        // Store apps (Minecraft) don't have traditional exe paths - icons handled separately
        if (launcherType == Panomi.Core.Models.LauncherType.Minecraft)
            return null;
        
        if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
            return null;

        return launcherType switch
        {
            Panomi.Core.Models.LauncherType.Steam => Path.Combine(installPath, "Steam.exe"),
            Panomi.Core.Models.LauncherType.EpicGames => FindEpicExe(installPath),
            Panomi.Core.Models.LauncherType.EAApp => FindEAAppExe(installPath),
            Panomi.Core.Models.LauncherType.UbisoftConnect => FindUbisoftExe(installPath),
            Panomi.Core.Models.LauncherType.GOGGalaxy => FindGOGExe(installPath),
            Panomi.Core.Models.LauncherType.BattleNet => FindBattleNetExe(installPath),
            Panomi.Core.Models.LauncherType.RockstarGames => FindRockstarExe(installPath),
            Panomi.Core.Models.LauncherType.RiotGames => FindRiotExe(installPath),
            _ => null
        };
    }

    private static string? FindEAAppExe(string installPath)
    {
        // EA App executable locations
        var possiblePaths = new[]
        {
            Path.Combine(installPath, "EADesktop.exe"),
            Path.Combine(installPath, "EA Desktop", "EADesktop.exe"),
            Path.Combine(installPath, "EALauncher.exe"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Fallback: search for EADesktop.exe
        try
        {
            var found = Directory.GetFiles(installPath, "EADesktop.exe", SearchOption.AllDirectories).FirstOrDefault();
            return found;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindUbisoftExe(string installPath)
    {
        // Ubisoft Connect executable locations
        var possiblePaths = new[]
        {
            Path.Combine(installPath, "UbisoftConnect.exe"),
            Path.Combine(installPath, "upc.exe"),
            Path.Combine(installPath, "Uplay.exe"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static string? FindGOGExe(string installPath)
    {
        // GOG Galaxy executable locations
        var possiblePaths = new[]
        {
            Path.Combine(installPath, "GalaxyClient.exe"),
            Path.Combine(installPath, "GOG Galaxy.exe"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static string? FindBattleNetExe(string installPath)
    {
        // Battle.net launcher executable locations
        var possiblePaths = new[]
        {
            Path.Combine(installPath, "Battle.net Launcher.exe"),
            Path.Combine(installPath, "Battle.net.exe"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static string? FindRockstarExe(string installPath)
    {
        // Rockstar Games Launcher executable locations
        var possiblePaths = new[]
        {
            Path.Combine(installPath, "Launcher.exe"),
            Path.Combine(installPath, "LauncherPatcher.exe"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static string? FindRiotExe(string installPath)
    {
        // Riot Client executable locations
        var possiblePaths = new[]
        {
            Path.Combine(installPath, "RiotClientServices.exe"),
            Path.Combine(installPath, "Riot Client.exe"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    /// <summary>
    /// Gets Spotify executable path if installed (checks multiple common locations)
    /// Returns "shell:" prefix for Store version
    /// </summary>
    public static string? GetSpotifyExePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Check standalone version first
        var standalonePath = Path.Combine(appData, "Spotify", "Spotify.exe");
        if (File.Exists(standalonePath))
            return standalonePath;

        // Check Microsoft Store version
        var storePackagePath = Path.Combine(localAppData, "Packages", "SpotifyAB.SpotifyMusic_zpdnekdrzrea0");
        if (Directory.Exists(storePackagePath))
            return "shell:AppsFolder\\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify";

        return null;
    }

    /// <summary>
    /// Gets Discord executable path if installed (checks multiple common locations)
    /// </summary>
    public static string? GetDiscordExePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        var possiblePaths = new[]
        {
            // Standard install (most common)
            Path.Combine(localAppData, "Discord", "Update.exe"),
            // Microsoft Store version
            Path.Combine(localAppData, "Microsoft", "WindowsApps", "Discord.exe"),
            // Discord PTB (Public Test Build)
            Path.Combine(localAppData, "DiscordPTB", "Update.exe"),
            // Discord Canary (Alpha)
            Path.Combine(localAppData, "DiscordCanary", "Update.exe"),
            // Rare manual/enterprise installs
            Path.Combine(programFiles, "Discord", "Discord.exe"),
            Path.Combine(programFilesX86, "Discord", "Discord.exe"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    /// <summary>
    /// Gets Discord app.ico for better icon quality (checks multiple install locations)
    /// </summary>
    public static string? GetDiscordIconPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        
        // Check standard Discord, PTB, and Canary folders
        var discordFolders = new[] { "Discord", "DiscordPTB", "DiscordCanary" };
        
        foreach (var folder in discordFolders)
        {
            var discordDir = Path.Combine(localAppData, folder);
            
            if (!Directory.Exists(discordDir))
                continue;

            try
            {
                var appFolder = Directory.GetDirectories(discordDir, "app-*")
                    .OrderByDescending(d => d)
                    .FirstOrDefault();
                
                if (appFolder != null)
                {
                    var iconPath = Path.Combine(appFolder, "resources", "app.ico");
                    if (File.Exists(iconPath))
                        return iconPath;
                    
                    var exePath = Path.Combine(appFolder, "Discord.exe");
                    if (File.Exists(exePath))
                        return exePath;
                }
            }
            catch
            {
                // Continue to next folder if access denied
            }
        }

        return GetDiscordExePath();
    }

    /// <summary>
    /// Checks if Discord is installed
    /// </summary>
    public static bool IsDiscordInstalled() => GetDiscordExePath() != null;

    private static string? FindEpicExe(string installPath)
    {
        // Epic Games Launcher exe location
        var possiblePaths = new[]
        {
            Path.Combine(installPath, "Launcher", "Portal", "Binaries", "Win64", "EpicGamesLauncher.exe"),
            Path.Combine(installPath, "Launcher", "Portal", "Binaries", "Win32", "EpicGamesLauncher.exe"),
            Path.Combine(installPath, "Launcher", "Engine", "Binaries", "Win64", "EpicGamesLauncher.exe"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Fallback: search for any EpicGamesLauncher.exe
        try
        {
            var found = Directory.GetFiles(installPath, "EpicGamesLauncher.exe", SearchOption.AllDirectories).FirstOrDefault();
            return found;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts icon from an executable and returns as BitmapImage
    /// </summary>
    public static BitmapImage? ExtractIconFromExe(string? exePath)
    {
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            return null;

        lock (_cacheLock)
        {
            if (_iconCache.TryGetValue(exePath, out var cached))
                return cached;
        }

        BitmapImage? result = null;
        IntPtr hIcon = IntPtr.Zero;

        try
        {
            hIcon = ExtractIcon(IntPtr.Zero, exePath, 0);
            if (hIcon == IntPtr.Zero || hIcon.ToInt64() == 1)
                return null;

            using var icon = Icon.FromHandle(hIcon);
            using var bitmap = icon.ToBitmap();
            using var memoryStream = new MemoryStream();
            
            bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
            memoryStream.Position = 0;

            // Create BitmapImage from stream
            result = new BitmapImage();
            
            // Copy to a new stream that can stay open
            var imageData = memoryStream.ToArray();
            var imageStream = new MemoryStream(imageData);
            imageStream.Position = 0;
            
            result.SetSource(imageStream.AsRandomAccessStream());
        }
        catch
        {
            result = null;
        }
        finally
        {
            if (hIcon != IntPtr.Zero && hIcon.ToInt64() != 1)
                DestroyIcon(hIcon);
        }

        lock (_cacheLock)
        {
            _iconCache[exePath] = result;
        }

        return result;
    }

    /// <summary>
    /// Clears the icon cache
    /// </summary>
    public static void ClearCache()
    {
        lock (_cacheLock)
        {
            _iconCache.Clear();
        }
    }
}
