param(
    [string]$GameDir
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not $GameDir) {
    $GameDir = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "..\.."))
}
$GameDir = $GameDir.TrimEnd('\', '/')

if (-not (Test-Path (Join-Path $GameDir "valheim_Data\Managed\assembly_valheim.dll"))) {
    throw "'$GameDir' is not a Valheim install (valheim_Data\Managed\assembly_valheim.dll not found). Pass -GameDir <path to Valheim>."
}
$plugins = Join-Path $GameDir "BepInEx\plugins"
if (-not (Test-Path (Join-Path $plugins "Jotunn.dll"))) {
    throw "Jotunn.dll not found in '$plugins'. Install BepInEx + Jotunn into the game first."
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The 'dotnet' SDK was not found on PATH. Install it from https://dotnet.microsoft.com/download"
}

$project = Join-Path $repoRoot "ValheimSkillCapMod\ValheimSkillCapMod.Local.csproj"
dotnet build $project -c Debug -p:GameDir=$($GameDir -replace '\\', '/')
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

Copy-Item (Join-Path $repoRoot "ValheimSkillCapMod\bin\Debug\net48\ValheimSkillCapMod.dll") $plugins -Force
Write-Host "Deployed to $plugins\ValheimSkillCapMod.dll"
