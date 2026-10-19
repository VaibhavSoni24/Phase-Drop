# Contributing to Phase Drop

Thank you for your interest in contributing to Phase Drop! We welcome contributions, bug reports, feature proposals, and documentation enhancements.

---

## Development Prerequisites
* **Operating System:** Windows 11 x64 (Build 22621+ recommended)
* **SDK:** [.NET 10 LTS SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* **Media Engine:** FFmpeg & FFprobe (placed in `engines/media/` or bundled during build)
* **Packaging Tool:** [NSIS 3.13+](https://nsis.sourceforge.io/) (for building the installer)
* **PowerShell:** 7.0+ (or Windows PowerShell 5.1)

---

## Building the Project
From the repository root:
```powershell
# Restore dependencies and build Release configuration
.\scripts\build.ps1

# Run the test suite
.\scripts\test.ps1

# Package the NSIS installer
.\scripts\package.ps1
```

---

## Coding Standards & Architectural Guidelines
1. **Separation of Concerns:** Keep core conversion logic in `PhaseDrop.Core` and `PhaseDrop.Engines.*`. Do not reference WPF or Win32 APIs from the core domain.
2. **Nullable Reference Types:** Nullable annotations are enabled across the entire solution. Do not disable or suppress CS8600-CS8604 warnings without justification.
3. **No Heavy Polling:** All Windows integration must remain event-driven. Do not introduce continuous polling loops or CPU-intensive background timers.
4. **Safety First:** Never overwrite user files in place. Always use `CollisionSafeWriter` and atomic temporary files.
5. **Security:** Never concatenate strings to build process command lines. Use `ProcessStartInfo.ArgumentList`.

---

## Pull Request Guidelines
1. Fork the repository and create your feature branch: `git checkout -b feat/my-new-feature`.
2. Follow Conventional Commits: `feat:`, `fix:`, `docs:`, `test:`, `refactor:`.
3. Ensure all automated tests pass: `.\scripts\test.ps1`.
4. Submit a clear Pull Request detailing the changes, motivation, and manual test evidence.
