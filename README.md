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

Running `sdmonitor` without arguments starts the dashboard with default values:
`dashboard 1500`. If the Stream Deck HID handle is unavailable, SDMonitor keeps
running as a console dashboard instead of exiting.

```text
sdmonitor list
sdmonitor info
sdmonitor brightness <0-100>
sdmonitor sleep <seconds>
sdmonitor logo
sdmonitor clear
sdmonitor color <key 0-5> <r 0-255> <g 0-255> <b 0-255>
sdmonitor watch
sdmonitor dashboard [interval-ms] [frames]
sdmonitor layout-test
```

See [release docs](docs/RELEASE.md) and [stack docs](docs/STACK.md) for build
and artifact details.
