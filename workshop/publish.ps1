#requires -Version 7
<#
.SYNOPSIS
  Build Expedition Framework and upload it to the Steam Workshop with steamcmd.

.DESCRIPTION
  1. Runs the checks (unless -SkipTests): the engine tests in tests/Sim.
  2. Builds Release into workshop/build/content (same layout as Mods/ExpeditionFramework).
  3. Writes workshop/build/item.vdf from workshop/workshop.json.
  4. Runs steamcmd +workshop_build_item and saves the new publishedfileid
     back into workshop.json on the first upload.

  Title, description, preview and visibility are sent on the first upload only.
  After that they are left alone so edits made on the Workshop page stick;
  pass -UpdateMetadata to push them again.

  The build copies ZohFramework.Api.dll from the installed Mods/ZohFramework, so ZoH Framework has to be
  installed there (or pass its path to the csproj as ZohApi).

.EXAMPLE
  ./workshop/publish.ps1 -DryRun
.EXAMPLE
  ./workshop/publish.ps1 -SteamUser mylogin -ChangeNote "Fix a typo"
#>
param(
    [string]$SteamUser = $env:STEAM_USER,
    [string]$ChangeNote,
    [switch]$UpdateMetadata,
    [switch]$SkipTests,
    [switch]$DryRun,
    [string]$SteamCmd = $(if ($env:STEAMCMD) { $env:STEAMCMD } else { 'steamcmd' })
)

$ErrorActionPreference = 'Stop'
$root      = Split-Path $PSScriptRoot -Parent
$configPath = Join-Path $PSScriptRoot 'workshop.json'
$buildDir  = Join-Path $PSScriptRoot 'build'
$contentDir = Join-Path $buildDir 'content'
$vdfPath   = Join-Path $buildDir 'item.vdf'

$config = Get-Content $configPath -Raw | ConvertFrom-Json
[xml]$manifest = Get-Content (Join-Path $root 'manifest.xml') -Raw
$version = $manifest.Manifest.ModVersion
$isNew = [string]$config.publishedfileid -eq '0'

if (-not $ChangeNote) { $ChangeNote = "v$version" }
if (-not $DryRun -and -not $SteamUser) {
    throw 'No Steam login. Pass -SteamUser <login> or set $env:STEAM_USER.'
}

# --- Test & build ---------------------------------------------------------
if (-not $SkipTests) {
    Write-Host '==> Running the engine tests' -ForegroundColor Cyan
    dotnet run -c Release --project (Join-Path $root 'tests/Sim')
    if ($LASTEXITCODE) { throw 'Tests failed.' }
}

Write-Host "==> Building Expedition Framework v$version" -ForegroundColor Cyan
if (Test-Path $buildDir) { Remove-Item $buildDir -Recurse -Force }
New-Item -ItemType Directory -Force $contentDir | Out-Null
# The csproj's InstallMod target copies the mod's files into InstallDir. SelfTests and DevTools stay off: the debug
# panel's self-test and pack-testing buttons are not for players.
$installDir = ($contentDir -replace '\\', '/') + '/'
dotnet build (Join-Path $root 'ExpeditionFramework.csproj') -c Release --nologo "-p:InstallDir=$installDir" -p:SelfTests=false -p:DevTools=false
if ($LASTEXITCODE) { throw 'Build failed.' }

foreach ($required in 'ExpeditionFramework.dll', 'ExpeditionFramework.Api.dll', 'ZohFramework.Api.dll', 'manifest.xml', 'ExpeditionFramework/main.json') {
    if (-not (Test-Path (Join-Path $contentDir $required))) { throw "Staged content is missing $required" }
}
Write-Host '    Staged files:'
Get-ChildItem $contentDir -Recurse -File | ForEach-Object {
    '      ' + [IO.Path]::GetRelativePath($contentDir, $_.FullName)
}

# --- VDF ------------------------------------------------------------------
function Format-Vdf([string]$s) { '"' + ($s -replace '\\', '\\' -replace '"', '\"') + '"' }

$fields = [ordered]@{
    appid           = $config.appid
    publishedfileid = $config.publishedfileid
    contentfolder   = $contentDir
    changenote      = $ChangeNote
}
if ($isNew -or $UpdateMetadata) {
    $preview = Join-Path $root $config.preview
    if (-not (Test-Path $preview)) {
        $missing = "Preview image not found: $preview (set ""preview"" in workshop.json; it must be under 1 MB)"
        if (-not $DryRun) { throw $missing }
        Write-Warning $missing
    }
    elseif ((Get-Item $preview).Length -ge 1MB) { throw "Preview image must be under 1 MB: $preview" }
    $fields.previewfile = $preview
    $fields.visibility  = $config.visibility
    $fields.title       = $config.title
    $fields.description = (Get-Content (Join-Path $root $config.description) -Raw).TrimEnd()
}

$vdf = "`"workshopitem`"`n{`n"
foreach ($k in $fields.Keys) { $vdf += "`t`"$k`"`t$(Format-Vdf ([string]$fields[$k]))`n" }
$vdf += "}`n"
Set-Content -Path $vdfPath -Value $vdf -Encoding utf8NoBOM

Write-Host "==> $(if ($isNew) { 'Creating new' } else { "Updating $($config.publishedfileid)" }) Workshop item" -ForegroundColor Cyan
Write-Host "    VDF: $vdfPath"

if ($DryRun) {
    Write-Host $vdf
    Write-Host 'Dry run: nothing uploaded.' -ForegroundColor Yellow
    return
}

# --- Upload ---------------------------------------------------------------
# Interactive: steamcmd asks for the password and Steam Guard code on the
# first login, then reuses its cached session.
& $SteamCmd +login $SteamUser +workshop_build_item $vdfPath +quit
if ($LASTEXITCODE) { throw "steamcmd exited with $LASTEXITCODE" }

# steamcmd writes the assigned id back into the VDF.
$m = Select-String -Path $vdfPath -Pattern '"publishedfileid"\s+"(\d+)"'
$id = if ($m) { $m.Matches[0].Groups[1].Value } else { '0' }
if ($id -eq '0') { throw 'Upload did not report a publishedfileid; check the steamcmd output above.' }

if ($isNew) {
    $config.publishedfileid = [long]$id
    $config | ConvertTo-Json | Set-Content $configPath -Encoding utf8NoBOM
    Write-Host "Saved publishedfileid $id to workshop.json - commit it." -ForegroundColor Green
}
Write-Host "Done: https://steamcommunity.com/sharedfiles/filedetails/?id=$id" -ForegroundColor Green
