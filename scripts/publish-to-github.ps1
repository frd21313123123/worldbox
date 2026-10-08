param(
  [string]$Repository = 'WorldBox-Multiplayer',
  [ValidateSet('public','private')][string]$Visibility = 'public'
)
$ErrorActionPreference = 'Stop'
if (!(Get-Command gh -ErrorAction SilentlyContinue)) {
  throw 'Install GitHub CLI from https://cli.github.com/ before publishing.'
}
& gh auth status
if ($LASTEXITCODE -ne 0) { throw 'Run gh auth login once to authorize repository creation.' }
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $root
try {
  if (!(Test-Path '.git')) { git init -b main }
  git add .
  git diff --cached --quiet
  if ($LASTEXITCODE -ne 0) { git commit -m 'Add multiplayer MVP with Worldfall compatibility and CI' }
  $visibilityArg = if ($Visibility -eq 'private') { '--private' } else { '--public' }
  gh repo create $Repository $visibilityArg --source . --remote origin --push
  if ($LASTEXITCODE -ne 0) { throw 'GitHub repository creation/push failed' }
  Write-Host ('Repository created: ' + ((gh repo view --json url --jq '.url') -join ''))
} finally { Pop-Location }
