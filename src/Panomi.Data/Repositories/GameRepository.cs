using Microsoft.EntityFrameworkCore;
using Panomi.Core.Models;

namespace Panomi.Data.Repositories;

/// <summary>
/// Repository for Game entities
/// </summary>
public class GameRepository
{
    private readonly PanomiDbContext _context;

    public GameRepository(PanomiDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Game>> GetAllAsync()
    {
        return await _context.Games
            .Include(g => g.Launcher)
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Game>> GetByLauncherAsync(int launcherId)
    {
        return await _context.Games
            .Include(g => g.Launcher)
            .Where(g => g.LauncherId == launcherId)
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Game>> GetRecentAsync(int count)
    {
        return await _context.Games
            .Include(g => g.Launcher)
            .Where(g => g.LastPlayed != null)
            .OrderByDescending(g => g.LastPlayed)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<Game>> SearchAsync(string searchText)
    {
        var search = searchText.ToLowerInvariant();
        return await _context.Games
            .Include(g => g.Launcher)
            .Where(g => g.Name.ToLower().Contains(search))
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    public async Task<Game?> GetByIdAsync(int id)
    {
        return await _context.Games
            .Include(g => g.Launcher)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<Game?> GetByExternalIdAsync(int launcherId, string externalId)
    {
        return await _context.Games
            .FirstOrDefaultAsync(g => g.LauncherId == launcherId && g.ExternalId == externalId);
    }

    public async Task<Game> AddAsync(Game game)
    {
        _context.Games.Add(game);
        await _context.SaveChangesAsync();
        return game;
    }

    public async Task UpdateAsync(Game game)
    {
        _context.Games.Update(game);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var game = await _context.Games.FindAsync(id);
        if (game != null)
        {
            _context.Games.Remove(game);
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteByLauncherAsync(int launcherId)
    {
        var games = await _context.Games.Where(g => g.LauncherId == launcherId).ToListAsync();
        _context.Games.RemoveRange(games);
        await _context.SaveChangesAsync();
    }

    public async Task AddOrUpdateAsync(Game game)
    {
        var existing = await GetByExternalIdAsync(game.LauncherId, game.ExternalId ?? string.Empty);
        if (existing != null)
        {
            existing.Name = game.Name;
            existing.InstallPath = game.InstallPath;
            existing.ExecutablePath = game.ExecutablePath;
            existing.LaunchCommand = game.LaunchCommand;
            // Don't update IconPath if we already have one
            if (string.IsNullOrEmpty(existing.IconPath))
            {
                existing.IconPath = game.IconPath;
            }
            await UpdateAsync(existing);
        }
        else
        {
            await AddAsync(game);
        }
    }
}
