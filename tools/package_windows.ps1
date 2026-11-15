param(
    [string]$PlayerDirectory = '',
    [string]$OutputDirectory = '',
    [string]$RarExe = ''
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $PlayerDirectory) { $PlayerDirectory = Join-Path $projectRoot 'Builds\Windows\Pokagoryu' }
$PlayerDirectory = (Resolve-Path -LiteralPath $PlayerDirectory).Path
foreach ($required in @('Pokagoryu.exe', 'UnityPlayer.dll', 'Pokagoryu_Data', 'MonoBleedingEdge')) {
    if (-not (Test-Path -LiteralPath (Join-Path $PlayerDirectory $required))) { throw "Incomplete Unity player: missing $required" }
}
if (-not $RarExe) { $RarExe = Join-Path $env:ProgramFiles 'WinRAR\Rar.exe' }
if (-not (Test-Path -LiteralPath $RarExe)) { throw 'WinRAR is required to package a self-extracting EXE. Pass -RarExe.' }
$module = Join-Path (Split-Path -Parent $RarExe) 'Default.SFX'
if (-not (Test-Path -LiteralPath $module)) { throw 'WinRAR Default.SFX module not found' }
$config = [IO.File]::ReadAllText((Join-Path $projectRoot 'Assets\Scripts\Core\GameConfig.cs'))
$match = [regex]::Match($config, 'Version\s*=\s*"([^"]+)"')
if (-not $match.Success) { throw 'Could not read game version' }
$version = $match.Groups[1].Value
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'Releases' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$output = Join-Path $OutputDirectory ("Pokagoryu-$version-Windows-x64.exe")
if (Test-Path -LiteralPath $output) { throw "Output already exists: $output" }
$leaf = Split-Path -Leaf $PlayerDirectory
$commentFile = Join-Path ([IO.Path]::GetTempPath()) ('pokagoryu-sfx-' + [Guid]::NewGuid().ToString('N') + '.txt')
$comment = "; SFX configuration`r`nTitle=Pokagoryu $version`r`nText=Choose a folder to extract the complete Windows game. The game will start after extraction.`r`nSetup=`"$leaf\Pokagoryu.exe`"`r`n"
[IO.File]::WriteAllText($commentFile, $comment, [Text.Encoding]::ASCII)
try {
    Push-Location -LiteralPath (Split-Path -Parent $PlayerDirectory)
    try {
        & $RarExe a -cfg- -r -m5 -idq ('-sfx' + $module) ('-z' + $commentFile) $output $leaf
        if ($LASTEXITCODE -ne 0) { throw 'EXE packaging failed' }
    } finally { Pop-Location }
    & $RarExe t -idq $output
    if ($LASTEXITCODE -ne 0) { throw 'EXE integrity test failed' }
} finally { Remove-Item -LiteralPath $commentFile -Force -ErrorAction SilentlyContinue }
Write-Output $output
