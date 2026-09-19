# Stack

## Runtime

- .NET 10
- C#
- Console CLI plus class library

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

- CLI command: `dashboard [interval-ms] [frames]`.
- Dashboard maps keys to CPU, RAM, GPU, root disk, network upload, and network download.
- Linux metrics come from `/proc/stat`, `/proc/meminfo`, `/proc/net/dev`, and `DriveInfo`.
- GPU usage uses `nvidia-smi` when present; otherwise tile shows `N/A`.
- Tile renderer is pure C# pixel drawing; no `System.Drawing` dependency.
- Network upload/download bars auto-scale to the peak rate observed during the current dashboard run.

## Current Environment Notes

- Device is visible from this session with `lsusb` as `0fd9:0063 Elgato Systems GmbH Stream Deck Mini`.
- `/dev/hidraw*` was not present during discovery, so direct control from this Linux/WSL session may need USB/HID permissions or Windows-native execution.
