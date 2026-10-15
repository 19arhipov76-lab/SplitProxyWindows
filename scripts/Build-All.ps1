param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'

$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Dist = Join-Path $Root 'dist'
$Stage = Join-Path $Dist 'stage'

if (Test-Path $Dist) {
    Remove-Item -Recurse -Force $Dist
}

New-Item -ItemType Directory -Force -Path $Stage | Out-Null

& (Join-Path $PSScriptRoot 'Get-Dependencies.ps1')

# Build route engine.
$Route = Join-Path $Root 'third_party\wfp-divert'
$RouteBuild = Join-Path $Route 'build-splitproxy'

cmake `
    -S $Route `
    -B $RouteBuild `
    -A x64 `
    -DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded

cmake --build $RouteBuild --config $Configuration -- /m

$RouteExe = Join-Path $RouteBuild "$Configuration\wfp_divert.exe"

if (-not (Test-Path $RouteExe)) {
    throw 'Route engine build failed.'
}

# Publish .NET components self-contained.
$AppOut = Join-Path $Stage 'app'
$ServiceOut = Join-Path $Stage 'service'
$UpdaterOut = Join-Path $Stage 'updater'

dotnet publish `
    (Join-Path $Root 'src\SplitProxy.App\SplitProxy.App.csproj') `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -o $AppOut

dotnet publish `
    (Join-Path $Root 'src\SplitProxy.Service\SplitProxy.Service.csproj') `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -o $ServiceOut

dotnet publish `
    (Join-Path $Root 'src\SplitProxy.Updater\SplitProxy.Updater.csproj') `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -o $UpdaterOut

# Friendly final application name.
$AppExe = Get-ChildItem `
    $AppOut `
    -Filter 'SplitProxy.App.exe' `
    | Select-Object -First 1

if (-not $AppExe) {
    throw 'SplitProxy.App.exe was not produced.'
}

Rename-Item `
    $AppExe.FullName `
    'SplitProxy.exe' `
    -Force

# Route engine.
$EngineOut = Join-Path $Stage 'engine'

New-Item `
    -ItemType Directory `
    -Force `
    -Path $EngineOut `
    | Out-Null

Copy-Item `
    $RouteExe `
    (Join-Path $EngineOut 'SplitProxy.RouteEngine.exe') `
    -Force

Copy-Item `
    (Join-Path $RouteBuild "$Configuration\WinDivert.dll") `
    $EngineOut `
    -Force

Copy-Item `
    (Join-Path $RouteBuild "$Configuration\WinDivert64.sys") `
    $EngineOut `
    -Force

# Licenses.
$LicOut = Join-Path $Stage 'licenses'

New-Item `
    -ItemType Directory `
    -Force `
    -Path $LicOut `
    | Out-Null

Copy-Item `
    (Join-Path $Root 'licenses\*') `
    $LicOut `
    -Force

# Find Inno Setup compiler.
$ISCC = $null

$IsccCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue

if ($IsccCommand) {
    $ISCC = $IsccCommand.Source
}

if (-not $ISCC) {

    $IsccCandidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:ChocolateyInstall\bin\ISCC.exe"
    )

    foreach ($Candidate in $IsccCandidates) {

        if (
            -not [string]::IsNullOrWhiteSpace($Candidate) -and
            (Test-Path $Candidate)
        ) {
            $ISCC = $Candidate
            break
        }
    }
}

if (-not $ISCC) {

    $FoundIscc = Get-ChildItem `
        -Path 'C:\Program Files', 'C:\Program Files (x86)' `
        -Filter 'ISCC.exe' `
        -File `
        -Recurse `
        -ErrorAction SilentlyContinue `
        | Select-Object -First 1

    if ($FoundIscc) {
        $ISCC = $FoundIscc.FullName
    }
}

if (-not $ISCC) {
    throw 'Inno Setup 6 compiler ISCC.exe not found.'
}

Write-Host "[OK] Inno Setup: $ISCC" -ForegroundColor Green

# Build installer.
& $ISCC (Join-Path $Root 'installer\SplitProxy.iss')

$Installer = Join-Path $Dist 'SplitProxySetup.exe'

if (-not (Test-Path $Installer)) {
    throw 'Installer was not produced.'
}

Write-Host "[OK] Installer: $Installer" -ForegroundColor Green
