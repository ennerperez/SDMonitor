# Release Artifacts

SDMonitor publishes one self-contained executable per platform:

- `sdmonitor-<version>-win-x64.exe`
- `sdmonitor-<version>-linux-x64`
- `sdmonitor-<version>-osx-arm64`

Versions come from GitVersion using repository history and tags. The publish
scripts pass GitVersion values into `dotnet publish` so the executable metadata
matches the artifact filename.

## GitHub Actions

CI is split into four workflows:

- `ci.yml` is the main workflow. It runs build, package, and tag releases.
- `build.yml` compiles and runs unit tests. It also validates pull requests.
- `package.yml` publishes `win-x64`, `linux-x64`, and `osx-arm64` workflow artifacts.
- `release.yml` uploads the packaged executables to GitHub Releases.

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

Artifacts are written to `artifacts/dist/`.

## Install

Linux:

```bash
chmod +x sdmonitor-<version>-linux-x64
sudo install -m 755 sdmonitor-<version>-linux-x64 /usr/local/bin/sdmonitor
```

macOS Apple Silicon:

```bash
chmod +x sdmonitor-<version>-osx-arm64
sudo install -m 755 sdmonitor-<version>-osx-arm64 /usr/local/bin/sdmonitor
```

Windows:

```powershell
New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\SDMonitor" | Out-Null
Copy-Item .\sdmonitor-<version>-win-x64.exe "$env:LOCALAPPDATA\SDMonitor\sdmonitor.exe"
```

Add `%LOCALAPPDATA%\SDMonitor` to `PATH` if command-line access is needed from
any directory.
