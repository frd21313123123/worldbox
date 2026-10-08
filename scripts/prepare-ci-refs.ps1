param(
  [Parameter(Mandatory=$true)][string]$WorldBoxDir,
  [Parameter(Mandatory=$true)][string]$SteamDll,
  [Parameter(Mandatory=$true)][string]$Output
)
$ErrorActionPreference = 'Stop'
$WorldBoxDir = (Resolve-Path $WorldBoxDir).Path
$SteamDll = (Resolve-Path $SteamDll).Path
$tmp = Join-Path ([IO.Path]::GetTempPath()) ('wbmp-refs-' + [Guid]::NewGuid().ToString('N'))
try {
  $managed = Join-Path $WorldBoxDir 'worldbox_Data\Managed'
  $bep = Join-Path $WorldBoxDir 'BepInEx\core\BepInEx.dll'
  $outManaged = Join-Path $tmp 'game/worldbox_Data/Managed'
  New-Item -ItemType Directory -Path $outManaged -Force | Out-Null
  foreach ($name in @('UnityEngine.CoreModule.dll','UnityEngine.InputLegacyModule.dll','UnityEngine.IMGUIModule.dll')) {
    Copy-Item (Join-Path $managed $name) (Join-Path $outManaged $name)
  }
  $outCore = Join-Path $tmp 'game/BepInEx/core'
  New-Item -ItemType Directory -Path $outCore -Force | Out-Null
  Copy-Item $bep (Join-Path $outCore 'BepInEx.dll')
  Copy-Item $SteamDll (Join-Path $tmp 'Steamworks.NET.dll')
  $Output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Output)
  if (Test-Path $Output) { Remove-Item -Force $Output }
  Compress-Archive -Path (Join-Path $tmp '*') -DestinationPath $Output
  $hash = (Get-FileHash $Output -Algorithm SHA256).Hash
  Write-Host "Private archive: $Output"
  Write-Host "SHA256: $hash"
  Write-Host 'Do NOT commit or publicly upload this bundle; it contains third-party/game binaries.'
} finally { if (Test-Path $tmp) { Remove-Item $tmp -Force -Recurse } }
