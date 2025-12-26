using Panomi.Core.Models;

namespace Panomi.Detection.Detectors;

/// <summary>
/// Detector for Riot Games (League of Legends, Valorant, etc.)
/// </summary>
public class RiotDetector : BaseLauncherDetector
{
    public override LauncherType LauncherType => LauncherType.RiotGames;
    public override string LauncherName => "Riot";

    // Known Riot games and their folder names
    private static readonly Dictionary<string, string> KnownGames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "League of Legends", "League of Legends" },
        { "VALORANT", "Valorant" },
        { "LoR", "Legends of Runeterra" },
    };

    public override Task<bool> IsInstalledAsync()
    {
        var installPath = GetRiotClientPath();
        return Task.FromResult(!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath));
    }

    public override Task<string?> GetInstallPathAsync()
    {
        return Task.FromResult(GetRiotClientPath());
    }

    public override Task<DetectionResult> DetectGamesAsync()
    {
        var result = new DetectionResult();

        var riotPath = GetRiotClientPath();
        if (string.IsNullOrEmpty(riotPath))
        {
            result.ErrorMessage = "Riot Client not found";
            return Task.FromResult(result);
        }

        result.IsInstalled = true;
        result.InstallPath = riotPath;

        // Scan for installed Riot games
        var games = GetInstalledGames();
        foreach (var game in games)
        {
            result.Games.Add(game);
        }

        return Task.FromResult(result);
    }

    private string? GetRiotClientPath()
    {
        // Check registry for Riot Client (HKCU)
        var path = ReadUserRegistryValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Riot Game Riot_Client.", "InstallLocation");
        if (!string.IsNullOrEmpty(path))
        {
            path = path.Replace("/", "\\");
            if (Directory.Exists(path))
                return path;
        }

        // Try common install location
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var riotClientPath = Path.Combine(programData, "Riot Games", "RiotClientInstalls.json");
        if (File.Exists(riotClientPath))
        {
            // Parse JSON to find client path
            try
            {
                var json = File.ReadAllText(riotClientPath);
                // Simple extraction - look for rc_live path
                var match = System.Text.RegularExpressions.Regex.Match(json, @"""rc_live""\s*:\s*""([^""]+)""");
                if (match.Success)
                {
                    var clientExe = match.Groups[1].Value.Replace(@"\\", @"\");
                    var clientDir = Path.GetDirectoryName(clientExe);
                    if (!string.IsNullOrEmpty(clientDir) && Directory.Exists(clientDir))
                        return clientDir;
                }
                
                // Try rc_beta if rc_live not found
                match = System.Text.RegularExpressions.Regex.Match(json, @"""rc_beta""\s*:\s*""([^""]+)""");
                if (match.Success)
                {
                    var clientExe = match.Groups[1].Value.Replace(@"\\", @"\");
                    var clientDir = Path.GetDirectoryName(clientExe);
                    if (!string.IsNullOrEmpty(clientDir) && Directory.Exists(clientDir))
                        return clientDir;
                }
            }
            catch { }
        }

        return null;
    }

    private string? GetRiotClientExePath()
    {
        var clientPath = GetRiotClientPath();
        if (string.IsNullOrEmpty(clientPath))
            return null;

        var exePath = Path.Combine(clientPath, "RiotClientServices.exe");
        return File.Exists(exePath) ? exePath : null;
    }

    private List<DetectedGame> GetInstalledGames()
    {
        var games = new List<DetectedGame>();

        // Check registry for each Riot game
        var lolGame = DetectLeagueOfLegends();
        if (lolGame != null) games.Add(lolGame);

        var valorantGame = DetectValorant();
        if (valorantGame != null) games.Add(valorantGame);

        var lorGame = DetectLegendsOfRuneterra();
        if (lorGame != null) games.Add(lorGame);

        return games;
    }

    private DetectedGame? DetectLeagueOfLegends()
    {
        // Check registry (Riot uses HKCU, not HKLM)
        var installPath = ReadUserRegistryValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Riot Game league_of_legends.live", "InstallLocation");
        
        // Riot uses forward slashes in registry, normalize to backslashes
        if (!string.IsNullOrEmpty(installPath))
            installPath = installPath.Replace("/", "\\");
        
        if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
            return null;

        var exePath = Path.Combine(installPath, "LeagueClient.exe");
        if (!File.Exists(exePath))
            return null;

        // Get Riot Client path for launch command
        var riotClientPath = GetRiotClientExePath();
        var launchCommand = !string.IsNullOrEmpty(riotClientPath)
            ? $"\"{riotClientPath}\" --launch-product=league_of_legends --launch-patchline=live"
            : exePath;

        return new DetectedGame
        {
            Name = "League of Legends",
            ExternalId = "league_of_legends",
            InstallPath = installPath,
            ExecutablePath = exePath,
            LaunchCommand = launchCommand
        };
    }

    private DetectedGame? DetectValorant()
    {
        // Check registry (Riot uses HKCU, not HKLM)
        var installPath = ReadUserRegistryValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Riot Game valorant.live", "InstallLocation");
        
        // Riot uses forward slashes in registry, normalize to backslashes
        if (!string.IsNullOrEmpty(installPath))
            installPath = installPath.Replace("/", "\\");
        
        if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
            return null;

        // Valorant uses a special launcher
        var exePath = Path.Combine(installPath, "live", "VALORANT.exe");
        if (!File.Exists(exePath))
        {
            exePath = Path.Combine(installPath, "VALORANT.exe");
        }

        if (!File.Exists(exePath))
            return null;

        // Get Riot Client path for launch command
        var riotClientPath = GetRiotClientExePath();
        var launchCommand = !string.IsNullOrEmpty(riotClientPath)
            ? $"\"{riotClientPath}\" --launch-product=valorant --launch-patchline=live"
            : exePath;

        return new DetectedGame
        {
            Name = "Valorant",
            ExternalId = "valorant",
            InstallPath = installPath,
            ExecutablePath = exePath,
            LaunchCommand = launchCommand
        };
    }

    private DetectedGame? DetectLegendsOfRuneterra()
    {
        // Check registry (Riot uses HKCU, not HKLM)
        var installPath = ReadUserRegistryValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Riot Game bacon.live", "InstallLocation");
        
        // Riot uses forward slashes in registry, normalize to backslashes
        if (!string.IsNullOrEmpty(installPath))
            installPath = installPath.Replace("/", "\\");
        
        if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
            return null;

        var exePath = Path.Combine(installPath, "LoR.exe");
        if (!File.Exists(exePath))
        {
            // Alternative name
            exePath = Path.Combine(installPath, "Legends of Runeterra.exe");
        }

        if (!File.Exists(exePath))
            return null;

        // Get Riot Client path for launch command
        var riotClientPath = GetRiotClientExePath();
        var launchCommand = !string.IsNullOrEmpty(riotClientPath)
            ? $"\"{riotClientPath}\" --launch-product=bacon --launch-patchline=live"
            : exePath;

        return new DetectedGame
        {
            Name = "Legends of Runeterra",
            ExternalId = "legends_of_runeterra",
            InstallPath = installPath,
            ExecutablePath = exePath,
            LaunchCommand = launchCommand
        };
    }
}
