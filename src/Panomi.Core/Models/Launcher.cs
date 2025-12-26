namespace Panomi.Core.Models;

/// <summary>
/// Represents a game launcher (Steam, Epic, etc.)
/// </summary>
public class Launcher
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? InstallPath { get; set; }
    public bool IsInstalled { get; set; }
    public DateTime? LastScanned { get; set; }
    public LauncherType Type { get; set; }
    
    // Navigation property
    public ICollection<Game> Games { get; set; } = new List<Game>();
}

/// <summary>
/// Enumeration of supported launcher types
/// </summary>
public enum LauncherType
{
    Steam = 1,
    EpicGames = 2,
    EAApp = 3,
    UbisoftConnect = 4,
    GOGGalaxy = 5,
    BattleNet = 6,
    RockstarGames = 7,
    RiotGames = 8,
    Minecraft = 9,
    Roblox = 10,
    Manual = 99
}
