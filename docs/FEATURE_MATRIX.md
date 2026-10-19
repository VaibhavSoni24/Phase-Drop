# Phase Drop Feature Matrix & Format Registry

## 1. Overview
The Phase Drop capability registry maps detected input file extensions to functional conversion paths, compression routines, and specialized editing tools. Every supported operation listed below is implemented and tested against real file fixtures.

---

## 2. Format Families & Operations

### 2.1 Raster & Vector Images
* **Supported Input Formats:** `.png`, `.jpg`, `.jpeg`, `.webp`, `.bmp`, `.gif`, `.tiff`, `.tif`, `.ico`, `.svg`
* **Supported Output Formats:** `.png`, `.jpg`, `.webp`, `.bmp`, `.gif`, `.tiff`, `.ico`, `.pdf`

| Operation | Engine | Lossy/Lossless | Batch Capable | Interactive Tool UI | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Convert Format** | WIC / Managed / FFmpeg | Configurable | Yes | No (Direct Drop) | Verified |
| **Resize (Scale/Pixels)** | WIC / Managed | Lossy/Lossless | Yes | Yes (Tool Dialog) | Verified |
| **Crop** | WIC / Managed | Lossy/Lossless | Single | Yes (Crop Frame UI) | Verified |
| **Rotate (90/180/270)** | WIC / Managed | Lossless | Yes | No (Direct / Preset) | Verified |
| **Flip (H/V)** | WIC / Managed | Lossless | Yes | No (Direct / Preset) | Verified |
| **Compress / Optimize** | WIC / Managed / FFmpeg | Lossy | Yes | Yes (Quality Slider) | Verified |
| **Strip EXIF Metadata** | WIC / Managed | Lossless | Yes | No (Direct Drop) | Verified |
| **Image to PDF** | Managed (PdfSharp) | Lossless | Yes (Multi-file) | No (Direct Drop) | Verified |

---

### 2.2 Video
* **Supported Input Formats:** `.mp4`, `.mov`, `.mkv`, `.webm`, `.avi`, `.wmv`, `.flv`, `.m4v`
* **Supported Output Formats:** `.mp4`, `.webm`, `.mkv`, `.mov`, `.avi`, `.gif`, `.mp3` (audio), `.wav` (audio)

| Operation | Engine | Lossy/Lossless | Batch Capable | Interactive Tool UI | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Convert Video** | Bundled FFmpeg | Lossy | Yes | No (Direct Drop) | Verified |
| **Trim Video** | Bundled FFmpeg | Stream copy/re-encode | Single | Yes (Trim Slider UI) | Verified |
| **Compress Video** | Bundled FFmpeg | Lossy | Yes | Yes (Bitrate/Preset) | Verified |
| **Extract Audio** | Bundled FFmpeg | Lossy/Lossless | Yes | No (Direct Drop) | Verified |
| **Convert to Animated GIF** | Bundled FFmpeg | Lossy | Yes | Yes (FPS/Scale) | Verified |
| **Extract Frame/Thumbnail** | Bundled FFmpeg | Lossless | Yes | Yes (Timestamp Picker)| Verified |
| **Speed Adjustment (0.5x-2x)**| Bundled FFmpeg | Re-encode | Yes | Yes (Speed Picker) | Verified |

---

### 2.3 Audio
* **Supported Input Formats:** `.mp3`, `.wav`, `.flac`, `.aac`, `.m4a`, `.ogg`, `.opus`, `.wma`
* **Supported Output Formats:** `.mp3`, `.wav`, `.flac`, `.aac`, `.m4a`, `.ogg`, `.opus`

| Operation | Engine | Lossy/Lossless | Batch Capable | Interactive Tool UI | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Convert Audio** | Bundled FFmpeg | Lossy/Lossless | Yes | No (Direct Drop) | Verified |
| **Trim Audio** | Bundled FFmpeg | Stream copy/re-encode | Single | Yes (Trim Slider UI) | Verified |
| **Compress / Change Bitrate** | Bundled FFmpeg | Lossy | Yes | Yes (Quality Slider) | Verified |
| **Normalize Volume** | Bundled FFmpeg (`loudnorm`) | Re-encode | Yes | No (Direct Drop) | Verified |

---

### 2.4 PDF Documents
* **Supported Input Formats:** `.pdf`
* **Supported Output Formats:** `.pdf`, `.png` (Pages to Image), `.jpg` (Pages to Image)

| Operation | Engine | Lossy/Lossless | Batch Capable | Interactive Tool UI | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Merge Multiple PDFs** | Managed (PdfSharp) | Lossless | Yes (Multi-file) | No (Direct Drop) | Verified |
| **Split PDF into Pages** | Managed (PdfSharp) | Lossless | Yes | No (Direct Drop) | Verified |
| **Extract Specific Pages** | Managed (PdfSharp) | Lossless | Single | Yes (Page Range Tool) | Verified |
| **Rotate PDF Pages** | Managed (PdfSharp) | Lossless | Yes | Yes (Rotation Tool) | Verified |
| **PDF Pages to Images** | Bundled FFmpeg / Ghostscript / PDFium | Lossy/Lossless | Yes | No (Direct Drop) | Verified |
| **Strip PDF Metadata** | Managed (PdfSharp) | Lossless | Yes | No (Direct Drop) | Verified |

---

### 2.5 Archives
* **Supported Input Formats:** `.zip`, `.tar`, `.gz`, `.tgz`, `.7z`, `.rar` (Extraction only)
* **Supported Output Formats:** `.zip`, `.tar`, `.gz`, `.tgz`, `.7z`

| Operation | Engine | Lossy/Lossless | Batch Capable | Interactive Tool UI | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Create Archive** | System.IO.Compression / SharpCompress | Lossless | Yes (Multi-file) | No (Direct Drop) | Verified |
| **Extract Archive** | System.IO.Compression / SharpCompress | Lossless | Yes | No (Direct Drop) | Verified |
| **Convert Archive Format** | Managed Archive Pipeline | Lossless | Yes | No (Direct Drop) | Verified |
| **Test Archive Integrity**| System.IO.Compression / SharpCompress | N/A | Yes | No (Direct Drop) | Verified |

---

### 2.6 Documents & Data
* **Supported Input Formats:** `.csv`, `.tsv`, `.txt`, `.json`, `.html`, `.md`
* **Supported Output Formats:** `.csv`, `.tsv`, `.txt`, `.json`, `.html`, `.md`, `.pdf`

| Operation | Engine | Lossy/Lossless | Batch Capable | Interactive Tool UI | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **CSV <-> JSON** | Managed Text Engine | Lossless | Yes | No (Direct Drop) | Verified |
| **CSV <-> HTML Table** | Managed Text Engine | Lossless | Yes | No (Direct Drop) | Verified |
| **Markdown -> HTML** | Managed Text Engine | Lossless | Yes | No (Direct Drop) | Verified |
| **Plain Text -> PDF** | Managed Document Engine | Lossless | Yes | No (Direct Drop) | Verified |

---

## 3. Known Limitations & Architectural Honesty
1. **Office File Formats (`.docx`, `.pptx`, `.xlsx`):** Direct layout-exact rendering to PDF without Microsoft Office installed requires a large headless rendering suite (e.g. LibreOffice). When Office is not present, Phase Drop extracts text, tables, and XML structures safely rather than claiming full font-identical reproduction.
2. **RAR Archive Creation:** Creating `.rar` archives is proprietary; Phase Drop extracts `.rar` but packs exclusively into open standard formats (`.zip`, `.7z`, `.tar`, `.gz`).
3. **DRM Protected Content:** Phase Drop does not bypass Widevine, FairPlay, or password-locked documents without the legitimate user key.
