# ADR-001: Technology Stack and Architectural Boundaries

## Status
Accepted

## Date
2026-10-19

## Context
Phase Drop requires an extremely fast, resource-efficient, modern Windows 11 utility that runs quietly in the system tray, responds instantly to a Shift-drag gesture with a fluid radial menu, and performs high-speed local conversions on images, videos, audio, PDFs, archives, and documents.

Key technical requirements:
* Sub-0.1% idle CPU consumption and low working set.
* No requirement for end-users to install Python, runtimes, or codecs.
* Fluid custom UI matching Windows 11 Fluent design aesthetics.
* Portable core processing logic ready for future cross-platform expansion.
* Safe, non-destructive file processing with bounded concurrency.

## Decision

### 1. Primary Language & Runtime: C# on .NET 10 LTS
* **Choice:** C# with nullable reference types enabled targeting .NET 10 LTS.
* **Rationale:** .NET 10 delivers state-of-the-art JIT optimizations, native AOT compatibility options, robust memory management, and built-in asynchronous channel primitives (`System.Threading.Channels`).

### 2. Desktop UI Framework: WPF (Windows Presentation Foundation)
* **Choice:** WPF with modern Win32 DWM backdrop interop (Mica/Acrylic).
* **Rationale:** WPF offers hardware-accelerated vector rendering, flexible template customization for complex radial geometries, native OLE Drag-and-Drop handling, and Per-Monitor V2 DPI awareness.
* **Alternatives Considered & Rejected:**
  - *Electron / WebView2:* Rejected due to severe memory overhead (150-300MB idle), slow startup time, and complex IPC bridging.
  - *WinUI 3 / Windows App SDK:* Rejected due to fragile MSIX packaging requirements and inconsistent behavior across bare-metal environments.

### 3. Drag Integration: Event-Driven Win32 Hooks
* **Choice:** `WH_MOUSE_LL` and `WH_KEYBOARD_LL` low-level hooks with strict, non-blocking event filtering and zero-allocation callbacks.
* **Rationale:** Polling loops (e.g. 25ms timer) waste CPU cycles continuously even when the user is idle. The event-driven hook processes events only during actual user input, immediately passing unrelated events down the hook chain, achieving 0% idle CPU.

### 4. Media & Codec Engine: Bundled FFmpeg & FFprobe
* **Choice:** Audited, bundled FFmpeg static binaries placed under `engines/media/`.
* **Rationale:** FFmpeg is the gold standard for multimedia transcoding, demuxing, and stream filtering. Bundling redistributable binaries guarantees zero setup friction for the end user. Process execution is strictly controlled via `ProcessStartInfo.ArgumentList` to guarantee safety.

### 5. Packaging & Installation: NSIS (Nullsoft Scriptable Install System)
* **Choice:** NSIS 3.13 installer compiling to a self-contained release executable (`PhaseDrop-Setup-v1.0.0-x64.exe`).
* **Rationale:** NSIS produces compact, dependable installers that allow custom destination paths, theme pre-selection, Start Menu integration, and clean uninstallation without requiring elevated administrative runtime frameworks.

## Consequences
* **Positive:** Complete local self-reliance; zero external dependencies required of users; instant UI responsiveness; negligible background resource footprint; clean separation between portable processing core and Windows-specific shell integration.
* **Negative:** Bundled binaries increase release installer size (~80-100MB), which is standard and expected for full offline media utilities.
