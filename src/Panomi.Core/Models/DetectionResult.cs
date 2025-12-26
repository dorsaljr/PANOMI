namespace Panomi.Core.Models;

/// <summary>
/// Result of a launcher detection scan
/// </summary>
public class DetectionResult
{
    public bool IsInstalled { get; set; }
    public string? InstallPath { get; set; }
    public List<DetectedGame> Games { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// A game detected during a launcher scan
/// </summary>
public class DetectedGame
{
    public string Name { get; set; } = string.Empty;
    public string? InstallPath { get; set; }
    public string? ExecutablePath { get; set; }
    public string? LaunchCommand { get; set; }
    public string? ExternalId { get; set; }
}
