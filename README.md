![](.editoricon.png)

# SDMonitor

SDMonitor controls an Elgato Stream Deck Mini and can render a small hardware
dashboard on the six keys.

## Install

Download the artifact for your platform from the `Package` workflow or from a
GitHub Release:

- GitHub Releases publish platform archives named `win-x64.zip`, `linux-x64.zip`,
  and `osx-arm64.zip`.
- Windows x64: `win-x64/sdmonitor.exe`
- Linux x64: `linux-x64/sdmonitor`
- Debian/Ubuntu x64: `linux-x64/sdmonitor.deb`
- Fedora/Red Hat x64: `linux-x64/sdmonitor.rpm`
- Other Linux distros: `linux-x64/sdmonitor.flatpak`
- Portable Linux x64: `linux-x64/sdmonitor.AppImage`
- Osx Apple Silicon: `osx-arm64/sdmonitor`
- Osx DMG: `osx-arm64/sdmonitor.dmg`

Linux/Osx quick install:

```bash
chmod +x linux-x64/sdmonitor
sudo install -m 755 linux-x64/sdmonitor /usr/local/bin/sdmonitor
```

Windows quick installation:

```powershell
New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\SDMonitor" | Out-Null
Copy-Item .\win-x64\sdmonitor.exe "$env:LOCALAPPDATA\SDMonitor\sdmonitor.exe"
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

Artifacts are written to `artifacts/dist/<runtime>/`. Versions are generated
with GitVersion and kept in executable/package metadata. Linux package
generation automatically installs or verifies `dpkg-deb`, `fakeroot`,
`rpmbuild`, `flatpak-builder`, `flatpak`, `curl`, the Flatpak runtime/SDK, and
`appimagetool` before publish starts. macOS DMG generation on Linux uses
`mkfs.hfsplus` from `hfsprogs`; on macOS it uses `hdiutil`.

Install Linux packaging tools on Debian/Ubuntu:

```bash
sudo apt update
sudo apt install curl dpkg fakeroot flatpak flatpak-builder hfsprogs rpm
```

Install Linux packaging tools on Fedora/Red Hat:

```bash
sudo dnf install curl dpkg fakeroot flatpak flatpak-builder hfsplus-tools rpm-build
```

Install Linux packaging tools on Arch:

```bash
sudo pacman -S curl dpkg fakeroot flatpak flatpak-builder hfsprogs rpm-tools
```

Install Flatpak runtime used by local packaging:

```bash
sudo flatpak remote-add --system --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo
sudo flatpak install -y --system flathub org.freedesktop.Platform//24.08 org.freedesktop.Sdk//24.08
```

Flatpak packaging also needs a working Flatpak sandbox/user namespace. WSL or
locked down containers can fail with `Unable to allocate instance id`.

## Commands

Running `sdmonitor` without arguments starts the dashboard and loads
`preferences.json` from the per-user SDMonitor config directory. If the file
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

Dashboard tile appearance is configured in `preferences.json` under the
per-user config directory: `~/.config/SDMonitor` on Linux,
`~/Library/Application Support/SDMonitor` on Osx, and
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
