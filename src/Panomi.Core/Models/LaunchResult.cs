namespace Panomi.Core.Models;

/// <summary>
/// Result of a game or launcher launch attempt (for internal tracking only)
/// </summary>
public class LaunchResult
{
    public bool Success { get; init; }
    public LaunchError Error { get; init; } = LaunchError.None;
    public string? Details { get; init; }
    
    public static LaunchResult Succeeded() => new() { Success = true };
    
    public static LaunchResult Failed(LaunchError error, string? details = null) 
        => new() { Success = false, Error = error, Details = details };
}

/// <summary>
/// Types of launch errors (for internal logging)
/// </summary>
public enum LaunchError
{
    None,
    GameNotFound,
    ExecutableNotFound,
    LauncherNotInstalled,
    PermissionDenied,
    InvalidCommand,
    ProcessStartFailed
}
