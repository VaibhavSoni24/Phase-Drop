# Third-Party Software Notices and Information

This file contains the open source software notices, licenses, and acknowledgements for third-party software components bundled or redistributed with **Phase Drop**.

---

## 1. .NET 10 Runtime Framework
* **Project:** .NET Core / Runtime
* **Copyright:** .NET Foundation and Contributors
* **License:** MIT License

```
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## 2. FFmpeg & FFprobe
* **Project:** FFmpeg & FFprobe
* **Copyright:** (c) 2000-2026 the FFmpeg developers
* **Distribution Build:** Gyan.dev Windows Essentials Build (`ffmpeg-9.0.2-essentials_build.zip`)
* **Version:** 9.0.2 (Essentials Build)
* **Archive SHA-256:** `60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba`
* **License:** GNU General Public License (GPL) version 3.0 or later
* **Source Code:** [https://ffmpeg.org](https://ffmpeg.org) and [https://github.com/GyanD/codexffmpeg](https://github.com/GyanD/codexffmpeg)

```
FFmpeg is a trademark of Fabrice Bellard, originator of the FFmpeg project.

AUDITED BUILD CONFIGURATION:
The bundled FFmpeg and FFprobe binaries are built with:
  --enable-gpl --enable-version3 --enable-static --disable-w32threads
  --enable-libx264 --enable-libx265 --enable-libvpx --enable-libdav1d
  --enable-libopus --enable-libmp3lame --enable-libvorbis --enable-libwebp

Because --enable-gpl, --enable-version3, libx264, and libx265 are enabled in this binary distribution,
the effective license governing the redistributed binary files (ffmpeg.exe and ffprobe.exe) is
the GNU General Public License (GPL) version 3.0 or later.

Phase Drop itself is licensed under the GNU General Public License version 3.0 (GPL-3.0-or-later),
which is fully compatible with this bundled FFmpeg distribution. In compliance with the GPL v3:
1. Complete, corresponding source code for the exact FFmpeg version bundled is accessible upstream
   at https://ffmpeg.org/download.html and https://github.com/GyanD/codexffmpeg.
2. Build configurations, verified archive hash, and acquisition logic are preserved in scripts/acquire-ffmpeg.ps1,
   artifacts/publish/engines/media/ffmpeg-metadata.json, and docs/THIRD_PARTY_DEPENDENCIES.md.
3. Users receive this license notice and full GPL-3.0 text alongside the installer distribution.
```

---

## 3. PdfSharp
* **Project:** PDFsharp
* **Copyright:** (c) 2005-2026 empira Software GmbH, Troisdorf (Cologne Area), Germany
* **License:** MIT License

```
The MIT License (MIT)

Copyright (c) 2005-2026 empira Software GmbH, Troisdorf (Cologne Area), Germany

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```

---

## 4. SharpCompress
* **Project:** SharpCompress
* **Copyright:** (c) 2010-2026 Adam Hathcock
* **License:** MIT License

```
The MIT License (MIT)

Copyright (c) 2010-2026 Adam Hathcock

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```

---

## 5. NSIS (Nullsoft Scriptable Install System)
* **Project:** NSIS
* **Copyright:** (c) 1999-2026 Nullsoft and Contributors
* **License:** zlib/libpng License

```
This software is provided 'as-is', without any express or implied
warranty. In no event will the authors be held liable for any damages
arising from the use of this software.

Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not
   claim that you wrote the original software. If you use this software
   in a product, an acknowledgment in the product documentation would be
   appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be
   misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.
```
