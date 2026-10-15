param([switch]$Force)
$ErrorActionPreference = 'Stop'
$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Third = Join-Path $Root 'third_party'
New-Item -ItemType Directory -Force -Path $Third | Out-Null

# Selective process router. Pin exact audited commit, then apply our small
# SOCKS5 username/password patch required by Split Proxy Windows.
$RouteDir = Join-Path $Third 'wfp-divert'
$RouteCommit = '8179c800dafa9efe8f8f27d34098b306c7affd7b'
$RoutePatch = Join-Path $Root 'patches\wfp-divert-socks-auth.patch'
if ($Force -and (Test-Path $RouteDir)) { Remove-Item -Recurse -Force $RouteDir }
if (-not (Test-Path (Join-Path $RouteDir '.git'))) {
    git clone https://github.com/okatodev/wfp-divert.git $RouteDir
}
pushd $RouteDir
try {
    git fetch --all --tags
    git checkout --force $RouteCommit
    git reset --hard $RouteCommit
    git clean -fdx
    git apply --check $RoutePatch
    git apply $RoutePatch
} finally {
    popd
}

# Official WinDivert 2.2.2 binary release. This release publishes one binary
# asset named WinDivert-2.2.2-A.zip; there is no *-Source.zip release asset.
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

# Preserve third-party license notices for redistribution.
New-Item -ItemType Directory -Force -Path (Join-Path $Root 'licenses') | Out-Null
Copy-Item (Join-Path $RouteDir 'LICENSE') (Join-Path $Root 'licenses\wfp-divert-MIT.txt') -Force
$licenseCandidate = Get-ChildItem $WinRoot.FullName -Recurse -File | Where-Object { $_.Name -match 'LICENSE|COPYING' } | Select-Object -First 1
if ($licenseCandidate) {
    Copy-Item $licenseCandidate.FullName (Join-Path $Root 'licenses\WinDivert-LICENSE.txt') -Force
}
@"
WinDivert source corresponding to the bundled 2.2.2 binary:
https://github.com/basil00/WinDivert/tree/v2.2.2
Pinned tag commit: 1789526ecfb9ff5397c94f9f54c1a3dc2fb60440
"@ | Set-Content -Encoding UTF8 (Join-Path $Root 'licenses\WinDivert-SOURCE.txt')

Write-Host '[OK] Dependencies prepared.' -ForegroundColor Green
