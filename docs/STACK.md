# Stack

## Runtime

- .NET 10
- C#
- Single console project with tests in a separate test project

## Device Access

- Direct USB HID control via HidSharp.
- Target device: Elgato Stream Deck Mini, VID `0x0FD9`, PID `0x0063`.
- Protocol family: Stream Deck Mini legacy HID protocol.

## Hardware Notes

- Mini layout: 3 columns by 2 rows, 6 keys.
- Key image size: 80 x 80 pixels.
- Key images are uploaded as BMP bytes, rotated 90 degrees clockwise.
- Output reports are 1024 bytes.
- Feature reports are 32 bytes.
- Input reports are 65 bytes and contain 6 key-state bytes after report ID.

## Dashboard Notes

- CLI command: `dashboard [config-path|interval-ms] [frames]`.
- Running the CLI without arguments starts `dashboard` and loads `dashboard-tiles.json` from the per-user SDMonitor config directory, creating it with defaults when missing.
- If the Stream Deck HID handle cannot be opened, dashboard mode falls back to console output.
- Dashboard tiles are configured through external JSON with `metric`, `font`, title/value sizes, title, border color, progress color, background color, progress bar height percent, margin/padding percent, refresh interval, text colors, and deck position.
- Dashboard maps configured metrics to CPU, RAM, GPU, root disk, network upload, and network download.
- Linux metrics come from `/proc/stat`, `/proc/meminfo`, `/proc/net/dev`, and `DriveInfo`.
- Windows metrics come from process CPU time, `GlobalMemoryStatusEx`, `DriveInfo`, and `NetworkInterface`.
- macOS metrics come from `top`, `sysctl`, `vm_stat`, `ioreg`, `NetworkInterface`, and `DriveInfo`.
- GPU usage uses `nvidia-smi` on Linux/Windows and `ioreg` on macOS when present; otherwise tile shows `N/A`.
- Tile renderer is pure C# pixel drawing; no `System.Drawing` dependency.
- Network upload/download bars auto-scale to the peak rate observed during the current dashboard run.

## Release Notes

- GitVersion generates artifact and assembly versions from Git history.
- Publish target is the unified `src/SDMonitor/SDMonitor.csproj`.
- Release artifacts are self-contained single-file executables.
- Supported runtime identifiers: `win-x64`, `linux-x64`, and `osx-arm64`.
- Local publish scripts write distributable files to `artifacts/dist/`.
- GitHub Actions is split into `ci.yml`, `build.yml`, `package.yml`, and `release.yml`.

## Current Environment Notes

- Device is visible from this session with `lsusb` as `0fd9:0063 Elgato Systems GmbH Stream Deck Mini`.
- `/dev/hidraw*` was not present during discovery, so direct control from this Linux/WSL session may need USB/HID permissions or Windows-native execution.
