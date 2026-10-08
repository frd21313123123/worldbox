param(
  [Parameter(Mandatory=$true)][string]$WorldBoxDir,
  [string]$SteamDll = "",
  [switch]$Install
)
$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot '..\src\WorldBoxMultiplayer.csproj'
$tests = Join-Path $PSScriptRoot '..\tests\WireTests.csproj'
if (!(Test-Path (Join-Path $WorldBoxDir 'worldbox_Data\Managed\UnityEngine.CoreModule.dll'))) {
  throw 'This folder is not a WorldBox Windows game installation.'
}
if (!(Test-Path (Join-Path $WorldBoxDir 'BepInEx\core\BepInEx.dll'))) {
  throw 'Install BepInEx 5 into WorldBox first, then launch game once.'
}
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw 'Install .NET SDK 8 or newer and .NET Framework 4.6.2 targeting pack.'
}
if ([string]::IsNullOrEmpty($SteamDll)) {
  $candidates = @(
    (Join-Path $WorldBoxDir 'worldbox_Data\Managed\Steamworks.NET.dll'),
    (Join-Path $WorldBoxDir 'BepInEx\plugins\Steamworks.NET.dll'),
    (Join-Path $PSScriptRoot '..\lib\Steamworks.NET.dll')
  )
  foreach ($c in $candidates) { if (Test-Path $c) { $SteamDll = $c; break } }
}
if ([string]::IsNullOrEmpty($SteamDll) -or !(Test-Path $SteamDll)) {
  throw 'Steamworks.NET.dll not found. Pass -SteamDll C:\path\Steamworks.NET.dll (Mono/.NET Framework edition).'
}
$WorldBoxDir = (Resolve-Path $WorldBoxDir).Path
$SteamDll = (Resolve-Path $SteamDll).Path
Write-Host 'Running pure protocol tests...'
& dotnet run --project $tests --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Protocol tests failed. Build aborted.' }
Write-Host 'Running world snapshot integrity tests...'
& dotnet run --project (Join-Path $PSScriptRoot '..\tests\SnapshotTests.csproj') --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Snapshot tests failed. Build aborted.' }
Write-Host 'Running SaveManager adapter contract tests (fake API)...'
& dotnet run --project (Join-Path $PSScriptRoot '..\tests\GameAdapterContractTests.csproj') --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Save adapter contract tests failed. Build aborted.' }
Write-Host 'Building BepInEx plugin...'
& dotnet build $project --configuration Release -p:WorldBoxDir="$WorldBoxDir" -p:SteamDll="$SteamDll"
if ($LASTEXITCODE -ne 0) { throw 'DLL build failed.' }
$dll = Join-Path $PSScriptRoot '..\src\bin\Release\net462\WorldBoxMultiplayer.dll'
if (!(Test-Path $dll)) { throw "Expected DLL missing: $dll" }
Write-Host "Built: $dll"
if ($Install) {
  $plugins = Join-Path $WorldBoxDir 'BepInEx\plugins\WorldBoxMultiplayer'
  New-Item -ItemType Directory -Force -Path $plugins | Out-Null
  Copy-Item $dll (Join-Path $plugins 'WorldBoxMultiplayer.dll') -Force
  # Keep one Steamworks.NET assembly. If the game does not ship it, deploy the reference.
  if (!(Test-Path (Join-Path $WorldBoxDir 'worldbox_Data\Managed\Steamworks.NET.dll'))) {
    Copy-Item $SteamDll (Join-Path $plugins 'Steamworks.NET.dll') -Force
  }
  Write-Host "Installed into: $plugins"
}
