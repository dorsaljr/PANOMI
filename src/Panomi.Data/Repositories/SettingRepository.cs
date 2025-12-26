using Microsoft.EntityFrameworkCore;
using Panomi.Core.Models;

namespace Panomi.Data.Repositories;

/// <summary>
/// Repository for Setting entities
/// </summary>
public class SettingRepository
{
    private readonly PanomiDbContext _context;

    public SettingRepository(PanomiDbContext context)
    {
        _context = context;
    }

    public async Task<string?> GetAsync(string key)
    {
        var setting = await _context.Settings.FindAsync(key);
        return setting?.Value;
    }

    public async Task SetAsync(string key, string value)
    {
        var setting = await _context.Settings.FindAsync(key);
        if (setting != null)
        {
            setting.Value = value;
            _context.Settings.Update(setting);
        }
        else
        {
            _context.Settings.Add(new Setting { Key = key, Value = value });
        }
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string key)
    {
        var setting = await _context.Settings.FindAsync(key);
        if (setting != null)
        {
            _context.Settings.Remove(setting);
            await _context.SaveChangesAsync();
        }
    }
}
