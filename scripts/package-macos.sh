#!/usr/bin/env bash
set -euo pipefail

binary="${1:?binary path required}"
version="${2:?version required}"
dist_root="${3:?dist root required}"
rid="${4:-osx-arm64}"
bundle_version="${5:-$version}"

make_abs() {
    case "$1" in
        /*) printf '%s\n' "$1" ;;
        *) printf '%s\n' "$PWD/$1" ;;
    esac
}

require_command() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "Missing required command: $1" >&2
        exit 1
    fi
}

run_root() {
    if [[ "$(id -u)" -eq 0 ]]; then
        "$@"
    else
        sudo "$@"
    fi
}

find_mkisofs_hfsplus() {
    local tool
    local tool_path

    for tool in xorrisofs mkisofs genisoimage; do
        if tool_path="$(command -v "$tool" 2>/dev/null)" && "$tool_path" --help 2>&1 | grep -q -- "-hfsplus"; then
            printf '%s\n' "$tool_path"
            return 0
        fi
    done

    return 1
}

package_name="${PACKAGE_NAME:-sdmonitor}"
bundle_name="${MACOS_BUNDLE_NAME:-SDMonitor.app}"
bundle_id="${MACOS_BUNDLE_ID:-dev.ennerperez.sdmonitor}"
display_name="${MACOS_DISPLAY_NAME:-SDMonitor}"
icon_path="${MACOS_ICON_PATH:-packaging/macos/sdmonitor.icns}"
package_root="${PACKAGE_ROOT:-${TMPDIR:-/tmp}/sdmonitor-package-macos}"

binary="$(make_abs "$binary")"
dist_root="$(make_abs "$dist_root")"
package_root="$(make_abs "$package_root")"
icon_path="$(make_abs "$icon_path")"
short_version="${version%%[-+]*}"

if [[ ! -f "$binary" ]]; then
    echo "macOS binary not found: $binary" >&2
    exit 1
fi

case "$rid" in
    osx-arm64) bundle_arch="arm64" ;;
    osx-x64) bundle_arch="x86_64" ;;
    *)
        echo "Unsupported macOS RID for packaging: $rid" >&2
        exit 1
        ;;
esac

work="$package_root/$rid"
bundle="$work/$bundle_name"
dmg_root="$work/dmg-root"
mount_root="$work/mount"
artifact="$dist_root/$package_name.dmg"

rm -rf "$work"
mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources" "$dmg_root" "$dist_root"

install -m 755 "$binary" "$bundle/Contents/MacOS/$package_name"
if [[ -f "$icon_path" ]]; then
    install -m 644 "$icon_path" "$bundle/Contents/Resources/sdmonitor.icns"
fi

cat > "$bundle/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key>
  <string>$package_name</string>
  <key>CFBundleIdentifier</key>
  <string>$bundle_id</string>
  <key>CFBundleName</key>
  <string>$display_name</string>
  <key>CFBundleDisplayName</key>
  <string>$display_name</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>$short_version</string>
  <key>CFBundleVersion</key>
  <string>$bundle_version</string>
  <key>LSMinimumSystemVersion</key>
  <string>12.0</string>
  <key>LSArchitecturePriority</key>
  <array>
    <string>$bundle_arch</string>
  </array>
  <key>NSHighResolutionCapable</key>
  <true/>
PLIST

if [[ -f "$icon_path" ]]; then
    cat >> "$bundle/Contents/Info.plist" <<'PLIST'
  <key>CFBundleIconFile</key>
  <string>sdmonitor</string>
PLIST
fi

cat >> "$bundle/Contents/Info.plist" <<'PLIST'
</dict>
</plist>
PLIST

cp -R "$bundle" "$dmg_root/$bundle_name"
ln -s /Applications "$dmg_root/Applications"

create_dmg_with_hdiutil() {
    hdiutil create \
        -volname "$display_name" \
        -srcfolder "$dmg_root" \
        -ov \
        -format UDZO \
        "$artifact" >/dev/null
}

create_dmg_with_mkisofs() {
    local tool

    tool="$(find_mkisofs_hfsplus)"
    "$tool" -quiet -hfsplus -R -V "$display_name" -o "$artifact" "$dmg_root" >/dev/null
}

create_dmg_with_hfsplus() {
    require_command mkfs.hfsplus
    require_command mount
    require_command umount

    if [[ "$(id -u)" -ne 0 ]]; then
        require_command sudo
        if ! sudo -n true >/dev/null 2>&1; then
            echo "Linux DMG packaging requires passwordless sudo for loop mount." >&2
            exit 1
        fi
    fi

    local size_kb
    local size_mb
    local mounted=0

    size_kb="$(du -sk "$dmg_root" | awk '{print $1}')"
    size_mb=$((size_kb / 1024 + 64))
    if (( size_mb < 96 )); then
        size_mb=96
    fi

    mkdir -p "$mount_root"
    dd if=/dev/zero of="$artifact" bs=1M count="$size_mb" status=none
    mkfs.hfsplus -v "$display_name" "$artifact" >/dev/null

    cleanup_mount() {
        if [[ "${mounted:-0}" -eq 1 ]]; then
            run_root umount "$mount_root" >/dev/null 2>&1 || true
        fi
    }
    trap cleanup_mount EXIT

    run_root mount -o loop "$artifact" "$mount_root"
    mounted=1
    run_root cp -a "$dmg_root/." "$mount_root/"
    sync
    run_root umount "$mount_root"
    mounted=0
    trap - EXIT
}

rm -f "$artifact"
if command -v hdiutil >/dev/null 2>&1; then
    create_dmg_with_hdiutil
elif [[ "$(uname -s)" == "Linux" ]] && find_mkisofs_hfsplus >/dev/null; then
    create_dmg_with_mkisofs
elif [[ "$(uname -s)" == "Linux" ]]; then
    create_dmg_with_hfsplus
else
    echo "DMG packaging requires hdiutil on macOS, or mkisofs with HFS+ support or mkfs.hfsplus on Linux." >&2
    exit 1
fi

echo "created $artifact"
