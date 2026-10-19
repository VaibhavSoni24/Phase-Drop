# Phase Drop Third-Party Dependencies & Licenses

All third-party libraries, packages, and executables redistributed with or referenced by Phase Drop are audited below. Phase Drop itself is distributed under the GNU General Public License v3.0 or later (GPL-3.0-or-later).

---

## 1. Runtime Framework & SDK
* **Framework:** .NET 10 LTS (`net10.0-windows10.0.26100.0` / `net10.0`)
* **Publisher:** Microsoft Corporation
* **License:** MIT License
* **URL:** [https://github.com/dotnet/runtime](https://github.com/dotnet/runtime)
* **Justification:** LTS framework providing high-performance managed execution, modern C# language features, built-in asynchronous channel primitives, and native Windows Desktop (WPF/Win32) runtime support.

---

## 2. Bundled Native Executables

### FFmpeg & FFprobe
* **Version:** 9.0.2+ / 8.0.1+ (Windows x64 Full Build)
* **Publisher:** FFmpeg Project / Gyan.dev
* **License:** GNU General Public License (GPL) v3.0+ (GPL-3.0-or-later enabled via `--enable-gpl --enable-version3`)
* **URL:** [https://ffmpeg.org](https://ffmpeg.org)
* **Redistribution Location:** `engines/media/ffmpeg.exe`, `engines/media/ffprobe.exe`
* **Justification:** Industry-standard audio/video decoding, transcoding, demuxing, filtering, and stream analysis. Bundling deterministic builds ensures end users never need to install codecs or package managers manually.
* **Compliance:** The bundled distribution is compiled under GPL-3.0-or-later (enabling x264/x265 codecs), which is copyleft-compatible with Phase Drop's GPL-3.0 license. Upstream source code repository links, build flags, and notices are provided in `THIRD-PARTY-NOTICES.md`.

---

## 3. Managed NuGet Dependencies

### PdfSharp
* **Version:** 6.1.1 (or latest stable)
* **Publisher:** empira Software GmbH
* **License:** MIT License
* **URL:** [https://github.com/empira/PDFsharp](https://github.com/empira/PDFsharp)
* **Justification:** Fully managed .NET library for creating and modifying PDF documents, extracting pages, merging multiple documents, and handling page rotations without external dependencies.

### SharpCompress
* **Version:** 0.38.0+
* **Publisher:** Adam Hathcock
* **License:** MIT License
* **URL:** [https://github.com/adamhathcock/sharpcompress](https://github.com/adamhathcock/sharpcompress)
* **Justification:** Pure C# compression library supporting non-standard formats (7z, Tar, GZip, BZip2) with streaming API, avoiding native DLL dependencies while enforcing strict directory traversal protection.

### Wpf.Ui (Optional Accent & Theme integration)
* **Version:** 3.0.0+ / Native System Backdrop Interop
* **Publisher:** Lepoco
* **License:** MIT License
* **URL:** [https://github.com/lepoco/wpfui](https://github.com/lepoco/wpfui)
* **Justification:** Fluent System backdrops (Mica/Acrylic) and Windows 11 design integration in WPF.

---

## 4. Packaging & Installer Tools

### NSIS (Nullsoft Scriptable Install System)
* **Version:** 3.13
* **Publisher:** Nullsoft and Contributors
* **License:** zlib/libpng license, bzip2 license, CPLv1
* **URL:** [https://nsis.sourceforge.io/](https://nsis.sourceforge.io/)
* **Justification:** Battle-tested, lightweight, scriptable native Windows installer builder that requires no runtime installer pre-requisites on the user's computer.
