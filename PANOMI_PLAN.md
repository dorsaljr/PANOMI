# PANOMI - Project Status

> **Last Updated:** v1.4.1 (June 2025)

## Overview
PANOMI is a lightweight, offline-first Windows game launcher aggregator that consolidates PC game libraries into one unified interface using local file/registry scanning.

---

## Tech Stack

| Layer | Technology |
|-------|------------|
| Frontend | WinUI 3 + WinAppSDK 1.5 (Fluent Design) |
| Backend | C# / .NET 8 |
| Database | SQLite + Entity Framework Core |
| Updates | Velopack (auto-updates from GitHub Releases) |
| Signing | Azure Trusted Signing |
| Distribution | GitHub Releases (direct exe download) |

---

## Supported Launchers

| Launcher | Status | Detection Method |
|----------|--------|------------------|
| Steam | ✅ Complete | Registry + VDF/ACF parsing |
| Epic Games | ✅ Complete | Registry + JSON manifests |
| EA App | ✅ Complete | Registry + XML configs |
| Ubisoft Connect | ✅ Complete | Registry + YAML |
| GOG Galaxy | ✅ Complete | Registry + SQLite DB |
| Battle.net | ✅ Complete | Registry + config files |
| Rockstar | ✅ Complete | Registry + configs |
| Riot Games | ✅ Complete | Registry + configs |
| Minecraft | ✅ Complete | Registry + JSON |
| Amazon Games | ✅ Complete | Registry + SQLite DB |
| itch.io | ✅ Complete | Registry + database |
| IndieGala | ✅ Complete | AppData configs |
| Legacy Games | ✅ Complete | Registry |
| Chinese Platforms | ✅ Complete | WeGame, Bilibili, Tencent |

---

## Core Features

- **Automatic game detection** from 14+ launcher platforms
- **Icon extraction** from installed executables (Shell API)
- **Manual game adding** with custom launch commands
- **Search, filter, sort** with smart filtering
- **Recently played tracking** with visual indicators
- **Favorites system** with pinned games
- **Multi-language support** (29 languages)
- **Auto-updates** via Velopack
- **Compact card UI** with hover-reveal play buttons

---

## Project Structure

```
PANOMI/
├── src/
│   ├── Panomi.UI/              # WinUI 3 frontend
│   ├── Panomi.Core/            # Business logic, models
│   └── Panomi.Detection/       # All launcher detectors
├── test/
│   ├── DbTest/
│   └── UbisoftTest/
├── docs/                       # GitHub Pages download site
├── publish/                    # Build output
└── Panomi.sln
```

---

## Key Design Decisions

- ✅ **Offline-first**: Pure file/registry scanning, no external APIs
- ✅ **Icon extraction**: From user's own installed executables (legal)
- ✅ **SQLite**: Local database for game library persistence
- ✅ **Velopack**: Replaced MSIX for simpler distribution
- ✅ **Azure Trusted Signing**: Professional code signing
- ✅ **Direct download**: Single exe installer from GitHub Releases
- ❌ **No Xbox/Game Pass**: Too complex, different ecosystem
- ❌ **No background service**: Manual refresh only
- ❌ **No cloud sync**: Local-only by design

---

## Links

- **Download**: [GitHub Pages](https://dorsaljr.github.io/PANOMI_BETA/)
- **Releases**: [GitHub Releases](https://github.com/dorsaljr/PANOMI_BETA/releases)
- **Source**: Private repository

---