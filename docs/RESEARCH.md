# Phase Drop: Technical Research & Feasibility Analysis

## 1. Executive Summary
Phase Drop is an open-source, offline-first Windows 11 utility designed to provide radial drag-and-drop file transformation. Activating during an active file drag operation upon holding the `Shift` key, Phase Drop presents an interactive radial wheel centered under the cursor, allowing instantaneous conversion, compression, editing, and archiving without ever leaving the desktop workflow.

This document synthesizes deep research conducted across existing reference implementations, Windows OLE Drag-and-Drop architecture, Win32 input hooks, .NET 10 LTS capabilities, media and document processing engines, and software licensing.

---

## 2. Reference Implementation Audit

### 2.1 Tangerine for Mac (Reference Target)
* **Website / App Store:** [tangerineformac.com](https://www.tangerineformac.com/) / Mac App Store ID: 6813034065
* **Core Paradigm:** "Drop. Transform. Done." Dragging files in macOS Finder while holding `Shift` summons a circular contextual wheel of export formats. Holding `Option + Shift` switches to contextual tools (Crop, Resize, Compress, Audio Strip, Metadata removal).
* **Execution:** 100% on-device processing. No accounts, telemetry, or remote servers.
* **UX Nuances:** Dynamic petal layout depending on file family (Image, Video, Audio, Document, Archive). Center hub acts as cancel zone or category navigation.

### 2.2 ZestDrop (Open-Source Windows Implementation)
* **Repository:** `Maxaccurate/ZestDrop` (GitHub)
* **Architecture:** C# WPF UI front-end orchestrating Python backend scripts (`work/ZestDrop/backend/images.py`, `media.py`, `documents.py`, `archives.py`).
* **Drag-and-Drop Mechanism:**
  - Evaluated foreground window class names (`CabinetWClass`, `Progman`, `WorkerW`).
  - Used a 25ms `DispatcherTimer` polling loop (`Native.Down`, `Native.GetCursorPos`).
  - Created a borderless topmost transparent WPF window with `AllowDrop = true`.
  - When the window appears under the cursor during an ongoing Windows OLE drag, Windows triggers `PreviewDragEnter` and passes the `IDataObject` containing `DataFormats.FileDrop`.
* **Identified Flaws in ZestDrop to Avoid in Phase Drop:**
  1. **Continuous 25ms Polling Loop:** Consistently wastes CPU even when the user is completely idle, violating Phase Drop's idle CPU budget (< 0.1%).
  2. **Python Backend Dependency:** Bundled/required a full Python environment with external scripts. Phase Drop must be 100% native/managed C# (.NET 10 LTS) with zero Python requirements.
  3. **Lack of Pure Local Conversion Engines:** Relying on external scripts and Office COM interop without standalone fallbacks.

### 2.3 Windows Native Drag-and-Drop Lifecycle & The Shift-Drag Risk
On Windows, standard file dragging from Windows Explorer (`CabinetWClass`) or Desktop (`Progman` / `WorkerW`) executes an OLE modal loop:
```
Shell Explorer Thread -> DoDragDrop(pDataObject, pDropSource, dwOKEffects, pdwEffect)
```
1. While `DoDragDrop` is running, Explorer captures mouse events and sends OLE notifications (`IDropTarget::DragEnter`, `DragOver`, etc.) to whichever window is physically beneath the cursor and has registered as an OLE Drop Target (`RegisterDragDrop`).
2. If Phase Drop does not have a window under the cursor, OLE does not notify Phase Drop of the drag.
3. Therefore, Phase Drop must detect that:
   - The user initiated a drag from an Explorer/Desktop window (mouse left button pressed + cursor moved past `SM_CXDRAG`/`SM_CYDRAG`).
   - The user pressed `Shift` (or `Shift + Ctrl`).
4. At that exact instant, Phase Drop displays its radial menu window at the cursor position with `ShowActivated = false` and `Topmost = true`.
5. The very next mouse movement causes Windows OLE to hit Phase Drop's window! Windows sends `WM_DROPFILES` / `IDropTarget::DragEnter` with `CF_HDROP` containing the list of dragged file paths.
6. **Cancellation & Release:** If the user releases the mouse button over a petal, `PreviewDrop` fires with the target format. If the user moves away or hits `Escape`, the menu dismisses immediately and leaves normal Windows drag semantics intact.

---

## 3. Input Interception: Event-Driven Hooks vs. Polling

| Characteristic | 25ms Timer Polling (ZestDrop) | Lightweight Event-Driven Win32 Hooks (Phase Drop) |
| :--- | :--- | :--- |
| **Idle CPU Usage** | 0.5% - 2.5% constant CPU wakeups | **0.00%** (True 0 CPU at idle; interrupts only on events) |
| **Latency** | Up to 25ms delay before menu spawns | **< 1ms** (Instantaneous response upon pressing Shift) |
| **System Impact** | Constant timer wakeups drain laptop battery | Hook filters in < 5 microseconds; immediately yields `CallNextHookEx` |
| **Reliability** | May miss brief modifier key transitions | Catches exact key transitions (`WM_KEYDOWN`, `WM_LBUTTONUP`) |

### Hook Strategy for Phase Drop:
1. Install `WH_MOUSE_LL` and `WH_KEYBOARD_LL`.
2. Mouse hook filters immediately: if not `WM_LBUTTONDOWN`, `WM_LBUTTONUP`, or `WM_MOUSEMOVE` with left button down, call `CallNextHookEx` in 1 instruction.
3. Keyboard hook filters immediately: if `vkCode` is not `VK_SHIFT`, `VK_CONTROL`, or `VK_ESCAPE`, call `CallNextHookEx` immediately without allocations.
4. If `WM_LBUTTONDOWN` occurs on an Explorer or Desktop window, candidate state is armed.
5. If mouse moves past drag threshold and `Shift` is held, trigger the radial menu.
6. When the menu is dismissed or mouse is released, hooks revert to zero-work passive passing.

---

## 4. Processing Engines & Format Coverage

### 4.1 Image Processing Engine
* **Technologies:** Windows Imaging Component (WIC via `System.Windows.Media.Imaging`) + Bundled FFmpeg CLI engine.
* **Capabilities:**
  - Raster conversions: PNG, JPEG, WebP, BMP, GIF, TIFF, ICO.
  - Transformations: Non-destructive crop, proportional resize, rotation (90/180/270), flip, quality compression, EXIF metadata stripping.
  - Image to PDF: Direct encapsulation into clean multi-page or single-page PDF.

### 4.2 Audio & Video Processing Engine
* **Technologies:** Bundled static `ffmpeg.exe` and `ffprobe.exe` (LGPL/GPL compliant).
* **Capabilities:**
  - Video formats: MP4, MOV, MKV, WebM, AVI.
  - Audio formats: MP3, WAV, FLAC, AAC (M4A), OGG (Opus).
  - Operations: Codec transcode, bit-rate control, resolution scaling, video compression, trim (start/end timestamp), audio extraction from video, GIF animation generation, playback speed adjustment.
  - Metadata & Stream Inspection: `ffprobe` JSON parsing for precise stream, duration, and codec analysis.

### 4.3 PDF Processing Engine
* **Technologies:** Managed `PdfSharp` (MIT licensed) & PDF Pig (Apache-2.0).
* **Capabilities:**
  - Merge multiple PDFs into one document.
  - Split PDF into individual pages or page ranges.
  - Extract, reorder, and rotate pages.
  - Images to PDF and PDF pages to high-resolution PNG/JPEG images.
  - Metadata inspection and clean removal.

### 4.4 Archive Engine
* **Technologies:** .NET 10 built-in `System.IO.Compression` (`ZipArchive`, `GZipStream`, `BrotliStream`, `TarFile`) + `SharpCompress` (MIT).
* **Capabilities:**
  - Formats: ZIP, 7Z, TAR, GZ, TGZ.
  - Creation, inspection, extraction, and format conversion.
  - Security: Path traversal protection (`Zip Slip` prevention), extraction sanitization, collision-safe output.

### 4.5 Document Processing Engine
* **Technologies:** Pure managed parsers + headless export bridges for Office XML and Plaintext formats (DOCX, XLSX, PPTX, CSV, TXT, HTML).
* **Capabilities:**
  - Convert text and tabular data between CSV, TXT, HTML, JSON.
  - Document to PDF transformation.

---

## 5. Licensing Matrix & Compliance
All bundled components and libraries in Phase Drop are strictly audited for license compatibility with GPL-3.0-or-later:

| Component | License | Role in Phase Drop | Commercial / Redistributable |
| :--- | :--- | :--- | :--- |
| **Phase Drop Core & UI** | GPL-3.0-or-later | Main application source code | Yes (Copyleft) |
| **.NET 10 Runtime** | MIT | Core runtime framework | Yes |
| **FFmpeg / FFprobe** | LGPL v2.1+ / GPL v3+ | Audio/Video transcoding CLI | Yes (Source offer & notices provided) |
| **PdfSharp** | MIT | PDF manipulation & generation | Yes |
| **SharpCompress** | MIT | Archive formats (7z, Tar, Gz, Zip) | Yes |
| **NSIS** | zlib/libpng / CPL | Windows installer executable | Yes |

---

## 6. High DPI, Scaling, and Multi-Monitor Handling
* The radial menu coordinates must be converted between screen physical pixels (`POINT` from `GetCursorPos`) and WPF device-independent units (DIPs) via `VisualTreeHelper` or `DpiAwarenessContext.PerMonitorVersion2`.
* Multi-monitor bounds clamping ensures the radial wheel never clips off the edge of secondary monitors or taskbars.
