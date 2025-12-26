using Microsoft.EntityFrameworkCore;
using Panomi.Core.Services;

namespace Panomi.Data.Services;

/// <summary>
/// Service implementation for managing settings
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly IDbContextFactory<PanomiDbContext> _contextFactory;

    public SettingsService(IDbContextFactory<PanomiDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var setting = await context.Settings.FindAsync(key);
        return setting?.Value;
    }

    public async Task<T> GetSettingAsync<T>(string key, T defaultValue)
    {
        var value = await GetSettingAsync(key);
        if (string.IsNullOrEmpty(value))
            return defaultValue;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    public async Task SetSettingAsync(string key, string value)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var setting = await context.Settings.FindAsync(key);
        
        if (setting == null)
        {
            context.Settings.Add(new Panomi.Core.Models.Setting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }
        
        await context.SaveChangesAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        // Clear games only - keep launchers (they're seed data)
        context.Games.RemoveRange(context.Games);
        
        // Reset launcher scan status
        var launchers = await context.Launchers.ToListAsync();
        foreach (var launcher in launchers)
        {
            launcher.IsInstalled = false;
            launcher.InstallPath = null;
            launcher.LastScanned = null;
        }
        
        await context.SaveChangesAsync();
    }
}
