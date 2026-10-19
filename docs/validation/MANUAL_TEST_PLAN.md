# Phase Drop — Windows 11 Manual Drag Integration Test Plan

## 1. Test Environment Specification
* **Operating System:** Windows 11 Pro 64-bit (Build 22631 / 23H2 or 24H2)
* **Display Configuration:**
  - Primary Monitor: 1920x1080 @ 100% DPI Scaling
  - Secondary Monitor (if available): 2560x1440 @ 125% DPI Scaling or 3840x2160 @ 150% DPI Scaling
* **Shell Windows:**
  - File Explorer (`CabinetWClass`)
  - Windows Desktop (`Progman` / `WorkerW`)

---

## 2. Test Cases

### TC-01: Single File Shift-Drag from Windows Desktop
1. Select an image file (e.g., `sample.png`) on the desktop.
2. Hold the left mouse button and begin dragging the file.
3. While dragging, press and hold the `Shift` key.
4. **Expected Result:**
   - The Phase Drop radial menu appears smoothly at the pointer coordinates.
   - The center hub displays `sample.png` and `CONVERT` mode.
   - 8 image conversion petals are rendered (PNG, JPEG, WebP, GIF, BMP, TIFF, ICO, PDF).
5. Move the mouse cursor over the `WebP` petal.
   - **Expected Result:** The `WebP` petal highlights in accent blue, scales slightly, and center status reads "Modern high efficiency web image".
6. Release the left mouse button over the `WebP` petal.
   - **Expected Result:**
     - The radial menu dismisses smoothly.
     - The floating task card appears at bottom-right showing progress.
     - An output file `sample.webp` is created in the same desktop directory.
     - The original `sample.png` is preserved untouched.

---

### TC-02: Multi-File Drag from File Explorer
1. Open File Explorer to a folder containing multiple documents or images.
2. Select 3 files (e.g., `doc1.pdf`, `doc2.pdf`, `doc3.pdf`).
3. Drag with the left mouse button and hold `Shift`.
4. **Expected Result:**
   - The radial menu appears centered near cursor.
   - The center hub displays `3 Files` and PDF operations.
5. Release mouse over `Merge PDFs`.
   - **Expected Result:**
     - Task card displays merge progress.
     - `Merged_Document.pdf` is created containing all pages in sequence.
     - Originals are untouched.

---

### TC-03: Cancellation & Escape Key Behavior
1. Drag any file from Explorer with `Shift` held so the radial menu is visible.
2. While mouse is still held down, press `Escape`.
   - **Expected Result:** Radial menu closes immediately; Explorer drag is not corrupted.
3. In a separate test: Drag into the center hub ("Drop here to Cancel") and release the mouse button.
   - **Expected Result:** Radial menu dismisses; no jobs are dispatched; no files are modified.

---

### TC-04: Tools Mode (`Ctrl+Shift` or Center Toggle)
1. Drag an image while holding `Ctrl+Shift` (or click the center hub).
2. **Expected Result:**
   - The menu switches to `TOOLS` mode (purple accent badge).
   - Petals show: Crop, Resize, Compress, Rotate 90°, Flip H, Strip EXIF, To PDF.
3. Drop onto `Crop`.
   - **Expected Result:**
     - The interactive `CropWindow` opens with the loaded image.
     - Draggable crop frame and aspect ratio presets (1:1, 4:3, 16:9, Free) function accurately.
     - Clicking "Apply & Crop" produces a collision-safe cropped image file.

---

### TC-05: Monitor Edge & Multi-Monitor Clamping
1. Drag a file near the extreme right, bottom, or top edge of the monitor.
2. Hold `Shift` to trigger the menu.
   - **Expected Result:**
     - The radial menu is clamped within the visible monitor work area (`rcWork`).
     - No petals are clipped or rendered off-screen.
