param(
    [string]$UnityExe = '',
    [string]$OutputDirectory = '',
    [switch]$Development
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $UnityExe) { $UnityExe = $env:UNITY_EXE }
if (-not $UnityExe) {
    $versionLine = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion:' } | Select-Object -First 1
    $version = ($versionLine -replace '^m_EditorVersion:\s*', '').Trim()
    $UnityExe = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityExe)) { throw 'Matching Unity Editor not found. Pass -UnityExe or set UNITY_EXE.' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'Builds\Windows\Pokagoryu' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$logDirectory = Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$logFile = Join-Path $logDirectory 'build-windows.log'
$arguments = '-batchmode -projectPath "' + $projectRoot + '" -executeMethod PokaGoryu.EditorTools.PokaGoryuBuild.BuildWindowsCommandLine -pgOut "' + $OutputDirectory + '" -logFile "' + $logFile + '"'
if ($Development) { $arguments += ' -pgDev' }
$process = Start-Process -FilePath $UnityExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
while (-not $process.WaitForExit(1000)) { }
if ($process.ExitCode -ne 0) { throw "Unity build failed. See $logFile" }
Write-Output (Join-Path $OutputDirectory 'Pokagoryu.exe')
