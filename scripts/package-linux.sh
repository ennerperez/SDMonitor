#!/usr/bin/env bash
set -euo pipefail

binary="${1:?binary path required}"
version="${2:?version required}"
dist_root="${3:?dist root required}"
rid="${4:-linux-x64}"

make_abs() {
    case "$1" in
        /*) printf '%s\n' "$1" ;;
        *) printf '%s\n' "$PWD/$1" ;;
    esac
}

package_root="${PACKAGE_ROOT:-${TMPDIR:-/tmp}/sdmonitor-package-linux}"
assets_root="${ASSETS_ROOT:-packaging/linux}"
package_name="${PACKAGE_NAME:-sdmonitor}"
app_id="${APP_ID:-dev.ennerperez.sdmonitor}"
maintainer="${PACKAGE_MAINTAINER:-SDMonitor Maintainers <ennerperez@gmail.com>}"
description="${PACKAGE_DESCRIPTION:-Stream Deck Mini Hardware Monitor}"
license="${PACKAGE_LICENSE:-MIT}"
homepage="${PACKAGE_HOMEPAGE:-https://github.com/ennerperez/SDMonitor}"
flatpak_runtime="${FLATPAK_RUNTIME:-org.freedesktop.Platform}"
flatpak_sdk="${FLATPAK_SDK:-org.freedesktop.Sdk}"
flatpak_runtime_version="${FLATPAK_RUNTIME_VERSION:-24.08}"
flatpak_branch="${FLATPAK_BRANCH:-stable}"
linux_package_formats="${LINUX_PACKAGE_FORMATS:-deb rpm flatpak appimage}"
license_path="$(make_abs LICENSE)"
binary="$(make_abs "$binary")"
dist_root="$(make_abs "$dist_root")"
package_root="$(make_abs "$package_root")"
assets_root="$(make_abs "$assets_root")"

if [[ ! -f "$binary" ]]; then
    echo "Linux binary not found: $binary" >&2
    exit 1
fi

if [[ "$rid" != "linux-x64" ]]; then
    echo "Unsupported Linux RID for packaging: $rid" >&2
    exit 1
fi

require_command() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "Missing required command: $1" >&2
        echo "Install packaging tools: dpkg-deb fakeroot rpmbuild flatpak-builder flatpak curl" >&2
        exit 1
    fi
}

copy_linux_assets() {
    local root="$1"
    install -Dm755 "$binary" "$root/usr/bin/$package_name"
    install -Dm644 "$assets_root/$app_id.desktop" "$root/usr/share/applications/$app_id.desktop"
    install -Dm644 "$assets_root/$app_id.svg" "$root/usr/share/icons/hicolor/scalable/apps/$app_id.svg"
    install -Dm644 "$assets_root/$app_id.metainfo.xml" "$root/usr/share/metainfo/$app_id.metainfo.xml"
    install -Dm644 "$license_path" "$root/usr/share/doc/$package_name/copyright"
}

deb_version="${version//+/.}"
deb_version="${deb_version//-/\~}"

rpm_version="${version%%[-+]*}"
rpm_release="1"
if [[ "$version" == *-* ]]; then
    prerelease="${version#*-}"
    prerelease="${prerelease%%+*}"
    prerelease="${prerelease//[^A-Za-z0-9._]/.}"
    rpm_release="0.${prerelease}"
fi

mkdir -p "$dist_root" "$package_root"

create_deb() {
    require_command dpkg-deb
    require_command fakeroot

    local root="$package_root/deb/root"
    local artifact="$dist_root/$package_name.deb"

    rm -rf "$root"
    mkdir -p "$root/DEBIAN"
    chmod 0755 "$root/DEBIAN"
    copy_linux_assets "$root"

    cat > "$root/DEBIAN/control" <<CONTROL
Package: $package_name
Version: $deb_version
Section: utils
Priority: optional
Architecture: amd64
Maintainer: $maintainer
Depends: libc6, libudev1
Homepage: $homepage
Description: $description
 SDMonitor controls an Elgato Stream Deck Mini and renders system metrics.
CONTROL

    fakeroot dpkg-deb --build "$root" "$artifact" >/dev/null
    echo "created $artifact"
}

create_rpm() {
    require_command rpmbuild

    local topdir="$package_root/rpm"
    local spec="$topdir/SPECS/$package_name.spec"
    local artifact="$dist_root/$package_name.rpm"

    rm -rf "$topdir"
    mkdir -p "$topdir/BUILD" "$topdir/BUILDROOT" "$topdir/RPMS" "$topdir/SOURCES" "$topdir/SPECS" "$topdir/SRPMS" "$topdir/TMP"

    cat > "$spec" <<SPEC
Name: $package_name
Version: $rpm_version
Release: $rpm_release%{?dist}
Summary: $description
License: $license
URL: $homepage
BuildArch: x86_64
Requires: glibc, libudev

%description
SDMonitor controls an Elgato Stream Deck Mini and renders system metrics.

%prep

%build

%install
install -Dm0755 "$binary" "%{buildroot}/usr/bin/$package_name"
install -Dm0644 "$assets_root/$app_id.desktop" "%{buildroot}/usr/share/applications/$app_id.desktop"
install -Dm0644 "$assets_root/$app_id.svg" "%{buildroot}/usr/share/icons/hicolor/scalable/apps/$app_id.svg"
install -Dm0644 "$assets_root/$app_id.metainfo.xml" "%{buildroot}/usr/share/metainfo/$app_id.metainfo.xml"
install -Dm0644 "$license_path" "%{buildroot}/usr/share/doc/$package_name/copyright"

%files
/usr/bin/$package_name
/usr/share/applications/$app_id.desktop
/usr/share/icons/hicolor/scalable/apps/$app_id.svg
/usr/share/metainfo/$app_id.metainfo.xml
/usr/share/doc/$package_name/copyright
SPEC

    rpmbuild -bb --quiet "$spec" \
        --define "_topdir $(cd "$topdir" && pwd)" \
        --define "_tmppath $(cd "$topdir/TMP" && pwd)" \
        --define "source_date_epoch_from_changelog 0" \
        >/dev/null
    find "$topdir/RPMS" -type f -name '*.rpm' -exec cp {} "$artifact" \;
    if [[ ! -f "$artifact" ]]; then
        echo "RPM artifact not found" >&2
        exit 1
    fi
    echo "created $artifact"
}

create_flatpak() {
    require_command flatpak-builder
    require_command flatpak

    local work="$package_root/flatpak"
    local manifest="$work/$app_id.json"
    local repo="$work/repo"
    local build="$work/build"
    local state="$work/state"
    local artifact="$dist_root/$package_name.flatpak"
    local binary_abs
    local assets_abs

    binary_abs="$(cd "$(dirname "$binary")" && pwd)/$(basename "$binary")"
    assets_abs="$(cd "$assets_root" && pwd)"

    rm -rf "$work"
    mkdir -p "$work"

    cat > "$manifest" <<MANIFEST
{
  "app-id": "$app_id",
  "runtime": "$flatpak_runtime",
  "runtime-version": "$flatpak_runtime_version",
  "sdk": "$flatpak_sdk",
  "command": "$package_name",
  "finish-args": [
    "--device=all",
    "--filesystem=xdg-config/SDMonitor:create",
    "--share=network"
  ],
  "modules": [
    {
      "name": "$package_name",
      "buildsystem": "simple",
      "build-commands": [
        "install -Dm755 sdmonitor /app/bin/$package_name",
        "install -Dm644 $app_id.desktop /app/share/applications/$app_id.desktop",
        "install -Dm644 $app_id.svg /app/share/icons/hicolor/scalable/apps/$app_id.svg",
        "install -Dm644 $app_id.metainfo.xml /app/share/metainfo/$app_id.metainfo.xml"
      ],
      "sources": [
        { "type": "file", "path": "$binary_abs", "dest-filename": "sdmonitor" },
        { "type": "file", "path": "$assets_abs/$app_id.desktop" },
        { "type": "file", "path": "$assets_abs/$app_id.svg" },
        { "type": "file", "path": "$assets_abs/$app_id.metainfo.xml" }
      ]
    }
  ]
}
MANIFEST

    flatpak-builder --force-clean --disable-rofiles-fuse --state-dir="$state" --repo="$repo" --default-branch="$flatpak_branch" "$build" "$manifest" >/dev/null
    flatpak build-bundle "$repo" "$artifact" "$app_id" "$flatpak_branch" >/dev/null
    echo "created $artifact"
}

find_appimagetool() {
    if [[ -n "${APPIMAGETOOL:-}" ]]; then
        echo "$APPIMAGETOOL"
        return
    fi

    if command -v appimagetool >/dev/null 2>&1; then
        command -v appimagetool
        return
    fi

    require_command curl

    local tool_dir="${TOOLS_ROOT:-artifacts/tools}"
    local tool="$tool_dir/appimagetool-x86_64.AppImage"
    mkdir -p "$tool_dir"

    if [[ ! -f "$tool" ]]; then
        curl -fsSL \
            https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage \
            -o "$tool"
        chmod +x "$tool"
    fi

    echo "$tool"
}

create_appimage() {
    local work="$package_root/appimage"
    local appdir="$work/SDMonitor.AppDir"
    local artifact="$dist_root/$package_name.AppImage"
    local tool

    rm -rf "$work"
    mkdir -p "$appdir/usr/bin" "$appdir/usr/share/applications" "$appdir/usr/share/icons/hicolor/scalable/apps"

    copy_linux_assets "$appdir"
    cp "$assets_root/$app_id.metainfo.xml" "$appdir/usr/share/metainfo/$app_id.appdata.xml"
    cp "$assets_root/$app_id.desktop" "$appdir/$app_id.desktop"
    cp "$assets_root/$app_id.svg" "$appdir/$app_id.svg"
    cp "$assets_root/$app_id.svg" "$appdir/.DirIcon"

    cat > "$appdir/AppRun" <<'APPRUN'
#!/usr/bin/env bash
here="$(dirname "$(readlink -f "$0")")"
exec "$here/usr/bin/sdmonitor" "$@"
APPRUN
    chmod +x "$appdir/AppRun"

    tool="$(find_appimagetool)"
    ARCH=x86_64 APPIMAGE_EXTRACT_AND_RUN=1 "$tool" "$appdir" "$artifact" >/dev/null
    chmod +x "$artifact"
    echo "created $artifact"
}

for format in $linux_package_formats; do
    case "$format" in
        deb) create_deb ;;
        rpm) create_rpm ;;
        flatpak) create_flatpak ;;
        appimage) create_appimage ;;
        *)
            echo "Unsupported Linux package format: $format" >&2
            exit 1
            ;;
    esac
done
