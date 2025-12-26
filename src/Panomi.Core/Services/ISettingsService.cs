namespace Panomi.Core.Services;

/// <summary>
/// Service for managing application settings
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Get a setting value
    /// </summary>
    Task<string?> GetSettingAsync(string key);
    
    /// <summary>
    /// Get a setting value with a default
    /// </summary>
    Task<T> GetSettingAsync<T>(string key, T defaultValue);
    
    /// <summary>
    /// Set a setting value
    /// </summary>
    Task SetSettingAsync(string key, string value);
    
    /// <summary>
    /// Reset the database
    /// </summary>
    Task ResetDatabaseAsync();
}
