param(
    [string[]] $Runtime = @("win-x64", "linux-x64", "osx-arm64"),
    [string] $Configuration = "Release",
    [string] $Project = "src/SDMonitor/SDMonitor.csproj",
    [string] $PublishRoot = "artifacts/publish",
    [string] $DistRoot = "artifacts/dist"
)

$ErrorActionPreference = "Stop"

$bash = Get-Command bash -ErrorAction SilentlyContinue
if ($bash) {
    & $bash.Source "scripts/install-packaging-tools.sh" @Runtime
    if ($LASTEXITCODE -ne 0) {
        throw "Packaging tool installation failed"
    }
}

dotnet tool restore --verbosity quiet | Out-Null

function Get-GitVersionVariable {
    param([string] $Name)
    dotnet tool run dotnet-gitversion -- /output json /showvariable $Name /nocache
}

$version = Get-GitVersionVariable "SemVer"
$assemblyVersion = Get-GitVersionVariable "AssemblySemVer"
$fileVersion = Get-GitVersionVariable "AssemblySemFileVer"
$informationalVersion = Get-GitVersionVariable "InformationalVersion"

New-Item -ItemType Directory -Force -Path $PublishRoot, $DistRoot | Out-Null

foreach ($rid in $Runtime) {
    $extension = if ($rid.StartsWith("win-")) { ".exe" } else { "" }
    $ridOutput = Join-Path $PublishRoot $rid
    $ridDist = Join-Path $DistRoot $rid
    $artifact = Join-Path $ridDist "sdmonitor$extension"

    if (Test-Path $ridOutput) {
        Remove-Item -Recurse -Force $ridOutput
    }
    if (Test-Path $ridDist) {
        Remove-Item -Recurse -Force $ridDist
    }
    New-Item -ItemType Directory -Force -Path $ridDist | Out-Null

    dotnet publish $Project `
        --verbosity quiet `
        --configuration $Configuration `
        --runtime $rid `
        --self-contained true `
        --output $ridOutput `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -p:GenerateAssemblyInfo=true `
        -p:Version=$version `
        -p:AssemblyVersion=$assemblyVersion `
        -p:FileVersion=$fileVersion `
        -p:InformationalVersion=$informationalVersion

    $publishedBinary = Join-Path $ridOutput "SDMonitor$extension"
    if (-not (Test-Path $publishedBinary)) {
        $publishedBinary = Join-Path $ridOutput "sdmonitor$extension"
    }

    if (-not (Test-Path $publishedBinary)) {
        throw "Published binary not found in $ridOutput"
    }

    Copy-Item $publishedBinary $artifact -Force
    if (-not $rid.StartsWith("win-")) {
        chmod +x $artifact
    }

    Write-Output "created $artifact"

    if ($rid.StartsWith("linux-")) {
        $bash = Get-Command bash -ErrorAction SilentlyContinue
        if (-not $bash) {
            throw "bash is required to generate Linux .deb, .rpm, .flatpak, and .AppImage packages"
        }

        & $bash.Source "scripts/package-linux.sh" $publishedBinary $version $ridDist $rid
        if ($LASTEXITCODE -ne 0) {
            throw "Linux package generation failed"
        }
    }

    if ($rid.StartsWith("osx-")) {
        $bash = Get-Command bash -ErrorAction SilentlyContinue
        if (-not $bash) {
            throw "bash is required to generate macOS DMG packages"
        }

        & $bash.Source "scripts/package-macos.sh" $publishedBinary $version $ridDist $rid $fileVersion
        if ($LASTEXITCODE -ne 0) {
            throw "macOS DMG package generation failed"
        }
    }
}
