using Microsoft.EntityFrameworkCore;
using Panomi.Core.Models;

namespace Panomi.Data.Repositories;

/// <summary>
/// Repository for Launcher entities
/// </summary>
public class LauncherRepository
{
    private readonly PanomiDbContext _context;

    public LauncherRepository(PanomiDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Launcher>> GetAllAsync()
    {
        return await _context.Launchers
            .Include(l => l.Games)
            .OrderBy(l => l.Id)
            .ToListAsync();
    }

    public async Task<Launcher?> GetByIdAsync(int id)
    {
        return await _context.Launchers
            .Include(l => l.Games)
            .FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<Launcher?> GetByTypeAsync(LauncherType type)
    {
        return await _context.Launchers
            .Include(l => l.Games)
            .FirstOrDefaultAsync(l => l.Type == type);
    }

    public async Task UpdateAsync(Launcher launcher)
    {
        _context.Launchers.Update(launcher);
        await _context.SaveChangesAsync();
    }
}
