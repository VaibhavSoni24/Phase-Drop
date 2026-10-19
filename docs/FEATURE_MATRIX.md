# Phase Drop Feature Matrix & Format Registry

## 1. Overview
The Phase Drop capability registry maps detected input file extensions to functional conversion paths, compression routines, and specialized editing tools. Every supported operation listed below is documented with its verified lifecycle status.

### Status Definitions
* **Implemented:** Full engine code exists in the application codebase.
* **Unit tested:** Automated unit test fixture validates algorithm logic, boundaries, and errors.
* **Integration tested:** End-to-end job queue execution verified with temporary file staging and output verification.
* **Manually verified on Windows:** Tested interactively on Windows 11 with real explorer interactions.
* **Unsupported or deferred:** Explicitly removed or deferred until a verified local engine is bundled; not exposed in the UI.

---

## 2. Format Families & Operations

### 2.1 Raster & Vector Images
* **Supported Input Formats:** `.png`, `.jpg`, `.jpeg`, `.webp`, `.bmp`, `.gif`, `.tiff`, `.tif`, `.ico`, `.svg`
* **Supported Output Formats:** `.png`, `.jpg`, `.webp`, `.bmp`, `.gif`, `.tiff`, `.ico`, `.pdf`

| Operation | Engine | Lossy/Lossless | Batch Capable | Interactive Tool UI | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Convert Format (PNG, JPG, BMP, GIF, TIFF, ICO)** | WIC / Managed | Configurable | Yes | No (Direct Drop) | Unit tested, Integration tested |
| **Convert to WebP** | Bundled FFmpeg | Lossy/Lossless | Yes | No (Direct Drop) | Unit tested, Integration tested |
| **Resize (Scale/Pixels)** | WIC / Managed | Lossy/Lossless | Yes | Yes (Tool Dialog) | Unit tested, Manually verified on Windows |
| **Crop** | WIC / Managed | Lossy/Lossless | Single | Yes (Crop Frame UI) | Unit tested, Manually verified on Windows |
| **Rotate (90/180/270)** | WIC / Managed | Lossless | Yes | No (Direct / Preset) | Unit tested, Integration tested |
| **Flip (H/V)** | WIC / Managed | Lossless | Yes | No (Direct / Preset) | Implemented, Unit tested |
| **Compress / Optimize** | WIC / Managed / FFmpeg | Lossy | Yes | Yes (Quality Slider) | Implemented, Unit tested |
| **Strip EXIF Metadata** | WIC / Managed | Lossless | Yes | No (Direct Drop) | Implemented, Unit tested |
| **Image(s) to PDF** | Managed (PdfSharp) | Lossless | Yes (Multi-file) | No (Direct Drop) | Unit tested, Integration tested |

---

### 2.2 Video
* **Supported Input Formats:** `.mp4`, `.mov`, `.mkv`, `.webm`, `.avi`, `.wmv`, `.flv`, `.m4v`
* **Supported Output Formats:** `.mp4`, `.webm`, `.mkv`, `.mov`, `.avi`, `.gif`, `.mp3` (audio), `.wav` (audio)

| Operation | Engine | Lossy/Lossless | Batch Capable | Interactive Tool UI | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Convert Video (MP4, WebM, MKV, MOV, AVI)** | Bundled FFmpeg | Lossy | Yes | No (Direct Drop) | Unit tested, Integration tested |
| **Trim Video** | Bundled FFmpeg | Stream copy/re-encode | Single | Yes (Trim Slider UI) | Implemented, Manually verified on Windows |
| **Compress Video** | Bundled FFmpeg | Lossy | Yes | Yes (Bitrate/Preset) | Implemented, Unit tested |
| **Extract Audio (MP3, WAV)** | Bundled FFmpeg | Lossy/Lossless | Yes | No (Direct Drop) | Unit tested, Integration tested |
| **Convert to Animated GIF** | Bundled FFmpeg | Lossy | Yes | Yes (FPS/Scale) | Implemented, Unit tested |
| **Extract Frame/Thumbnail** | Bundled FFmpeg | Lossless | Yes | Yes (Timestamp Picker)| Implemented, Unit tested |
| **Speed Adjustment (0.5x-2x)**| Bundled FFmpeg | Re-encode | Yes | Yes (Speed Picker) | Implemented, Unit tested |

---

### 2.3 Audio
* **Supported Input Formats:** `.mp3`, `.wav`, `.flac`, `.aac`, `.m4a`, `.ogg`, `.opus`, `.wma`
* **Supported Output Formats:** `.mp3`, `.wav`, `.flac`, `.aac`, `.m4a`, `.ogg`, `.opus`

| Operation | Engine | Lossy/Lossless | Batch Capable | Interactive Tool UI | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Convert Audio (MP3, WAV, FLAC, AAC, OGG, Opus)** | Bundled FFmpeg | Lossy/Lossless | Yes | No (Direct Drop) | Unit tested, Integration tested |
| **Trim Audio** | Bundled FFmpeg | Stream copy/re-encode | Single | Yes (Trim Slider UI) | Implemented, Manually verified on Windows |
| **Compress / Change Bitrate** | Bundled FFmpeg | Lossy | Yes | Yes (Quality Slider) | Implemented, Unit tested |
| **Normalize Volume (EBU R128)** | Bundled FFmpeg (`loudnorm`) | Re-encode | Yes | No (Direct Drop) | Implemented, Unit tested |

---

### 2.4 PDF Documents
* **Supported Input Formats:** `.pdf`
* **Supported Output Formats:** `.pdf`, `.zip` (archived), `.7z` (archived)

| Operation | Engine | Lossy/Lossless | Batch Capable | Interactive Tool UI | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Merge Multiple PDFs** | Managed (PdfSharp) | Lossless | Yes (Multi-file) | No (Direct Drop) | Unit tested, Integration tested |
| **Split PDF into Pages** | Managed (PdfSharp) | Lossless | Yes | No (Direct Drop) | Unit tested, Integration tested |
| **Extract Specific Pages** | Managed (PdfSharp) | Lossless | Single | Yes (Page Range Tool) | Unit tested, Integration tested |
| **Rotate PDF Pages** | Managed (PdfSharp) | Lossless | Yes | Yes (Rotation Tool) | Unit tested, Integration tested |
| **Strip PDF Metadata** | Managed (PdfSharp) | Lossless | Yes | No (Direct Drop) | Implemented, Unit tested |
| **PDF Pages to Raster Images** | None (Requires PDFium/Ghostscript) | N/A | No | No | **Unsupported or deferred** |

---

### 2.5 Archives
* **Supported Input Formats:** `.zip`, `.tar`, `.gz`, `.tgz`, `.7z`, `.rar` (Extraction only)
* **Supported Output Formats:** `.zip`, `.tar`, `.gz`, `.tgz`, `.7z`

| Operation | Engine | Lossy/Lossless | Batch Capable | Interactive Tool UI | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Create Archive (ZIP, 7z, TAR, TGZ)** | System.IO.Compression / SharpCompress | Lossless | Yes (Multi-file) | No (Direct Drop) | Unit tested, Integration tested |
| **Extract Archive (Safe Directory Traversal)** | System.IO.Compression / SharpCompress | Lossless | Yes | No (Direct Drop) | Unit tested, Integration tested |
| **Convert Archive Format** | Managed Archive Pipeline | Lossless | Yes | No (Direct Drop) | Unit tested, Integration tested |
| **Test Archive Integrity**| System.IO.Compression / SharpCompress | N/A | Yes | No (Direct Drop) | Unit tested, Integration tested |

---

### 2.6 Documents & Data
* **Supported Input Formats:** `.csv`, `.tsv`, `.txt`, `.json`, `.html`, `.md`
* **Supported Output Formats:** `.csv`, `.tsv`, `.txt`, `.json`, `.html`, `.md`, `.pdf`

| Operation | Engine | Lossy/Lossless | Batch Capable | Interactive Tool UI | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **RFC 4180 CSV <-> JSON** | Managed State-Machine Parser | Lossless | Yes | No (Direct Drop) | Unit tested, Integration tested |
| **RFC 4180 CSV <-> HTML Table** | Managed Text Engine (Entity Escaped) | Lossless | Yes | No (Direct Drop) | Unit tested, Integration tested |
| **Markdown -> HTML (Escaped)** | Managed Text Engine | Lossless | Yes | No (Direct Drop) | Implemented, Unit tested |
| **Plain Text -> PDF** | Managed Document Engine | Lossless | Yes | No (Direct Drop) | Implemented, Unit tested |

---

## 3. Known Limitations & Honest Architectural Demarcation
1. **PDF Page Rasterization (Deferred):** PDFsharp 6.1 is a document generation and page-manipulation library, not a rasterizer. Converting PDF pages to raster bitmaps (`PNG`/`JPEG`) requires a full rasterizer engine (e.g. PDFium). Rather than pretending metadata operations or unverified FFmpeg filters accomplish this, PDF-to-image is excluded from the active registry until a fully licensed rasterizer is bundled.
2. **Office Formats (`.docx`, `.pptx`, `.xlsx`):** Exact layout fidelity without Microsoft Office installed requires a large headless office suite. When Office is absent, Phase Drop extracts text and table structures safely rather than claiming layout-identical rendering.
3. **Proprietary Archives (RAR):** Creating `.rar` archives requires proprietary licensing; Phase Drop extracts `.rar` via SharpCompress but writes exclusively to open formats (`.zip`, `.7z`, `.tar.gz`).
4. **Encrypted / DRM Content:** Phase Drop does not bypass Widevine or password-protected archives without valid user credentials.
