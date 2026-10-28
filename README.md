# MediaPort

MediaPort is a portable Windows GUI for yt-dlp. The packaged application contains the official yt-dlp Windows binary and FFmpeg, so end users do not need Python, PowerShell, or a separate installation.

## Included features

- Video quality presets through 4K
- MP3, M4A, Opus, FLAC, and WAV audio extraction
- Playlist pre-scan with total video count, title list, current item, and overall progress
- Pre-download URL validation that removes invalid or unsupported links
- Embedded subtitles, thumbnails, metadata, and chapters
- SponsorBlock segment removal
- Cookies from Chrome, Edge, Firefox, Brave, Opera, or Vivaldi
- Live percentage, speed, ETA, activity log, and full yt-dlp/FFmpeg process-tree cancellation
- Safe Windows filenames and user-selectable download folders

## Build

Run `build.ps1` on Windows. It uses the built-in .NET Framework C# compiler, embeds `yt-dlp.exe` plus the FFmpeg executable found on `PATH`, and writes `dist\MediaPort-Portable.exe`.

Only download media that you own or are authorized to save. Site terms and applicable copyright laws still apply.
