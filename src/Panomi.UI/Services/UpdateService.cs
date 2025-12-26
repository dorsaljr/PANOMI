using System.Diagnostics;
using Velopack;
using Velopack.Sources;

namespace Panomi.UI.Services;

/// <summary>
/// Handles automatic updates via GitHub Releases using Velopack.
/// Silent by default - no UI interruption unless update ready to apply.
/// </summary>
public static class UpdateService
{
    // TODO: Replace with your actual GitHub repo
    private const string GitHubRepoUrl = "https://github.com/panodev/panomi";
    
    private static UpdateManager? _updateManager;
    private static UpdateInfo? _pendingUpdate;

    /// <summary>
    /// Initialize Velopack on app startup (call from Main/App constructor)
    /// </summary>
    public static void Initialize()
    {
        try
        {
            VelopackApp.Build().Run();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Panomi] Velopack init error: {ex.Message}");
        }
    }

    /// <summary>
    /// Check for updates silently in background. Does not block UI.
    /// </summary>
    public static async Task CheckForUpdatesAsync()
    {
        try
        {
            var source = new GithubSource(GitHubRepoUrl, null, false);
            _updateManager = new UpdateManager(source);

            // Check if we're running an installed version (not debug)
            if (!_updateManager.IsInstalled)
            {
                Debug.WriteLine("[Panomi] Not installed via Velopack, skipping update check");
                return;
            }

            _pendingUpdate = await _updateManager.CheckForUpdatesAsync();
            
            if (_pendingUpdate != null)
            {
                Debug.WriteLine($"[Panomi] Update available: {_pendingUpdate.TargetFullRelease.Version}");
                
                // Download in background
                await _updateManager.DownloadUpdatesAsync(_pendingUpdate);
                Debug.WriteLine("[Panomi] Update downloaded, ready to apply on next restart");
            }
            else
            {
                Debug.WriteLine("[Panomi] No updates available");
            }
        }
        catch (Exception ex)
        {
            // Silent failure - don't interrupt user
            Debug.WriteLine($"[Panomi] Update check failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns true if an update has been downloaded and is ready to install
    /// </summary>
    public static bool HasPendingUpdate => _pendingUpdate != null;

    /// <summary>
    /// Get the version of the pending update, if any
    /// </summary>
    public static string? PendingVersion => _pendingUpdate?.TargetFullRelease.Version.ToString();

    /// <summary>
    /// Apply the pending update and restart the app
    /// </summary>
    public static void ApplyUpdateAndRestart()
    {
        if (_updateManager != null && _pendingUpdate != null)
        {
            try
            {
                _updateManager.ApplyUpdatesAndRestart(_pendingUpdate);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Panomi] Failed to apply update: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Apply update on app exit (no restart)
    /// </summary>
    public static void ApplyUpdateOnExit()
    {
        if (_updateManager != null && _pendingUpdate != null)
        {
            try
            {
                _updateManager.ApplyUpdatesAndExit(_pendingUpdate);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Panomi] Failed to apply update on exit: {ex.Message}");
            }
        }
    }
}
