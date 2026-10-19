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
* **Version:** 9.0.2 (Windows x64 Essentials Build)
* **Archive SHA-256:** `60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba`
* **Publisher:** FFmpeg Project / Gyan.dev
* **License:** GNU General Public License (GPL) v3.0+ (GPL-3.0-or-later enabled via `--enable-gpl --enable-version3`)
* **URL:** [https://ffmpeg.org](https://ffmpeg.org) and [https://www.gyan.dev/ffmpeg/builds/](https://www.gyan.dev/ffmpeg/builds/)
* **Redistribution Location:** `engines/media/ffmpeg.exe`, `engines/media/ffprobe.exe`, `engines/media/ffmpeg-metadata.json`
* **Justification:** Industry-standard audio/video decoding, transcoding, demuxing, filtering, and stream analysis. Bundling deterministic builds ensures end users never need to install codecs or package managers manually.
* **Compliance:** The bundled distribution is compiled under GPL-3.0-or-later (enabling x264/x265, libvpx, libopus, libmp3lame, libvorbis, libwebp), which is copyleft-compatible with Phase Drop's GPL-3.0 license. Upstream source code repository links, build flags, and notices are provided in `THIRD-PARTY-NOTICES.md`.

---

## 3. Managed NuGet Dependencies

### PdfSharp
* **Version:** 6.1.1
* **Publisher:** empira Software GmbH
* **License:** MIT License
* **URL:** [https://github.com/empira/PDFsharp](https://github.com/empira/PDFsharp)
* **Justification:** Fully managed .NET library for creating and modifying PDF documents, extracting pages, merging multiple documents, and handling page rotations without external dependencies.

### SharpCompress
* **Version:** 0.50.6
* **Publisher:** Adam Hathcock
* **License:** MIT License
* **URL:** [https://github.com/adamhathcock/sharpcompress](https://github.com/adamhathcock/sharpcompress)
* **Justification:** Pure C# compression library supporting non-standard formats (7z, Tar, GZip, BZip2) with streaming API, avoiding native DLL dependencies while enforcing strict directory traversal protection.

### Fluent Backdrops & Interop Architecture
* **Implementation:** Native Win32 `DwmSetWindowAttribute` Interop via `dwmapi.dll`
* **Justification:** Phase Drop invokes native Windows 11 Desktop Window Manager (DWM) APIs directly to apply Mica (`DWMWA_SYSTEMBACKDROP_TYPE = 2`) and Acrylic (`DWMWA_SYSTEMBACKDROP_TYPE = 3`) backdrops, avoiding heavy external WPF UI framework wrappers.

---

## 4. Test Framework Dependencies

### Xunit.SkippableFact
* **Version:** 1.5.85
* **Publisher:** Andrew Arnott
* **License:** MIT License
* **URL:** [https://github.com/AArnott/Xunit.SkippableFact](https://github.com/AArnott/Xunit.SkippableFact)
* **Justification:** Provides genuine dynamic test skipping (`[SkippableFact]` / `Skip.If`) for external tool integration tests on development machines while guaranteeing strict failure enforcement in CI.

### xUnit & Test SDK
* **xUnit:** 2.9.3 (Apache-2.0)
* **Microsoft.NET.Test.Sdk:** 17.13.0 (Microsoft Pre-Release/Product License)
* **coverlet.collector:** 6.0.4 (MIT)
* **xunit.runner.visualstudio:** 3.0.2 (MIT)

---

## 5. Packaging & Installer Tools

### NSIS (Nullsoft Scriptable Install System)
* **Version:** 3.13
* **Publisher:** Nullsoft and Contributors
* **License:** zlib/libpng license, bzip2 license, CPLv1
* **URL:** [https://nsis.sourceforge.io/](https://nsis.sourceforge.io/)
* **Justification:** Battle-tested, lightweight, scriptable native Windows installer builder that requires no runtime installer pre-requisites on the user's computer.
