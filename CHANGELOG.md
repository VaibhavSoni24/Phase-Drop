# Changelog

All notable changes to the **Phase Drop** project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-10-19

### Added
- **Core Architecture & Engine Orchestration:**
  - Portable .NET 10 LTS capability registry covering Images, Video, Audio, PDF, Documents, and Archives.
  - Asynchronous bounded-concurrency job queue (`System.Threading.Channels`) with progress reporting, pause/resume, and cancellation.
  - Collision-safe atomic file writer (`CollisionSafeWriter`) preventing original file overwrites.
- **Fluent Windows 11 Radial Menu:**
  - Custom vector-rendered radial wheel with animated petals, icons, hover accents, and dynamic layout.
  - Shift-drag activation from Windows Explorer (`CabinetWClass`) and Desktop (`Progman`/`WorkerW`).
  - Contextual tools wheel for specialized operations (Crop, Resize, Compress, Trim, Metadata).
  - High-DPI and multi-monitor screen boundary clamping.
  - Fluent theme support: System default, Dark mode, and Light mode with Windows 11 backdrop effects.
- **Engines & Processing Pipelines:**
  - Image transformations: crop with draggable handles, resize, rotation, flip, format conversion (PNG, JPEG, WebP, BMP, GIF, TIFF, ICO), and EXIF stripping.
  - Video & Audio conversion powered by bundled FFmpeg: trim, compress, extract audio, animated GIF generation, and stream inspection.
  - PDF manipulation: merge, split, extract pages, rotate pages, and image-to-PDF conversion.
  - Archive management: ZIP, 7Z, TAR, and GZ creation and extraction with strict Zip-Slip protection.
- **System Tray & Settings:**
  - Resident background mode with sub-0.1% idle CPU consumption.
  - System tray icon with quick actions, recent files, and output location configuration.
  - Comprehensive Settings dialog for theme, drag thresholds, concurrency limits, and startup preferences.
- **Installer & Packaging:**
  - Full NSIS 3.13 installer with destination selection, theme selection, Start Menu integration, and clean uninstallation.
  - Automated build, test, and packaging scripts in PowerShell.

### Fixed & Hardened
- **CI/CD & Packaging:**
  - Repaired GitHub Actions workflow failure caused by missing NSIS in PATH by dynamically appending Chocolatey and ProgramFiles NSIS directories.
  - Eliminated all machine-specific paths (e.g. `C:\Users\shubh`, `D:\Softwares`, `C:\Program Files\dotnet\dotnet.exe`) across all packaging and CI scripts.
  - Replaced WinGet scans with deterministic FFmpeg acquisition and verification script (`scripts/acquire-ffmpeg.ps1`).
  - Added PE header check, size plausibility (>50MB), SHA-256 validation, and live binary `--version` checks in `scripts/validate-release.ps1`.
- **Engine Correctness:**
  - Implemented RFC 4180 compliant CSV state machine handling quoted delimiters, embedded newlines, escaped quotes (`""`), and UTF-8 BOM.
  - Enforced deterministic JSON-to-CSV header discovery and HTML entity escaping on user-provided data.
  - Added explicit encoder validation for images (rejecting unsupported extensions with `NotSupportedException`) and alpha transparency flattening onto white background for JPEG.
  - Hardened archive extraction with canonical destination path checks, Zip-Slip traversal rejection, and automatic cleanup of partial output on failure.
  - Reconciled PDF registry to honestly reflect capabilities (removed placeholder PDF-to-image rasterization; provided archive options).
  - Hardened user settings with atomic persistence (`.tmp` to target move) and corrupted settings fallback with automated backup.
