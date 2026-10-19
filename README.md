<div align="center">

<img src="assets/branding/logo.png" alt="Phase Drop Logo" width="160" height="160" />

# Phase Drop
### *Drop. Transform. Done.*

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)
[![Platform: Windows 11](https://img.shields.io/badge/Platform-Windows%2011%20x64-0078D4.svg)](https://microsoft.com/windows)
[![.NET 10 LTS](https://img.shields.io/badge/.NET-10.0%20LTS-512BD4.svg)](https://dotnet.microsoft.com/)
[![Local Only](https://img.shields.io/badge/Privacy-100%25%20Local%20%7C%20No%20Cloud-success.svg)](#privacy--security-guarantee)

**An ultra-fast, private, open-source Windows 11 utility for converting, editing, compressing, and managing files through a circular radial menu activated seamlessly during drag-and-drop operations.**

[Features](#key-features) • [Quick Start](#quick-start) • [Supported Formats](#supported-formats--tools) • [Architecture](#architecture) • [Building from Source](#building-from-source) • [Security & Privacy](#privacy--security-guarantee)

</div>

---

## 🚀 Key Features

* **Instant Radial Menu (Shift-Drag):** Select files in Windows File Explorer or on the Desktop, drag with the left mouse button, and hold `Shift`. A beautiful Windows 11 Fluent radial menu blooms around your cursor. Drop onto your target format or tool—done!
* **100% Local Processing:** Every conversion, transcode, and transformation happens right on your machine.
* **Zero External Dependencies Required:** You do **not** need to install Python, .NET runtime, FFmpeg, or Microsoft Office. The installer bundles all necessary processing engines.
* **Sub-0.1% Idle CPU Usage:** Event-driven Win32 input monitoring guarantees zero background CPU churn when idle.
* **Non-Destructive & Collision-Safe:** Your original files are never overwritten. Converted files are safely named (`photo (1).webp`) in the chosen folder.
* **Windows 11 Fluent Aesthetics:** Supports System, Dark, and Light themes with subtle acrylic/Mica backdrops, fluid animations, and crisp vector typography.
* **Zero Telemetry or Ads:** No accounts, no internet access, no tracking, completely private and open-source.

---

## 🎯 Quick Start

### 1. Download & Install
Download `PhaseDrop-Setup-v1.0.0-x64.exe` from GitHub Releases. Run the installer, select your preferred theme (System / Dark / Light), and choose whether to launch Phase Drop at Windows startup.

### 2. Basic Conversion Workflow
1. Select one or more files in **File Explorer** or on the **Desktop**.
2. Begin dragging with the **Left Mouse Button**.
3. While dragging, hold the **Shift** key.
4. The Phase Drop radial menu appears centered under your cursor.
5. Move your pointer over the desired petal (e.g. `PNG`, `WebP`, `MP3`, `Compress`).
6. **Release the mouse button**. Phase Drop converts the file immediately and provides a non-intrusive notification upon completion.

### 3. Contextual Tools Workflow
Hold **Ctrl + Shift** while dragging to switch the wheel to **Contextual Tools Mode**:
* **Images:** Crop with interactive handles, Resize, Compress, Strip EXIF, Convert to PDF.
* **Videos:** Trim clip, Compress, Extract Audio, Convert to GIF, Change Speed.
* **Audio:** Trim, Change Bitrate, Volume Normalize.
* **PDFs:** Merge multiple PDFs, Split into individual pages, Extract page ranges, Rotate pages, Strip metadata.
* **Archives:** Create `.zip`, `.7z`, or `.tar.gz` with selectable compression levels.

---

## 📦 Supported Formats & Tools

| Family | Supported Input | Supported Output | Specialized Tools |
| :--- | :--- | :--- | :--- |
| **Images** | PNG, JPG, JPEG, WebP, BMP, GIF, TIFF, ICO, SVG | PNG, JPG, WebP, BMP, GIF, TIFF, ICO, PDF | Interactive Crop, Resize, Rotate, Flip, Compress, Strip Metadata |
| **Video** | MP4, MOV, MKV, WebM, AVI, WMV, FLV | MP4, WebM, MKV, MOV, AVI, GIF, MP3, WAV | Interactive Trim, Compress, Audio Strip, GIF Maker, Thumbnail |
| **Audio** | MP3, WAV, FLAC, AAC, M4A, OGG, Opus, WMA | MP3, WAV, FLAC, AAC, M4A, OGG, Opus | Trim, Bitrate Adjust, Volume Normalization |
| **PDF** | PDF | PDF, ZIP (archive), 7Z (archive) | Merge, Split, Page Extraction, Page Rotation, Strip Info |
| **Archives** | ZIP, 7Z, TAR, GZ, TGZ, RAR (Extract only) | ZIP, 7Z, TAR, GZ, TGZ | Compress Folder/Files, Safe Extract, Format Transcode, Integrity Test |
| **Documents**| CSV, TSV, TXT, JSON, HTML, MD | CSV, TSV, TXT, JSON, HTML, MD, PDF | Table Conversion, Markdown to HTML, Text to PDF |

---

## 🔒 Privacy & Security Guarantee

* **Offline-First:** Phase Drop does not connect to the internet.
* **Zip-Slip Protected:** Safe canonical path verification prevents directory traversal attacks during archive extraction.
* **Safe Subprocesses:** Commands to FFmpeg/FFprobe use typed parameter lists rather than concatenated shell strings, eliminating command-injection risks.
* **Atomic Output:** Incomplete or aborted jobs clean up temporary files instantly.

---

## 🛠 Building from Source

### Prerequisites
* Windows 11 x64
* [.NET 10 LTS SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* [NSIS 3.13+](https://nsis.sourceforge.io/) (optional, for installer packaging)

### Build Commands
```powershell
# Clone the repository
git clone https://github.com/VaibhavSoni24/Phase-Drop.git
cd Phase-Drop

# Compile the solution (Release)
.\scripts\build.ps1

# Run the test suite
.\scripts\test.ps1

# Package the release installer
.\scripts\package.ps1

# Measure idle CPU & memory performance
.\scripts\measure-idle.ps1
```

---

## 📄 License & Notices

Phase Drop is free and open-source software licensed under the **GNU General Public License v3.0 or later** ([GPL-3.0-or-later](LICENSE)).
Third-party component notices and licenses are documented in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
