using Panomi.Core.Models;
using Panomi.Core.Services;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Panomi.Core.Implementations;

/// <summary>
/// Service for extracting and caching icons from installed executables
/// Uses Windows Shell API via Icon.ExtractAssociatedIcon for stability
/// </summary>
[SupportedOSPlatform("windows")]
public class IconService : IIconService
{
    private readonly string _iconsFolderPath;
    private const int IconSize = 256; // Best quality for modern displays
    
    // P/Invoke for checking if exe has embedded icons
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, int nIcons);
    
    /// <summary>
    /// Checks if an executable has embedded icon resources.
    /// Returns false if the exe has no icons (Windows would show default blue icon).
    /// </summary>
    private static bool HasEmbeddedIcon(string executablePath)
    {
        try
        {
            // ExtractIconEx with nIconIndex = -1 returns total icon count
            int iconCount = ExtractIconEx(executablePath, -1, null!, null!, 0);
            return iconCount > 0;
        }
        catch
        {
            return false;
        }
    }

    public string IconsFolderPath => _iconsFolderPath;

    public IconService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _iconsFolderPath = Path.Combine(appDataPath, "Panomi", "Icons");
        EnsureIconsFolderExists();
    }

    public IconService(string iconsFolderPath)
    {
        _iconsFolderPath = iconsFolderPath;
        EnsureIconsFolderExists();
    }

    public void EnsureIconsFolderExists()
    {
        if (!Directory.Exists(_iconsFolderPath))
        {
            Directory.CreateDirectory(_iconsFolderPath);
        }
    }

    public string? GetIconPath(string gameName)
    {
        if (string.IsNullOrEmpty(gameName))
            return null;

        var sanitizedName = SanitizeFileName(gameName);
        var iconPath = Path.Combine(_iconsFolderPath, sanitizedName + ".png");
        
        return File.Exists(iconPath) ? iconPath : null;
    }

    public string? GetIconPath(int gameId)
    {
        var iconPath = Path.Combine(_iconsFolderPath, $"game_{gameId}.png");
        return File.Exists(iconPath) ? iconPath : null;
    }

    public bool HasIcon(string gameName)
    {
        return !string.IsNullOrEmpty(GetIconPath(gameName));
    }

    public string? GetLauncherIconPath(LauncherType launcherType)
    {
        var iconPath = Path.Combine(_iconsFolderPath, $"launcher_{launcherType}.png");
        return File.Exists(iconPath) ? iconPath : null;
    }

    public string? ExtractAndCacheIcon(string executablePath, LauncherType launcherType, string externalId)
    {
        if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
            return null;

        var cacheFileName = $"{launcherType}_{SanitizeFileName(externalId)}.png";
        var cachePath = Path.Combine(_iconsFolderPath, cacheFileName);

        // Return cached icon if it exists
        if (File.Exists(cachePath))
            return cachePath;
        
        // Check if the executable has embedded icon resources
        // If not, Windows would return the default blue icon - skip and use placeholder
        if (!HasEmbeddedIcon(executablePath))
            return null;

        // Ensure folder exists
        EnsureIconsFolderExists();

        // Extract and cache the icon
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(executablePath);
            if (icon == null)
                return null;

            // Convert to bitmap and save as PNG for best quality
            using var bitmap = ExtractLargestIcon(icon);
            if (bitmap != null)
            {
                bitmap.Save(cachePath, ImageFormat.Png);
                return cachePath;
            }
        }
        catch
        {
            // Extraction failed - return null (UI will show text name instead)
        }

        return null;
    }

    public string? ExtractLauncherIcon(string launcherExecutablePath, LauncherType launcherType)
    {
        if (string.IsNullOrEmpty(launcherExecutablePath) || !File.Exists(launcherExecutablePath))
            return null;

        var cacheFileName = $"launcher_{launcherType}.png";
        var cachePath = Path.Combine(_iconsFolderPath, cacheFileName);

        // Return cached icon if it exists
        if (File.Exists(cachePath))
            return cachePath;

        // Ensure folder exists
        EnsureIconsFolderExists();

        // Extract and cache the icon
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(launcherExecutablePath);
            if (icon == null)
                return null;

            using var bitmap = ExtractLargestIcon(icon);
            if (bitmap != null)
            {
                bitmap.Save(cachePath, ImageFormat.Png);
                return cachePath;
            }
        }
        catch
        {
            // Extraction failed - return null (UI will show launcher name instead)
        }

        return null;
    }

    /// <summary>
    /// Extract icon as bitmap for saving
    /// </summary>
    private static Bitmap? ExtractLargestIcon(Icon icon)
    {
        try
        {
            return icon.ToBitmap();
        }
        catch
        {
            return null;
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Where(c => !invalidChars.Contains(c))
            .ToArray());
        
        return sanitized.Trim();
    }
    
    /// <summary>
    /// Search for .ico files in game install folder as fallback
    /// </summary>
    public string? ExtractIconFromFolder(string installPath, LauncherType launcherType, string externalId)
    {
        if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
            return null;
        
        var cacheFileName = $"{launcherType}_{SanitizeFileName(externalId)}_ico.png";
        var cachePath = Path.Combine(_iconsFolderPath, cacheFileName);
        
        // Return cached icon if it exists
        if (File.Exists(cachePath))
            return cachePath;
        
        try
        {
            // Search for .ico files in root folder
            var icoFiles = Directory.GetFiles(installPath, "*.ico", SearchOption.TopDirectoryOnly);
            
            if (icoFiles.Length == 0)
                return null;
            
            // Prefer common icon names
            var preferredNames = new[] { "icon", "game", "app", "logo" };
            var icoFile = icoFiles.FirstOrDefault(f => 
                preferredNames.Any(p => Path.GetFileNameWithoutExtension(f).ToLowerInvariant().Contains(p)))
                ?? icoFiles[0];
            
            EnsureIconsFolderExists();
            
            // Load .ico and save as PNG
            using var icon = new Icon(icoFile, 256, 256);
            using var bitmap = icon.ToBitmap();
            bitmap.Save(cachePath, ImageFormat.Png);
            return cachePath;
        }
        catch
        {
            return null;
        }
    }
}
