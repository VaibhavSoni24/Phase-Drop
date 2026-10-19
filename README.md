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

* **Instant Radial Menu (Shift-Drag):** Select files in Windows File Explorer or on the Desktop, drag with the left mouse button, and hold `Shift`. A modern Windows 11 Fluent radial menu blooms around your cursor. Drop onto your target format or tool—done!
* **Local Processing:** All conversions, transcodes, and edits execute entirely locally on your PC.
* **Self-Contained Distribution:** You do not need to pre-install Python, the .NET runtime, or FFmpeg. The installer bundles self-contained .NET 10 binaries and audited media engines.
* **Negligible Idle Footprint:** Verified ~0.0% idle CPU usage in Windows 11 benchmarks (`scripts/measure-idle.ps1`) through event-driven Win32 mouse hooks without polling loops.
* **Non-Destructive & Collision-Safe:** Original files are never overwritten. Output filenames are collision-safe (`photo (1).webp`) and committed atomically.
* **Windows 11 Fluent Aesthetics:** Supports System, Dark, and Light themes with subtle acrylic/Mica backdrops, fluid animations, and crisp typography.
* **Privacy by Design:** No telemetry, no accounts, and no background network calls during runtime.

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
| **Images** | PNG, JPG, JPEG, WebP, BMP, GIF, TIFF, ICO | PNG, JPG, WebP, BMP, GIF, TIFF, ICO, PDF | Interactive Crop, Resize, Rotate, Flip, Compress, Strip Metadata |
| **Video** | MP4, MOV, MKV, WebM, AVI, WMV, FLV, M4V | MP4, WebM, MKV, MOV, AVI, GIF, MP3, WAV | Interactive Trim, Compress, Audio Strip, GIF Maker, Thumbnail |
| **Audio** | MP3, WAV, FLAC, AAC, M4A, OGG, Opus, WMA | MP3, WAV, FLAC, AAC, M4A, OGG, Opus | Trim, Bitrate Adjust, Volume Normalization |
| **PDF** | PDF | PDF, ZIP (archive), 7Z (archive) | Merge, Split, Page Extraction, Page Rotation, Strip Info |
| **Archives** | ZIP, 7Z, TAR, GZ, TGZ, RAR (Extract only) | ZIP, 7Z, TAR, GZ, TGZ | Compress Folder/Files, Safe Extract, Format Transcode, Integrity Test |
| **Documents**| CSV, TSV, TXT, JSON, HTML, MD | CSV, TSV, TXT, JSON, HTML, MD | Table Conversion (CSV/TSV <-> JSON/HTML), Markdown to HTML |

### Format & Processing Demarcations
* **WebP Generation:** WebP encoding/decoding is routed through the verified bundled FFmpeg engine with stream validation.
* **Windows ICO Generation:** Produces genuine 22-byte Windows Icon headers (`00 00 01 00`) containing embedded PNG icon directories, fully compliant with Windows shell specifications.
* **Image-to-PDF:** Multi-image to PDF generation routes through the managed PDF engine (PDFsharp).
* **SVG Vector Images:** Raw SVG rasterization is currently unsupported and excluded from input routing until a self-contained vector renderer is bundled.
* **PDF-to-Raster (Images):** PDF page rasterization to PNG/JPEG is unsupported in v1.0.0; extracting pages into ZIP/7Z is provided.
* **Office Documents:** Proprietary Microsoft Office formats (.docx, .xlsx, .pptx) are unsupported without an installed office suite.

---

## 🔒 Privacy & Security Guarantee

* **Offline Runtime Guarantee:** The installed application operates completely offline, initiates zero network connections, and collects no telemetry. (Network access is used only by developers during build time to fetch pinned NuGet packages and FFmpeg releases).
* **Strict Zip-Slip & Traversal Defense:** Canonical destination containment prevents directory traversal, rooted paths, and sibling prefix attacks (`dest` vs `dest_evil`).
* **Resource Exhaustion Defense:** Archive extraction enforces 20,000 maximum entries and a 4 GB uncompressed size ceiling to prevent zip bombs.
* **Safe Subprocesses:** Commands to FFmpeg/FFprobe use structured argument lists without raw shell string concatenation, eliminating command-injection risks.
* **Atomic Output:** Incomplete, failed, or cancelled operations remove temporary staging files immediately. Original files are never overwritten.

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
