param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Dist = Join-Path $Root 'dist'
$Stage = Join-Path $Dist 'stage'
if (Test-Path $Dist) { Remove-Item -Recurse -Force $Dist }
New-Item -ItemType Directory -Force -Path $Stage | Out-Null

& (Join-Path $PSScriptRoot 'Get-Dependencies.ps1')

# Build route engine (MIT wfp-divert + LGPL WinDivert dynamic library/driver).
$Route = Join-Path $Root 'third_party\wfp-divert'
$RouteBuild = Join-Path $Route 'build-splitproxy'
cmake -S $Route -B $RouteBuild -A x64 -DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded
cmake --build $RouteBuild --config $Configuration -- /m
$RouteExe = Join-Path $RouteBuild "$Configuration\wfp_divert.exe"
if (-not (Test-Path $RouteExe)) { throw 'Route engine build failed.' }

# Publish .NET components self-contained: no .NET Runtime required on client.
$AppOut = Join-Path $Stage 'app'
$ServiceOut = Join-Path $Stage 'service'
$UpdaterOut = Join-Path $Stage 'updater'
dotnet publish (Join-Path $Root 'src\SplitProxy.App\SplitProxy.App.csproj') -c $Configuration -r win-x64 --self-contained true -o $AppOut
dotnet publish (Join-Path $Root 'src\SplitProxy.Service\SplitProxy.Service.csproj') -c $Configuration -r win-x64 --self-contained true -o $ServiceOut
dotnet publish (Join-Path $Root 'src\SplitProxy.Updater\SplitProxy.Updater.csproj') -c $Configuration -r win-x64 --self-contained true -o $UpdaterOut

# Friendly final names.
$AppExe = Get-ChildItem $AppOut -Filter 'SplitProxy.App.exe' | Select-Object -First 1
if ($AppExe) { Rename-Item $AppExe.FullName 'SplitProxy.exe' -Force }

$EngineOut = Join-Path $Stage 'engine'
New-Item -ItemType Directory -Force -Path $EngineOut | Out-Null
Copy-Item $RouteExe (Join-Path $EngineOut 'SplitProxy.RouteEngine.exe') -Force
Copy-Item (Join-Path $RouteBuild "$Configuration\WinDivert.dll") $EngineOut -Force
Copy-Item (Join-Path $RouteBuild "$Configuration\WinDivert64.sys") $EngineOut -Force

$LicOut = Join-Path $Stage 'licenses'
New-Item -ItemType Directory -Force -Path $LicOut | Out-Null
Copy-Item (Join-Path $Root 'licenses\*') $LicOut -Force

# Inno Setup compiler.
$IsccCandidates = @(
  "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ }
$ISCC = $IsccCandidates | Select-Object -First 1
if (-not $ISCC) { throw 'Inno Setup 6 not found.' }
& $ISCC (Join-Path $Root 'installer\SplitProxy.iss')
Write-Host "[OK] Installer: $Dist\SplitProxySetup.exe" -ForegroundColor Green
