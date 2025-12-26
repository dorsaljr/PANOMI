namespace Panomi.Core.Models;

/// <summary>
/// Represents an application setting stored as key-value pair
/// </summary>
public class Setting
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}
