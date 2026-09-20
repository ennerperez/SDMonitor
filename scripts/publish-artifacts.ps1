param(
    [string[]] $Runtime = @("win-x64", "linux-x64", "osx-arm64"),
    [string] $Configuration = "Release",
    [string] $Project = "src/SDMonitor/SDMonitor.csproj",
    [string] $PublishRoot = "artifacts/publish",
    [string] $DistRoot = "artifacts/dist"
)

$ErrorActionPreference = "Stop"

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
    $artifact = Join-Path $DistRoot "sdmonitor-$version-$rid$extension"

    if (Test-Path $ridOutput) {
        Remove-Item -Recurse -Force $ridOutput
    }

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
}
