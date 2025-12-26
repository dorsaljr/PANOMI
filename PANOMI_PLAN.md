# PANOMI - Development Plan

## Overview
A lightweight, offline-first Windows 11 app that consolidates all PC game launchers and games into one unified library using local file scanning only.

---

## Tech Stack

| Layer | Technology |
|-------|------------|
| Frontend | WinUI 3 + XAML (Fluent Design) |
| Backend | C# / .NET 8 |
| Database | SQLite + EF Core |
| Packaging | MSIX installer |

**Target footprint:** ~40-50MB installed, <100MB RAM

---

## Core Principles

| Principle | Implementation |
|-----------|----------------|
| Lightweight | No external APIs, no network calls for detection |
| Offline-first | Pure file/registry scanning |
| Simple | Manual icons provided by user |
| Secure | No API keys, no data sent anywhere |
| Incremental | One launcher at a time |

i---

## Icons Strategy

- **Automatic extraction**: Icons extracted from installed executables using Windows Shell API
- **Method**: `Icon.ExtractAssociatedIcon()` - most stable .NET method
- **Storage location**: `AppData/Local/Panomi/Icons/` (cached for performance)
- **Format**: PNG (converted from extracted icons, 256x256 for best quality)
- **Naming**: `{LauncherType}_{ExternalId}.png` for games, `{LauncherType}.png` for launchers
- **Fallback**: Display launcher/game NAME as text (no placeholder icons to avoid legal issues)
- **Legal**: No copyright issues - icons extracted from user's own installed files
- **Note**: Does not apply to PANOMI logo (original artwork)

### Icon Sources by Launcher
| Launcher | Launcher Icon Source | Game Icon Source |
|----------|---------------------|------------------|
| Steam | `steam.exe` | Game executable from install path |
| Epic Games | `EpicGamesLauncher.exe` | Game executable from manifest |
| EA App | `EADesktop.exe` | Game executable |
| Ubisoft | `upc.exe` | Game executable |
| GOG | `GalaxyClient.exe` | Game executable |
| Battle.net | `Battle.net.exe` | Game executable |
| Rockstar | `Launcher.exe` | Game executable |
| Riot | `RiotClientServices.exe` | Game executable |
| Minecraft | `MinecraftLauncher.exe` | Game executable |
| Roblox | `RobloxPlayerBeta.exe` | N/A (single game) |

---

## Launcher Implementation Order

| # | Launcher | Complexity | Detection Method |
|---|----------|------------|------------------|
| 1 | Steam | Medium | Registry + VDF/ACF parsing |
| 2 | Epic Games | Low | Registry + JSON manifests |
| 3 | EA App | Medium | Registry + XML configs |
| 4 | Ubisoft Connect | Medium | Registry + YAML |
| 5 | GOG Galaxy | Low | Registry + SQLite DB |
| 6 | Battle.net | Medium | Registry + config files |
| 7 | Rockstar | Low | Registry + configs |
| 8 | Riot | Low | Registry + configs |
| 9 | Minecraft | Medium | Registry + JSON |
| 10 | Roblox | Low | Registry |

---

## Development Phases

### Phase 1: Foundation
- [ ] Create solution structure (4 projects)
- [ ] Set up SQLite schema (Launchers, Games, Settings tables)
- [ ] Build UI shell (navigation, game grid, settings page)
- [ ] Implement `ILauncherDetector` interface
- [ ] Create icon loading system (manual icons from folder)

### Phase 2: Steam
- [ ] Detect Steam via Registry (`HKLM\SOFTWARE\Valve\Steam`)
- [ ] Parse `libraryfolders.vdf` for library paths
- [ ] Parse `appmanifest_*.acf` for installed games
- [ ] Display Steam games in UI
- [ ] Launch games via Steam protocol

### Phase 3: Epic Games
- [ ] Detect Epic via Registry
- [ ] Parse `.item` manifest files
- [ ] Extract game data
- [ ] Add to unified library

### Phase 4: EA App
- [ ] Detect EA App via Registry
- [ ] Parse XML config files
- [ ] Extract installed games

### Phase 5: Ubisoft Connect
- [ ] Detect via Registry
- [ ] Parse YAML configs
- [ ] Extract game data

### Phase 6: GOG Galaxy
- [ ] Detect via Registry
- [ ] Read GOG's SQLite database
- [ ] Extract game info

### Phase 7: Battle.net
- [ ] Detect via Registry
- [ ] Parse config files
- [ ] Map game IDs to names

### Phase 8: Rockstar
- [ ] Detect via Registry
- [ ] Parse launcher configs

### Phase 9: Riot
- [ ] Detect via Registry
- [ ] Find Riot game installations

### Phase 10: Minecraft
- [ ] Detect Java + Bedrock editions
- [ ] Parse launcher profiles

### Phase 11: Roblox
- [ ] Detect via Registry
- [ ] Add to library

### Phase 12: Polish
- [ ] Manual game adding feature
- [ ] Search, filter, sort functionality
- [ ] Recently played tracking
- [ ] MSIX packaging
- [ ] Performance optimization

---

## Project Structure

```
PANOMI/
├── src/
│   ├── Panomi.UI/              # WinUI 3 frontend
│   ├── Panomi.Core/            # Business logic, interfaces
│   ├── Panomi.Detection/       # All launcher detectors
│   └── Panomi.Data/            # SQLite, EF Core, models
├── tests/
│   ├── Panomi.Detection.Tests/
│   └── Panomi.Core.Tests/
├── icons/                      # Manual game icons
└── Panomi.sln
```

---

## Database Schema

### Launchers Table
- Id (int, PK)
- Name (string)
- InstallPath (string)
- IsInstalled (bool)
- LastScanned (datetime)

### Games Table
- Id (int, PK)
- LauncherId (int, FK)
- Name (string)
- InstallPath (string)
- ExecutablePath (string)
- LaunchCommand (string)
- IconPath (string, nullable)
- LastPlayed (datetime, nullable)
- DateAdded (datetime)

### Settings Table
- Key (string, PK)
- Value (string)

---

## Key Decisions

- ❌ No Steam API (file parsing instead)
- ❌ No Xbox/Game Pass (too complex)
- ❌ No background service (manual refresh)
- ❌ No auto icon extraction (manual icons only)
- ✅ SQLite for local database
- ✅ One launcher at a time development
- ✅ Unit tests with mock data

---

## Testing Strategy

- Unit tests with mock file structures
- Beta testers for launchers developer doesn't own
- Free games for GOG, Rockstar, Minecraft testing

---