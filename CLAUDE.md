# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build the app
dotnet build src/BsodDoctor

# Build with specific version (assembly metadata)
dotnet build src/BsodDoctor -p:Version=1.0.0

# Run (requires Windows for WPF runtime)
dotnet run --project src/BsodDoctor

# Run tests (console-based, no test framework — uses dotnet run)
dotnet run --project tests/BsodDoctor.Tests [<path-to-dmp-file>]

# Publish for distribution
dotnet publish src/BsodDoctor -c Release --no-build -o publish

# Full CI pipeline (build → test → publish → installer)
# See .github/workflows/release.yml
```

## Code Architecture

**BSOD Doctor** is a WPF desktop app (MVVM pattern) that reads Windows minidump files, extracts the BSOD error code, and presents Turkish-language solutions from a local SQLite database.

### Project Structure

```
bsod_doctor/
├── src/BsodDoctor/           # WPF application
│   ├── App.xaml/.cs          # Entry point (minimal — no DI setup)
│   ├── MainWindow.xaml/.cs   # Code-behind delegates everything to ViewModel
│   ├── ViewModels/
│   │   └── MainViewModel.cs  # Core logic: scanning, history, theme, commands
│   ├── Models/
│   │   ├── BsodError.cs      # Error code + solution (severity, category, etc.)
│   │   ├── AnalysisResult.cs # Scan result payload
│   │   └── HistoryItem.cs    # Past scan record (resolved/unresolved)
│   ├── Services/
│   │   ├── MinidumpReader.cs # Static binary parser for .dmp files
│   │   ├── BsodWatchService.cs  # One-shot dump scanner with cooldown
│   │   ├── DatabaseService.cs   # SQLite CRUD, seed import, migration
│   │   └── IDatabaseService.cs  # (Legacy interface — not used by app)
│   └── Resources/Themes/     # Dark / Light XAML theme dictionaries
├── tests/BsodDoctor.Tests/   # Console test (no test framework, runs via dotnet run)
│   └── TestData/test_minidump.dmp  # Synthetic dump for parser validation
├── database/
│   ├── schema.sql            # SQLite schema (bsod_errors + analysis_history)
│   └── seed_data.json        # 51 BSOD error codes with Turkish solutions
├── setup/                    # InnoSetup installer script + wizard images
└── .github/workflows/        # CI: build → test → publish → release
```

### Key Design Decisions

- **MinidumpReader** is a static class that parses binary .dmp files without ClrMD. Supports classic MDMP minidump format and PAGEDUMP64/PAGEDUMP32 full-dump formats. Cross-platform testable on Linux.
- **BsodWatchService** is one-shot (not a long-lived FileSystemWatcher). It scans `C:\Windows\Minidump\` on startup, applies a 24-hour cooldown per error code, and stops.
- **No DI container** — services are instantiated directly in the `MainViewModel` constructor. `App.xaml.cs` is empty.
- **Seed data** (`database/seed_data.json`) auto-imports into SQLite on first run. The database file lives at `bin/Data/bsod_errors.db` at runtime (not in the repo).
- **Theme preference** persisted to `bin/Data/settings.json`.
- **Tests** use a synthetic dump (`test_minidump.dmp`) expected to produce error code `0x00000050`. No test framework (xUnit/NUnit) — just a console app that returns exit code 0/1.
- Turkish language throughout: UI strings, commit messages, error descriptions, solution steps.

### Data Flow

```
App start → DatabaseService.InitializeAsync() (tables + seed import)
         → BsodWatchService.ScanOnceAsync()
              → MinidumpReader.ReadBugCheckCode(file) → error code string
              → DatabaseService.IsErrorInCooldownAsync(code, 24h)
              → DatabaseService.FindErrorByCodeAsync(code) → BsodError
              → DatabaseService.SaveAnalysisRecordAsync() → historyId
              → MainViewModel populates bindable properties → UI updates
         → (service stops, no background watcher)
Manual "Tara" button → same flow with scanAll=true (ignores file age filter)
```

### Database Schema

Two tables: `bsod_errors` (code → solution mapping with `kesin_cozum` column for the single best fix) and `analysis_history` (timestamped scan records with resolved/user_feedback tracking). See `database/schema.sql`.

### CI/CD

GitHub Actions on push to `master`: build → run tests → publish self-contained → InnoSetup installer → GitHub Release (stable, versioned `1.0.${{ github.run_number }}`). Assembly version injected via `-p:Version`. Installer includes app logo on wizard pages.
