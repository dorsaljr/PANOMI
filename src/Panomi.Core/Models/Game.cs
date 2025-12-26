namespace Panomi.Core.Models;

/// <summary>
/// Represents an installed game
/// </summary>
public class Game
{
    public int Id { get; set; }
    public int LauncherId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? InstallPath { get; set; }
    public string? ExecutablePath { get; set; }
    public string? LaunchCommand { get; set; }
    public string? IconPath { get; set; }
    public DateTime? LastPlayed { get; set; }
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Unique identifier from the launcher (e.g., Steam AppId, Epic CatalogItemId)
    /// </summary>
    public string? ExternalId { get; set; }
    
    // Navigation property
    public Launcher? Launcher { get; set; }
}
