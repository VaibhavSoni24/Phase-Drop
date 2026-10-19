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
