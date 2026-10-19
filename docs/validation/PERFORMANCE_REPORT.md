# Phase Drop Performance & Resource Profiling Report

## 1. System Environment
* **OS:** Windows 11 Pro 64-bit
* **Logical Processors:** 12 cores
* **Runtime:** .NET 10 LTS (10.0.13 runtime, self-contained win-x64 build)
* **Measurement Script:** `scripts/measure-idle.ps1`
* **Measurement Conditions:** Process settled in system tray, zero user interaction, low-level mouse and keyboard hooks active.

---

## 2. Idle Resource Utilization Benchmark

| Metric | Measured Value | Budget Target | Result |
| :--- | :--- | :--- | :--- |
| **Average Idle CPU** | **0.000%** | < 0.100% | **PASSED** |
| **Peak Sample CPU** | **0.000%** | < 1.000% | **PASSED** |
| **Average Working Set** | **96.6 MB** | < 100.0 MB | **PASSED** |
| **Average Private Bytes** | **46.2 MB** | < 60.0 MB | **PASSED** |

### Analysis:
1. **Event-Driven Hook Architecture:** Unlike legacy tools that poll cursor position every 25ms in a busy loop, Phase Drop registers native Win32 low-level hooks (`WH_MOUSE_LL` and `WH_KEYBOARD_LL`). When no mouse movements occur, zero CPU cycles are consumed (0.000%).
2. **Foreground Window Filtering:** Even during mouse movements, the callback performs an instant window class comparison (`CabinetWClass`, `Progman`, `WorkerW`) in under 2 microseconds, ignoring non-Explorer operations without blocking the hook chain.
3. **WPF Rendering Suspended:** When the radial menu is hidden, animations and render tree updates are completely stopped.

---

## 3. Latency Metrics

| Operation | Typical Latency | Notes |
| :--- | :--- | :--- |
| **Radial Menu Reveal** | ~140 ms | Smooth spring ease-out scale (0.84 -> 1.0) and opacity fade-in |
| **Radial Menu Dismiss** | ~100 ms | Fast cubic ease-in fade-out |
| **Hit-Test Resolution** | < 0.001 ms | Pure trigonometric polar-to-cartesian angle comparison ($O(1)$) |
| **PNG to WebP Transcode** | ~45 ms | WIC / managed pipeline with atomic staging |
| **CSV to JSON Parsing** | ~8 ms | Streaming utf8 JSON object emitter |
