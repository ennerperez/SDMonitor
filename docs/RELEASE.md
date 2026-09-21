# Release Artifacts

SDMonitor publishes one self-contained executable per platform folder:

- `win-x64/sdmonitor.exe`
- `linux-x64/sdmonitor`
- `linux-x64/sdmonitor.deb`
- `linux-x64/sdmonitor.rpm`
- `linux-x64/sdmonitor.flatpak`
- `linux-x64/sdmonitor.AppImage`
- `osx-arm64/sdmonitor`
- `osx-arm64/sdmonitor.dmg`

Versions come from GitVersion using repository history and tags. The publish
scripts pass GitVersion values into `dotnet publish` so the executable metadata
keeps the release version while artifact filenames stay stable.

## GitHub Actions

CI is split into four workflows:

- `ci.yml` is the main workflow. It runs build, package, and tag releases.
- `build.yml` compiles and runs unit tests. It also validates pull requests.
- `package.yml` publishes `win-x64`, `linux-x64`, and `osx-arm64` workflow artifacts.
  The `linux-x64` job also creates `.deb`, `.rpm`, `.flatpak`, and `.AppImage`
  packages. The `osx-arm64` job runs on macOS and creates `sdmonitor.dmg`.
- `release.yml` uploads one zip archive per platform folder to GitHub Releases.
  Release asset names are `win-x64.zip`, `linux-x64.zip`, and `osx-arm64.zip`.

`release.yml` runs for tags matching `v*` and can also be started manually with
a tag input. If the release does not exist yet, the workflow creates it before
uploading assets.

## Local Publish

Use Bash:

```bash
scripts/publish-artifacts.sh
```

Use PowerShell:

```powershell
./scripts/publish-artifacts.ps1
```

Publish one runtime:

```bash
scripts/publish-artifacts.sh linux-x64
```

Artifacts are written to `artifacts/dist/<runtime>/`.

Linux package generation requires:

- `dpkg-deb` and `fakeroot` for Debian packages.
- `rpmbuild` for Fedora/Red Hat packages.
- `flatpak-builder` and `flatpak` for Flatpak bundles.
- `curl` when `appimagetool` is not installed locally.

Flatpak uses `org.freedesktop.Platform//24.08` and `org.freedesktop.Sdk//24.08`
by default. Override with `FLATPAK_RUNTIME_VERSION` when needed.
Use `LINUX_PACKAGE_FORMATS="deb rpm flatpak appimage"` to override which Linux
packages are generated locally.
Flatpak packaging needs a working Flatpak sandbox/user namespace. WSL or locked
down containers can fail with `Unable to allocate instance id`.

macOS DMG generation requires `hdiutil`, so it runs only on macOS. On Linux or
Windows, publish scripts still cross-publish the raw `osx-arm64/sdmonitor`
binary and skip the DMG with a message.

## Install

Linux raw binary:

```bash
chmod +x linux-x64/sdmonitor
sudo install -m 755 linux-x64/sdmonitor /usr/local/bin/sdmonitor
```

Debian/Ubuntu:

```bash
sudo apt install ./linux-x64/sdmonitor.deb
```

Fedora/Red Hat:

```bash
sudo dnf install ./linux-x64/sdmonitor.rpm
```

Flatpak:

```bash
flatpak install --user ./linux-x64/sdmonitor.flatpak
flatpak run dev.ennerperez.sdmonitor
```

AppImage:

```bash
chmod +x linux-x64/sdmonitor.AppImage
./linux-x64/sdmonitor.AppImage
```

Osx Apple Silicon:

```bash
chmod +x osx-arm64/sdmonitor
sudo install -m 755 osx-arm64/sdmonitor /usr/local/bin/sdmonitor
```

Osx DMG:

```bash
open osx-arm64/sdmonitor.dmg
```

Windows:

```powershell
New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\SDMonitor" | Out-Null
Copy-Item .\win-x64\sdmonitor.exe "$env:LOCALAPPDATA\SDMonitor\sdmonitor.exe"
```

Add `%LOCALAPPDATA%\SDMonitor` to `PATH` if command-line access is needed from
any directory.
