# Phase Drop Security Architecture & Threat Model

## 1. Security Philosophy
Phase Drop executes exclusively on the user's local workstation. The application accepts files dropped from arbitrary locations; therefore, **all input files are treated as untrusted data**. 

Phase Drop adopts a strict defense-in-depth posture:
1. **Zero External Network Connectivity:** No telemetry, analytics, cloud processing, or remote server communication during file conversion.
2. **No Command-Line String Injection:** All external process invocations (`ffmpeg`, `ffprobe`) use `ProcessStartInfo.ArgumentList` with explicit arrays of strings, completely bypassing the Windows shell (`cmd.exe` / `powershell.exe`). Arbitrary characters, quotes, and whitespace in filenames cannot trigger command injection.
3. **Strict Path Traversal Protection (Zip-Slip Defense):** Archive extraction verifies that the canonical destination path of every archive entry starts with the canonical path of the target directory. Entries with `../` or absolute drive roots (`C:\`) outside the target are rejected immediately.
4. **Collision-Safe & Non-Destructive File Output:** Original files are opened strictly in read-only mode (`FileShare.Read`). Output files are written to unique collision-safe names (`name (1).ext`) or temp staging files that are atomically committed only upon full success.
5. **Clean Cancellation & Orphan Prevention:** When a job is cancelled or crashes, partially written files are cleanly deleted. Child process trees are forcefully terminated using Win32 Job Objects / process tree killing to ensure zero orphaned background workers.
6. **Resource Exhaustion & Bomb Mitigation:** Archive extraction, image decoding, and video transcoding enforce bounded memory and file-size sanity checks. Decompression ratios exceeding safe thresholds are aborted.
7. **Least Privilege Execution:** Phase Drop runs under the standard user token. It does not require or request administrator elevation (`UAC`). User configuration and cache files reside in `%LOCALAPPDATA%\PhaseDrop`.

---

## 2. Threat Modeling Matrix

| Threat Category | Potential Attack Vector | Phase Drop Countermeasure |
| :--- | :--- | :--- |
| **Command Injection** | Filenames containing shell metacharacters: `file"; calc.exe; ".mp4` | `ProcessStartInfo.UseShellExecute = false`; arguments passed solely via `ArgumentList.Add()`. |
| **Path Traversal (Zip Slip)** | Archive containing malicious entries like `../../Windows/System32/evil.dll` | Canonical path validation: `Path.GetFullPath(dest).StartsWith(targetDir)`. Violations throw `SecurityException`. |
| **Decompression Bomb (Zip Bomb)** | Small archive expanding into hundreds of gigabytes | Maximum expansion ratio enforcement and disk space pre-check before extraction. |
| **Symlink / Hardlink Attacks** | Archives creating symlinks to sensitive host files | Symlinks in untrusted archives are either skipped or safely normalized without following host links. |
| **Process Hang / CPU Starvation** | Malformed media file causing infinite loop in encoder | Strict process timeout and cooperative `CancellationToken` cancellation with process tree killing. |
| **Original File Destruction** | Accidental overwrite or corruption of user's precious photo/video | Read-only input access; collision-safe naming; atomic rename of `.tmp` file. |

---

## 3. Vulnerability Reporting Policy
If you discover a security vulnerability in Phase Drop, please report it privately via GitHub Security Advisories or by emailing the project maintainers. Do not file public GitHub issues for unpatched vulnerabilities.
Reports will be acknowledged within 48 hours, and a patch will be prepared and released promptly.
