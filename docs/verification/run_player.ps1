<#
.SYNOPSIS
  Smoke-tests a BUILT Windows player (not the editor): starts it in a small window, takes screenshots of ONLY the game
  window, optionally plays the first minute with key presses, kills it, and scans the player log for problems.

.DESCRIPTION
  Default mode: waits for the world to generate (title screen) and takes -ShotCount screenshots.
  -Play mode: title -> Enter (PLAY menu item) -> intro -> Space (skip) -> gameplay -> Tab held (left forearm with the health
  feathers) -> W held (walking), one screenshot after each step.

  Safety / how it works
  * Screenshots use PrintWindow on the game window handle (not a screen grab), so no other window ends up in the PNG.
    If PrintWindow returns a black image, the script falls back to a screen grab ONLY while the game window is in front.
  * Keys are sent with PostMessage(WM_KEYDOWN/WM_KEYUP) to the game window handle ONLY. They never go to other windows
    (so they cannot hit whatever you are typing in), and Unity's legacy Input Manager does react to them.
  * Mouse input is not simulated. While the game is in the gameplay state it locks the mouse cursor until the script closes it.
  * The window is visible for about 20-45 seconds and the city drone may be audible.

  Exit code: 0 when the player stayed alive and the log has no suspicious lines, otherwise 1.

.PARAMETER Exe          Path to the built Pokagoryu.exe.
.PARAMETER OutDir       Where the log and PNGs go (default: %TEMP%\pg_player).
.PARAMETER Name         Label of this run (file names).
.PARAMETER Play         Play the first minute with key presses (see above).
.PARAMETER WarmupSec    Seconds to wait after the window appears (world generation takes a few seconds). Default 9.
.PARAMETER ShotCount    Screenshots in the default mode. Default 2.
.PARAMETER ShotGapSec   Seconds between screenshots in the default mode. Default 4.
.PARAMETER Width        Window width (default 1280).
.PARAMETER Height       Window height (default 720).

.EXAMPLE
  powershell -File docs\verification\run_player.ps1 -Exe Builds\Windows\Pokagoryu\Pokagoryu.exe -Name build1
  powershell -File docs\verification\run_player.ps1 -Exe Builds\Windows\Pokagoryu\Pokagoryu.exe -Name build1 -Play
#>
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$OutDir = '',
    [string]$Name = 'player',
    [switch]$Play,
    [int]$WarmupSec = 9,
    [int]$ShotCount = 2,
    [int]$ShotGapSec = 4,
    [int]$Width = 1280,
    [int]$Height = 720
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PgW32 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint mapType);
}
'@
[PgW32]::SetProcessDPIAware() | Out-Null

if (-not $OutDir) { $OutDir = Join-Path ([IO.Path]::GetTempPath()) 'pg_player' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$Exe = (Resolve-Path -LiteralPath $Exe).Path
$log = Join-Path $OutDir "player_$Name.log"
if (Test-Path -LiteralPath $log) { Remove-Item -LiteralPath $log -Force }

function Get-MeanBrightness([System.Drawing.Bitmap]$bmp) {
    $sum = 0.0; $n = 0
    $stepY = [Math]::Max(1, [int]($bmp.Height / 40)); $stepX = [Math]::Max(1, [int]($bmp.Width / 60))
    for ($y = 0; $y -lt $bmp.Height; $y += $stepY) {
        for ($x = 0; $x -lt $bmp.Width; $x += $stepX) { $c = $bmp.GetPixel($x, $y); $sum += ($c.R + $c.G + $c.B); $n++ }
    }
    return [Math]::Round($sum / (3.0 * [Math]::Max(1, $n)), 1)
}

function Save-WindowShot([IntPtr]$h, [string]$path) {
    $r = New-Object PgW32+RECT
    [PgW32]::GetWindowRect($h, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
    if ($w -lt 100 -or $ht -lt 100) { return "bad window rect ${w}x${ht}" }
    $bmp = New-Object System.Drawing.Bitmap $w, $ht
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [PgW32]::PrintWindow($h, $hdc, 2) | Out-Null   # 2 = PW_RENDERFULLCONTENT
    $g.ReleaseHdc($hdc); $g.Dispose()
    $mean = Get-MeanBrightness $bmp
    $how = 'PrintWindow'
    if ($mean -lt 1.0 -and [PgW32]::GetForegroundWindow() -eq $h) {
        $g2 = [System.Drawing.Graphics]::FromImage($bmp)
        $g2.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size $w, $ht))
        $g2.Dispose()
        $mean = Get-MeanBrightness $bmp
        $how = 'screen grab (game window in front)'
    }
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return "$how, ${w}x${ht}, mean brightness $mean"
}

function Shot([IntPtr]$h, [string]$tag) {
    $png = Join-Path $OutDir ("player_{0}_{1}.png" -f $Name, $tag)
    'shot {0}: {1}' -f $tag, (Save-WindowShot $h $png)
}

# keys go to the game window handle only (PostMessage), never to the foreground window
function Key-Down([IntPtr]$h, [int]$vk) {
    $sc = [PgW32]::MapVirtualKey([uint32]$vk, 0)
    [PgW32]::PostMessage($h, 0x100, [IntPtr]$vk, [IntPtr]([int64](1 + ($sc * 65536)))) | Out-Null
}
function Key-Up([IntPtr]$h, [int]$vk) {
    $sc = [PgW32]::MapVirtualKey([uint32]$vk, 0)
    [PgW32]::PostMessage($h, 0x101, [IntPtr]$vk, [IntPtr]([int64](1 + ($sc * 65536)) + 1073741824 + 2147483648)) | Out-Null
}
function Key-Tap([IntPtr]$h, [int]$vk) { Key-Down $h $vk; Start-Sleep -Milliseconds 120; Key-Up $h $vk }

$argLine = "-screen-fullscreen 0 -screen-width $Width -screen-height $Height -logFile `"$log`""
$proc = Start-Process -FilePath $Exe -ArgumentList $argLine -PassThru -WorkingDirectory (Split-Path -Parent $Exe)
$started = Get-Date
'player started, pid ' + $proc.Id

$h = [IntPtr]::Zero
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Milliseconds 500
    if ($proc.HasExited) { break }
    $proc.Refresh()
    if ($proc.MainWindowHandle -ne [IntPtr]::Zero) { $h = $proc.MainWindowHandle; break }
}

$problem = $false
if ($proc.HasExited) { 'PLAYER EXITED EARLY, exit code ' + $proc.ExitCode; $problem = $true }
elseif ($h -eq [IntPtr]::Zero) { 'no game window appeared within 30 s'; $problem = $true }
else {
    'game window appeared after {0:N1} s' -f ((Get-Date) - $started).TotalSeconds
    Start-Sleep -Seconds $WarmupSec
    if ($Play) {
        Shot $h '1_title'
        Key-Tap $h 0x0D;  'pressed Enter (the PLAY menu item)'
        Start-Sleep -Seconds 4
        Shot $h '2_intro'
        Key-Tap $h 0x20;  'pressed Space (skip the intro)'
        Start-Sleep -Seconds 1
        Key-Tap $h 0x20
        Start-Sleep -Seconds 4
        Shot $h '3_gameplay'
        Key-Down $h 0x09; 'holding Tab (look at yourself)'
        Start-Sleep -Milliseconds 1800
        Shot $h '4_tab_held'
        Key-Up $h 0x09
        Start-Sleep -Milliseconds 800
        Key-Down $h 0x57; 'holding W (walk forward)'
        Start-Sleep -Seconds 3
        Shot $h '5_walking'
        Key-Up $h 0x57
        Start-Sleep -Milliseconds 500
    } else {
        for ($s = 1; $s -le $ShotCount; $s++) {
            if ($proc.HasExited) { 'PLAYER EXITED before shot ' + $s + ', exit code ' + $proc.ExitCode; $problem = $true; break }
            Shot $h ([string]$s)
            if ($s -lt $ShotCount) { Start-Sleep -Seconds $ShotGapSec }
        }
    }
}

$alive = -not $proc.HasExited
'process still running after the run: ' + $alive
if (-not $alive) { $problem = $true }
if ($alive) {
    $proc.CloseMainWindow() | Out-Null
    if (-not $proc.WaitForExit(4000)) { try { $proc.Kill() } catch { } }
}

'--- player log ---'
if (Test-Path -LiteralPath $log) {
    $lines = Get-Content -LiteralPath $log -Encoding UTF8
    'log lines: ' + $lines.Count
    $lines | Where-Object { $_ -match 'Direct3D|D3D1[12]|Vulkan|Renderer:|Graphics API|Initialize engine version' } | Select-Object -First 6
    $bad = @($lines | Where-Object { $_ -match '(?i)exception|error|failed|shader.*(not|missing|unsupported)|assertion|NullReference' })
    'suspicious lines: ' + $bad.Count
    $bad | Select-Object -First 15
    if ($bad.Count -gt 0) { $problem = $true }
} else {
    'no player log (' + $log + ')'
    $problem = $true
}
'log file: ' + $log
if ($problem) { exit 1 } else { exit 0 }
