$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$ffmpegCommand = Get-Command ffmpeg.exe -ErrorAction Stop
$ffmpegPath = $ffmpegCommand.Source
$ytDlpPath = Join-Path $projectRoot 'yt-dlp.exe'
$outputFolder = Join-Path $projectRoot 'dist'
$outputPath = Join-Path $outputFolder 'MediaPort-Portable.exe'
$manifestPath = Join-Path $projectRoot 'app.manifest'
$sourcePath = Join-Path $projectRoot 'Program.cs'

if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows C# compiler was not found.' }
if (-not (Test-Path -LiteralPath $ytDlpPath)) { throw 'yt-dlp.exe is missing. Download the official Windows release first.' }
if (-not (Test-Path -LiteralPath $ffmpegPath)) { throw 'ffmpeg.exe is missing.' }

New-Item -ItemType Directory -Path $outputFolder -Force | Out-Null

& $compiler /nologo /target:winexe /optimize+ /platform:x64 `
    "/out:$outputPath" `
    "/win32manifest:$manifestPath" `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    "/resource:$ytDlpPath,MediaPort.yt-dlp.exe" `
    "/resource:$ffmpegPath,MediaPort.ffmpeg.exe" `
    $sourcePath

if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE." }
Get-Item -LiteralPath $outputPath | Select-Object FullName, Length, LastWriteTime
