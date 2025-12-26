using Microsoft.EntityFrameworkCore;
using Panomi.Core.Models;

namespace Panomi.Data;

/// <summary>
/// Entity Framework Core database context for Panomi
/// </summary>
public class PanomiDbContext : DbContext
{
    public DbSet<Launcher> Launchers => Set<Launcher>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Setting> Settings => Set<Setting>();

    private readonly string _dbPath;

    public PanomiDbContext()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var panomiPath = Path.Combine(appDataPath, "Panomi");
        Directory.CreateDirectory(panomiPath);
        _dbPath = Path.Combine(panomiPath, "panomi.db");
    }

    public PanomiDbContext(DbContextOptions<PanomiDbContext> options) : base(options)
    {
        _dbPath = string.Empty;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Launcher configuration
        modelBuilder.Entity<Launcher>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.InstallPath).HasMaxLength(500);
            entity.HasIndex(e => e.Type).IsUnique();
        });

        // Game configuration
        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.InstallPath).HasMaxLength(500);
            entity.Property(e => e.ExecutablePath).HasMaxLength(500);
            entity.Property(e => e.LaunchCommand).HasMaxLength(1000);
            entity.Property(e => e.IconPath).HasMaxLength(500);
            entity.Property(e => e.ExternalId).HasMaxLength(100);
            
            entity.HasOne(e => e.Launcher)
                  .WithMany(l => l.Games)
                  .HasForeignKey(e => e.LauncherId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasIndex(e => new { e.LauncherId, e.ExternalId }).IsUnique();
        });

        // Setting configuration
        modelBuilder.Entity<Setting>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(100);
            entity.Property(e => e.Value).HasMaxLength(1000);
        });

        // Seed default launchers
        SeedLaunchers(modelBuilder);
    }

    private static void SeedLaunchers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Launcher>().HasData(
            new Launcher { Id = 1, Name = "Steam", Type = LauncherType.Steam, IsInstalled = false },
            new Launcher { Id = 2, Name = "Epic Games", Type = LauncherType.EpicGames, IsInstalled = false },
            new Launcher { Id = 3, Name = "EA App", Type = LauncherType.EAApp, IsInstalled = false },
            new Launcher { Id = 4, Name = "Ubisoft Connect", Type = LauncherType.UbisoftConnect, IsInstalled = false },
            new Launcher { Id = 5, Name = "GOG Galaxy", Type = LauncherType.GOGGalaxy, IsInstalled = false },
            new Launcher { Id = 6, Name = "Battle.net", Type = LauncherType.BattleNet, IsInstalled = false },
            new Launcher { Id = 7, Name = "Rockstar Games", Type = LauncherType.RockstarGames, IsInstalled = false },
            new Launcher { Id = 8, Name = "Riot Games", Type = LauncherType.RiotGames, IsInstalled = false },
            new Launcher { Id = 9, Name = "Minecraft", Type = LauncherType.Minecraft, IsInstalled = false },
            new Launcher { Id = 10, Name = "Roblox", Type = LauncherType.Roblox, IsInstalled = false },
            new Launcher { Id = 99, Name = "Manual", Type = LauncherType.Manual, IsInstalled = true }
        );
    }
}
