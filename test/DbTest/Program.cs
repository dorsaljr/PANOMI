using Microsoft.EntityFrameworkCore;
using Panomi.Core.Models;
using Panomi.Data;
using Panomi.Detection;
using Panomi.Detection.Detectors;

Console.WriteLine("Testing Service-like Scan Flow...");
Console.WriteLine("=".PadRight(50, '='));

try
{
    // Simulate what the service does
    using var context = new PanomiDbContext();
    var detectorFactory = new LauncherDetectorFactory();
    
    Console.WriteLine("Step 1: Get all launchers...");
    var launchers = await context.Launchers.ToListAsync();
    Console.WriteLine($"  Found {launchers.Count} launchers");
    
    Console.WriteLine("\nStep 2: Detect installed launchers...");
    foreach (var launcher in launchers.Where(l => l.Type != LauncherType.Manual))
    {
        var detector = detectorFactory.GetDetector(launcher.Type);
        if (detector == null)
        {
            Console.WriteLine($"  {launcher.Name}: No detector found");
            continue;
        }
        
        var isInstalled = await detector.IsInstalledAsync();
        var installPath = await detector.GetInstallPathAsync();
        
        Console.WriteLine($"  {launcher.Name}: IsInstalled={isInstalled}, Path={installPath}");
        
        launcher.IsInstalled = isInstalled;
        launcher.InstallPath = installPath;
        context.Launchers.Update(launcher);
    }
    
    Console.WriteLine("\nStep 3: Save changes...");
    var saved = await context.SaveChangesAsync();
    Console.WriteLine($"  Saved {saved} changes");
    
    Console.WriteLine("\nStep 4: Scan for games...");
    foreach (var launcher in launchers.Where(l => l.IsInstalled && l.Type != LauncherType.Manual))
    {
        var detector = detectorFactory.GetDetector(launcher.Type);
        if (detector == null) continue;
        
        var result = await detector.DetectGamesAsync();
        Console.WriteLine($"  {launcher.Name}: Found {result.Games.Count} games");
        
        foreach (var detectedGame in result.Games)
        {
            // Check if game already exists
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
                Console.WriteLine($"    + {game.Name}");
            }
            else
            {
                Console.WriteLine($"    = {existingGame.Name} (already exists)");
            }
        }
    }
    
    Console.WriteLine("\nStep 5: Save games...");
    saved = await context.SaveChangesAsync();
    Console.WriteLine($"  Saved {saved} changes");
    
    Console.WriteLine("\n" + "=".PadRight(50, '='));
    Console.WriteLine("Verifying final state...");
    
    using var verifyContext = new PanomiDbContext();
    var finalLaunchers = await verifyContext.Launchers.ToListAsync();
    var installedCount = finalLaunchers.Count(l => l.IsInstalled);
    Console.WriteLine($"Installed launchers: {installedCount}");
    
    var finalGames = await verifyContext.Games.Include(g => g.Launcher).ToListAsync();
    Console.WriteLine($"Total games: {finalGames.Count}");
    foreach (var g in finalGames)
    {
        Console.WriteLine($"  - {g.Name} ({g.Launcher?.Name})");
    }
    
    Console.WriteLine("\nSUCCESS!");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex}");
}
