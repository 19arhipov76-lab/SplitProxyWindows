param([switch]$Force)
$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Third = Join-Path $Root 'third_party'
New-Item -ItemType Directory -Force -Path $Third | Out-Null

$RouteDir = Join-Path $Third 'wfp-divert'
$RouteCommit = '8179c800dafa9efe8f8f27d34098b306c7affd7b'
if ($Force -and (Test-Path $RouteDir)) { Remove-Item -Recurse -Force $RouteDir }
if (-not (Test-Path (Join-Path $RouteDir '.git'))) {
    git clone https://github.com/okatodev/wfp-divert.git $RouteDir
}
pushd $RouteDir
git fetch --all --tags
git checkout $RouteCommit
popd

$WinZip = Join-Path $Third 'WinDivert-2.2.2-A.zip'
$WinUrl = 'https://github.com/basil00/WinDivert/releases/download/v2.2.2/WinDivert-2.2.2-A.zip'
$Expected = '63cb41763bb4b20f600b6de04e991a9c2be73279e317d4d82f237b150c5f3f15'
if (-not (Test-Path $WinZip)) { Invoke-WebRequest -Uri $WinUrl -OutFile $WinZip }
$Actual = (Get-FileHash $WinZip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($Actual -ne $Expected) { throw "WinDivert SHA256 mismatch: $Actual" }
$WinExtract = Join-Path $Third 'windivert-dist'
if (Test-Path $WinExtract) { Remove-Item -Recurse -Force $WinExtract }
Expand-Archive $WinZip $WinExtract
$WinRoot = Get-ChildItem $WinExtract -Directory | Select-Object -First 1
if (-not $WinRoot) { throw 'WinDivert archive layout unexpected.' }

$Dst = Join-Path $RouteDir 'windivert'
New-Item -ItemType Directory -Force -Path (Join-Path $Dst 'x64') | Out-Null
Copy-Item (Join-Path $WinRoot.FullName 'include\windivert.h') (Join-Path $Dst 'windivert.h') -Force
Copy-Item (Join-Path $WinRoot.FullName 'x64\WinDivert.dll') (Join-Path $Dst 'x64\WinDivert.dll') -Force
Copy-Item (Join-Path $WinRoot.FullName 'x64\WinDivert.lib') (Join-Path $Dst 'x64\WinDivert.lib') -Force
Copy-Item (Join-Path $WinRoot.FullName 'x64\WinDivert64.sys') (Join-Path $Dst 'x64\WinDivert64.sys') -Force

# Preserve third-party licenses for redistribution.
Copy-Item (Join-Path $RouteDir 'LICENSE') (Join-Path $Root 'licenses\wfp-divert-MIT.txt') -Force
$licenseCandidate = Get-ChildItem $WinRoot.FullName -Recurse -File | Where-Object { $_.Name -match 'LICENSE|COPYING' } | Select-Object -First 1
if ($licenseCandidate) { Copy-Item $licenseCandidate.FullName (Join-Path $Root 'licenses\WinDivert-LICENSE.txt') -Force }

# LGPL compliance: ship the exact WinDivert source archive alongside the binary distribution.
$WinSourceZip = Join-Path $Third 'WinDivert-2.2.2-Source.zip'
$WinSourceUrl = 'https://github.com/basil00/WinDivert/releases/download/v2.2.2/WinDivert-2.2.2-Source.zip'
$WinSourceExpected = '65ec79c9e6afa99f648a3f4d1f6db794640b40d0b65bd438770ea503ee14ecb7'
if (-not (Test-Path $WinSourceZip)) { Invoke-WebRequest -Uri $WinSourceUrl -OutFile $WinSourceZip }
$WinSourceActual = (Get-FileHash $WinSourceZip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($WinSourceActual -ne $WinSourceExpected) { throw "WinDivert source SHA256 mismatch: $WinSourceActual" }
Copy-Item $WinSourceZip (Join-Path $Root 'licenses\WinDivert-2.2.2-Source.zip') -Force

Write-Host '[OK] Dependencies prepared.' -ForegroundColor Green
