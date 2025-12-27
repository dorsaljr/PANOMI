using Panomi.Core.Models;
using Panomi.Core.Services;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using System.Security.Cryptography;

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
    
    // MD5 hashes of known generic/ugly Windows icons to filter out
    private static readonly HashSet<string> _genericIconHashes = new(StringComparer.OrdinalIgnoreCase)
    {
        "D61BEA93E7C29C21D322C5758882D6B0", // Generic Windows executable icon (blue square)
    };

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
                
                // Check if it's a generic icon we want to filter out
                if (IsGenericIcon(cachePath))
                {
                    File.Delete(cachePath);
                    return null;
                }
                
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
    /// Check if the icon file matches a known generic/ugly Windows icon
    /// </summary>
    private static bool IsGenericIcon(string iconPath)
    {
        try
        {
            var fileBytes = File.ReadAllBytes(iconPath);
            using var md5 = MD5.Create();
            var hashBytes = md5.ComputeHash(fileBytes);
            var hashString = BitConverter.ToString(hashBytes).Replace("-", "");
            
            return _genericIconHashes.Contains(hashString);
        }
        catch
        {
            return false;
        }
    }
}
