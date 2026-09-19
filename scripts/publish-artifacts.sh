#!/usr/bin/env bash
set -euo pipefail

configuration="${CONFIGURATION:-Release}"
project="${PROJECT:-src/SDMonitor/SDMonitor.csproj}"
publish_root="${PUBLISH_ROOT:-artifacts/publish}"
dist_root="${DIST_ROOT:-artifacts/dist}"
rids=("$@")

if [ "${#rids[@]}" -eq 0 ]; then
    rids=("win-x64" "linux-x64" "osx-arm64")
fi

dotnet tool restore --verbosity quiet >/dev/null

gitversion_variable() {
    dotnet tool run dotnet-gitversion -- /output json /showvariable "$1" /nocache
}

version="$(gitversion_variable SemVer)"
assembly_version="$(gitversion_variable AssemblySemVer)"
file_version="$(gitversion_variable AssemblySemFileVer)"
informational_version="$(gitversion_variable InformationalVersion)"

mkdir -p "$publish_root" "$dist_root"

for rid in "${rids[@]}"; do
    extension=""
    if [[ "$rid" == win-* ]]; then
        extension=".exe"
    fi

    rid_output="$publish_root/$rid"
    artifact="$dist_root/sdmonitor-$version-$rid$extension"

    rm -rf "$rid_output"
    dotnet publish "$project" \
        --verbosity quiet \
        --configuration "$configuration" \
        --runtime "$rid" \
        --self-contained true \
        --output "$rid_output" \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:DebugType=None \
        -p:DebugSymbols=false \
        -p:GenerateAssemblyInfo=true \
        -p:Version="$version" \
        -p:AssemblyVersion="$assembly_version" \
        -p:FileVersion="$file_version" \
        -p:InformationalVersion="$informational_version"

    cp "$rid_output/sdmonitor$extension" "$artifact"
    if [[ "$rid" != win-* ]]; then
        chmod +x "$artifact"
    fi

    echo "created $artifact"
done
