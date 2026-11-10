# FocusLock

A calm focus timer for Windows that closes games while a focus session runs.

Start a 25, 50 or 60 minute session (or any length you like). While it runs, any game that starts is closed
again and a small notification tells you why. Optional Pomodoro mode alternates focus blocks and short breaks,
and games are allowed during breaks. When the session ends you get a short summary.

## Everyday use

- The window hides into the corner of the taskbar while a session runs; closing or minimizing it does not stop
  the session. Hover the tray icon for the time left; double-click it to bring the window back.
- Notifications are small in-app notes in the corner of the screen, so they still appear when Windows
  Do Not Disturb is on.
- Strict mode (Settings) makes "End early" take a 60-second wait, or typing
  "I choose to end this session early."
- `FocusLock.exe --minimized` starts straight into the tray; the "start when I sign in" setting uses this.
- Starting FocusLock a second time just brings the running window back.
- Games are found automatically from Steam, Epic, GOG, the EA app, Ubisoft Connect and Battle.net. The list is
  refreshed every time a session starts, so a game installed a minute ago is still blocked.

## Requirements

- Windows 10 or 11, x64.
- To build: the .NET 10 SDK (current LTS). `winget install --id Microsoft.DotNet.SDK.10 --exact` installs it.
- To run the published app: nothing. The release build is self-contained.

### If `dotnet` says "No .NET SDKs were found"

On machines that also have the 32-bit .NET runtime installed, the system `PATH` may list
`C:\Program Files (x86)\dotnet\` before `C:\Program Files\dotnet\`. The 32-bit host then shadows the 64-bit SDK.
Either move `C:\Program Files\dotnet\` above the x86 entry in the system `PATH` (one-time fix), or call the SDK by
its full path:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build FocusLock.slnx
```

The commands below assume `dotnet` resolves to the 64-bit SDK.

## Build, test, run

```powershell
# Build everything
dotnet build FocusLock.slnx

# Run the unit tests
dotnet test FocusLock.slnx

# Run the app
dotnet run --project src\FocusLock.App
```

## Publish a single self-contained exe

```powershell
dotnet publish src\FocusLock.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
  -o publish
```

The result is `publish\FocusLock.exe`. It runs on any 64-bit Windows 10/11 machine without installing .NET.

## Project layout

```
FocusLock.slnx
Directory.Build.props        shared build settings (net10.0-windows, nullable, analyzers)
src/FocusLock.Core/          all logic; no UI references
src/FocusLock.App/           WPF user interface, tray icon, in-app notifications
tests/FocusLock.Tests/       xUnit tests for Core, using fakes for every system call
tools/make-icon.ps1          regenerates src/FocusLock.App/Assets/FocusLock.ico
```

The app project follows MVVM with CommunityToolkit.Mvvm: views bind to view models, and the only code-behind
is trivial wiring. `SessionController` is the single bridge between the UI and Core; it re-raises Core's
background-thread events on the UI thread. The tray icon uses the Windows Forms `NotifyIcon` (no extra package);
there is no other Windows Forms UI.

## Architecture overview

Core is split into small layers. Each layer only talks to the ones below it through interfaces, so the whole
thing runs in unit tests with fakes and no real processes, files or registry.

| Folder          | What lives there |
|-----------------|------------------|
| `Abstractions`  | Interfaces for everything that touches the system: `IClock`, `ITickTimer`, `IFileSystem`, `IRegistry`, `IAppEnvironment`, `IProcessProvider`, `IProcessTerminator`, `IProcessWatcher`. |
| `Platform`      | The Windows implementations of those interfaces. This is the only folder with real system calls. |
| `Detection`     | `IGameDetector` and one detector per source: Steam (reads `libraryfolders.vdf` and the app manifests), Epic (reads the launcher's `.item` manifests), GOG (registry `GOG.com\Games`), Ubisoft Connect (registry `Ubisoft\Launcher\Installs`), EA app and Battle.net (`PublisherGameDetector`: the Windows installed-programs list, filtered by publisher), launchers (by process name) and the user's custom list. `GameCatalog` runs them all and applies the user's on/off toggles. |
| `Blocking`      | `BlockPolicy` (plain data: block rules plus allow rules), `BlockRuleEvaluator` (folder, executable and process-name matching; the allowlist always wins; FocusLock itself and the Windows folder are never touched) and `BlockingEngine` (closes matching processes while active). |
| `Processes`     | `PollingProcessWatcher`: compares process lists once a second and reports new ones. It sits behind `IProcessWatcher`, so a WMI-event-based watcher can replace it later. |
| `Sessions`      | `SessionPlan` (single block or Pomodoro cycles), `FocusSessionEngine` (a pure state machine driven by timestamps, so it never drifts) and `ISessionService`, the only session API the UI uses. |
| `Settings`      | `AppSettings` and session stats as JSON in `%AppData%\FocusLock`, plus the run-at-sign-in registration. |
| `Logging`       | A small rolling-file logger for `Microsoft.Extensions.Logging`: one JSON object per line, one file per day, seven days kept. |

How a session works:

1. The UI builds a `SessionPlan` from the chosen length (and Pomodoro settings). `BlockPolicyBuilder.BuildFreshAsync`
   scans for games again, so new installs are covered, and builds a `BlockPolicy` from the enabled games plus the
   allowlist. Then the UI calls `ISessionService.StartAsync`.
2. `InProcessSessionService` starts the engine, activates `BlockingEngine` with the policy and ticks once a second.
   During breaks blocking is paused; the next focus block turns it back on.
3. `BlockingEngine` listens to `IProcessWatcher`. Every new process goes through `BlockRuleEvaluator`; matches are
   closed with `IProcessTerminator` and reported. Access-denied and already-exited results are logged, never thrown.
4. When the last focus block ends (or the user ends early) the service raises `Completed` with a `SessionSummary`
   and appends it to the stats file.

Everything the UI sees (`SessionState`, `SessionSummary`, `BlockedLaunchNotice`, `BlockPolicy`) is plain data.
That keeps the door open for a later Windows Service that enforces blocking as SYSTEM, with the UI talking to it over
named pipes through the same `ISessionService` contract.

## Adding a new game source

1. Add a class in `src/FocusLock.Core/Detection` that implements `IGameDetector` and returns `DetectedGame`
   entries (a folder, an executable path, or a process name).
2. Register it in `ServiceCollectionExtensions.AddFocusLockCore`.
3. Add a test next to the others in `tests/FocusLock.Tests/Detection` using `FakeFileSystem` and `FakeRegistry`.

## Where data lives

| File | Purpose |
|------|---------|
| `%AppData%\FocusLock\settings.json` | All settings, including toggles, custom blocklist and allowlist. |
| `%AppData%\FocusLock\stats.json`    | Session history (focused time, blocked launches). |
| `%AppData%\FocusLock\logs\focuslock-YYYY-MM-DD.log` | Daily log, JSON lines, seven days kept. |

If a settings or stats file cannot be read it is renamed to `.bak` and defaults are used.
