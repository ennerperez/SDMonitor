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
        echo "macOS DMG packaging must run on macOS with hdiutil available." >&2
        exit 1
    fi
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

require_command hdiutil

work="$package_root/$rid"
bundle="$work/$bundle_name"
dmg_root="$work/dmg-root"
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

rm -f "$artifact"
hdiutil create \
    -volname "$display_name" \
    -srcfolder "$dmg_root" \
    -ov \
    -format UDZO \
    "$artifact" >/dev/null

echo "created $artifact"
