# SDMonitor

SDMonitor controls an Elgato Stream Deck Mini and can render a small hardware
dashboard on the six keys.

## Install

Download the artifact for your platform from the `Package` workflow or from a
GitHub Release:

- Windows x64: `sdmonitor-<version>-win-x64.exe`
- Linux x64: `sdmonitor-<version>-linux-x64`
- macOS Apple Silicon: `sdmonitor-<version>-osx-arm64`

Linux/macOS quick install:

```bash
chmod +x sdmonitor-<version>-linux-x64
sudo install -m 755 sdmonitor-<version>-linux-x64 /usr/local/bin/sdmonitor
```

Windows quick install:

```powershell
New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\SDMonitor" | Out-Null
Copy-Item .\sdmonitor-<version>-win-x64.exe "$env:LOCALAPPDATA\SDMonitor\sdmonitor.exe"
```

## Local Publish

Publish all supported platforms:

```bash
scripts/publish-artifacts.sh
```

PowerShell:

```powershell
./scripts/publish-artifacts.ps1
```

Artifacts are written to `artifacts/dist/`. Versions are generated with
GitVersion.

## Commands

Running `sdmonitor` without arguments starts the dashboard and loads
`dashboard-tiles.json` from the per-user SDMonitor config directory. If the file
does not exist, SDMonitor creates it with default tiles before loading it. If the
Stream Deck HID handle is unavailable, SDMonitor keeps running as a console
dashboard instead of exiting.

```text
sdmonitor list
sdmonitor info
sdmonitor brightness <0-100>
sdmonitor sleep <seconds>
sdmonitor logo
sdmonitor clear
sdmonitor color <key 0-5> <r 0-255> <g 0-255> <b 0-255>
sdmonitor watch
sdmonitor dashboard [config-path|interval-ms] [frames]
sdmonitor layout-test
```

Dashboard tile appearance is configured in `dashboard-tiles.json` under the
per-user config directory: `~/.config/SDMonitor` on Linux,
`~/Library/Application Support/SDMonitor` on macOS, and
`%APPDATA%\SDMonitor` on Windows. Each tile selects a `metric`, `position`,
`title`, colors, text sizes, spacing, bottom progress bar height, `refreshMilliseconds`, and optional `thresholds`. Threshold
styles are enabled per tile with `thresholdsEnabled`; matching thresholds can
override text size, background color, font color, title/value colors, and border
color.

The Avalonia simulator can mirror the six-key dashboard without opening the
Stream Deck HID device:

```bash
dotnet run --project src/SDMonitor.Simulator/SDMonitor.Simulator.csproj
```

See [release docs](docs/RELEASE.md) and [stack docs](docs/STACK.md) for build
and artifact details.
