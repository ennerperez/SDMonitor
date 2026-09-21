#!/usr/bin/env bash
set -euo pipefail

rids=("$@")
linux_package_formats="${LINUX_PACKAGE_FORMATS:-deb rpm flatpak appimage}"
flatpak_runtime="${FLATPAK_RUNTIME:-org.freedesktop.Platform}"
flatpak_sdk="${FLATPAK_SDK:-org.freedesktop.Sdk}"
flatpak_runtime_version="${FLATPAK_RUNTIME_VERSION:-24.08}"

if [[ "${#rids[@]}" -eq 0 ]]; then
    rids=("win-x64" "linux-x64" "osx-arm64")
fi

has_rid_prefix() {
    local prefix="$1"
    local rid
    for rid in "${rids[@]}"; do
        [[ "$rid" == "$prefix"* ]] && return 0
    done
    return 1
}

has_format() {
    local wanted="$1"
    local format
    for format in $linux_package_formats; do
        [[ "$format" == "$wanted" ]] && return 0
    done
    return 1
}

run_root() {
    if [[ "$(id -u)" -eq 0 ]]; then
        "$@"
    else
        sudo "$@"
    fi
}

has_mkisofs_hfsplus() {
    local tool
    local tool_path

    for tool in xorrisofs mkisofs genisoimage; do
        if tool_path="$(command -v "$tool" 2>/dev/null)" && "$tool_path" --help 2>&1 | grep -q -- "-hfsplus"; then
            return 0
        fi
    done

    return 1
}

install_linux_packages() {
    local missing=()
    local command_name
    local required_commands=(curl)

    if has_format deb; then
        required_commands+=(dpkg-deb fakeroot)
    fi

    if has_format rpm; then
        required_commands+=(rpmbuild)
    fi

    if has_format flatpak; then
        required_commands+=(flatpak-builder flatpak)
    fi

    for command_name in "${required_commands[@]}"; do
        if ! command -v "$command_name" >/dev/null 2>&1; then
            missing+=("$command_name")
        fi
    done

    if [[ "${#missing[@]}" -eq 0 ]]; then
        return
    fi

    echo "installing Linux packaging tools: ${missing[*]}"

    if command -v apt-get >/dev/null 2>&1; then
        run_root apt-get update
        local apt_packages=(curl)
        has_format deb && apt_packages+=(dpkg fakeroot)
        has_format rpm && apt_packages+=(rpm)
        has_format flatpak && apt_packages+=(flatpak flatpak-builder)
        run_root apt-get install -y "${apt_packages[@]}"
        return
    fi

    if command -v dnf >/dev/null 2>&1; then
        local dnf_packages=(curl)
        has_format deb && dnf_packages+=(dpkg fakeroot)
        has_format rpm && dnf_packages+=(rpm-build)
        has_format flatpak && dnf_packages+=(flatpak flatpak-builder)
        run_root dnf install -y "${dnf_packages[@]}"
        return
    fi

    if command -v pacman >/dev/null 2>&1; then
        local pacman_packages=(curl)
        has_format deb && pacman_packages+=(dpkg fakeroot)
        has_format rpm && pacman_packages+=(rpm-tools)
        has_format flatpak && pacman_packages+=(flatpak flatpak-builder)
        run_root pacman -Sy --needed --noconfirm "${pacman_packages[@]}"
        return
    fi

    echo "Missing Linux packaging tools: ${missing[*]}" >&2
    echo "Install them manually, then rerun publish." >&2
    exit 1
}

install_flatpak_runtime() {
    if ! has_format flatpak; then
        return
    fi

    if flatpak info --system "$flatpak_runtime//$flatpak_runtime_version" >/dev/null 2>&1 \
        && flatpak info --system "$flatpak_sdk//$flatpak_runtime_version" >/dev/null 2>&1; then
        return
    fi

    echo "installing Flatpak runtime $flatpak_runtime//$flatpak_runtime_version"
    run_root flatpak remote-add --system --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo
    run_root flatpak install -y --system flathub \
        "$flatpak_runtime//$flatpak_runtime_version" \
        "$flatpak_sdk//$flatpak_runtime_version"
}

install_appimagetool() {
    if ! has_format appimage; then
        return
    fi

    if command -v appimagetool >/dev/null 2>&1; then
        return
    fi

    local tool_dir="${TOOLS_ROOT:-artifacts/tools}"
    local tool="$tool_dir/appimagetool-x86_64.AppImage"

    if [[ -x "$tool" ]]; then
        return
    fi

    mkdir -p "$tool_dir"
    echo "installing appimagetool to $tool"
    curl -fsSL \
        https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage \
        -o "$tool"
    chmod +x "$tool"
}

check_macos_tools() {
    if ! has_rid_prefix osx-; then
        return
    fi

    if [[ "$(uname -s)" == "Linux" ]]; then
        if has_mkisofs_hfsplus || command -v mkfs.hfsplus >/dev/null 2>&1; then
            return
        fi

        echo "installing Linux DMG tool: xorriso"
        if command -v apt-get >/dev/null 2>&1; then
            run_root apt-get update
            run_root apt-get install -y xorriso
            return
        fi

        if command -v dnf >/dev/null 2>&1; then
            run_root dnf install -y xorriso
            return
        fi

        if command -v pacman >/dev/null 2>&1; then
            run_root pacman -Sy --needed --noconfirm xorriso
            return
        fi

        echo "Missing Linux DMG tool: mkisofs with HFS+ support or mkfs.hfsplus" >&2
        echo "Install xorriso, hfsprogs, or hfsplus-tools, then rerun publish." >&2
        exit 1
    fi

    if [[ "$(uname -s)" != "Darwin" ]]; then
        echo "DMG packaging requires hdiutil on macOS or mkfs.hfsplus on Linux." >&2
        exit 1
    fi

    if command -v hdiutil >/dev/null 2>&1; then
        return
    fi

    echo "Missing required command: hdiutil" >&2
    exit 1
}

if has_rid_prefix linux-; then
    install_linux_packages
    install_flatpak_runtime
    install_appimagetool
fi

check_macos_tools
