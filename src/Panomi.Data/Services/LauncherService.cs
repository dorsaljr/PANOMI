using Microsoft.EntityFrameworkCore;
using Panomi.Core.Models;
using Panomi.Core.Services;
using Panomi.Detection;

namespace Panomi.Data.Services;

/// <summary>
/// Service implementation for managing launchers
/// </summary>
public class LauncherService : ILauncherService
{
    private readonly IDbContextFactory<PanomiDbContext> _contextFactory;
    private readonly LauncherDetectorFactory _detectorFactory;

    public LauncherService(
        IDbContextFactory<PanomiDbContext> contextFactory,
        LauncherDetectorFactory detectorFactory)
    {
        _contextFactory = contextFactory;
        _detectorFactory = detectorFactory;
    }

    public async Task<IEnumerable<Launcher>> GetAllLaunchersAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Launchers.Include(l => l.Games).ToListAsync();
    }

    public async Task<Launcher?> GetLauncherByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Launchers.Include(l => l.Games).FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<Launcher?> GetLauncherByTypeAsync(LauncherType type)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Launchers.Include(l => l.Games).FirstOrDefaultAsync(l => l.Type == type);
    }

    public async Task ScanLauncherAsync(int launcherId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var launcher = await context.Launchers.FindAsync(launcherId);
        if (launcher == null) return;

        await ScanLauncherInternalAsync(context, launcher);
        await context.SaveChangesAsync();
    }

    public async Task ScanLauncherAsync(LauncherType type)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var launcher = await context.Launchers.FirstOrDefaultAsync(l => l.Type == type);
        if (launcher == null) return;

        await ScanLauncherInternalAsync(context, launcher);
        await context.SaveChangesAsync();
    }

    public async Task DetectInstalledLaunchersAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var launchers = await context.Launchers.ToListAsync();
        
        foreach (var launcher in launchers.Where(l => l.Type != LauncherType.Manual))
        {
            var detector = _detectorFactory.GetDetector(launcher.Type);
            if (detector == null) continue;

            launcher.IsInstalled = await detector.IsInstalledAsync();
            launcher.InstallPath = await detector.GetInstallPathAsync();
        }
        
        await context.SaveChangesAsync();
    }

    private async Task ScanLauncherInternalAsync(PanomiDbContext context, Launcher launcher)
    {
        if (launcher.Type == LauncherType.Manual) return;

        var detector = _detectorFactory.GetDetector(launcher.Type);
        if (detector == null) return;

        var result = await detector.DetectGamesAsync();

        launcher.IsInstalled = result.IsInstalled;
        launcher.InstallPath = result.InstallPath;
        launcher.LastScanned = DateTime.UtcNow;

        if (!result.IsInstalled) return;

        foreach (var detectedGame in result.Games)
        {
            var existingGame = await context.Games
                .FirstOrDefaultAsync(g => g.LauncherId == launcher.Id && g.ExternalId == detectedGame.ExternalId);

            if (existingGame == null)
            {
                var game = new Game
                {
                    LauncherId = launcher.Id,
                    Name = detectedGame.Name,
                    InstallPath = detectedGame.InstallPath,
                    ExecutablePath = detectedGame.ExecutablePath,
                    LaunchCommand = detectedGame.LaunchCommand,
                    ExternalId = detectedGame.ExternalId,
                    DateAdded = DateTime.UtcNow
                };
                context.Games.Add(game);
            }
            else
            {
                existingGame.Name = detectedGame.Name;
                existingGame.InstallPath = detectedGame.InstallPath;
                existingGame.ExecutablePath = detectedGame.ExecutablePath;
                existingGame.LaunchCommand = detectedGame.LaunchCommand;
            }
        }
    }
}
