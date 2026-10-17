# Local memory checks

Each execution needs the owner's explicit approval. These checks do not run in CI or open a USB device.

- Build `tests/VibePanel.MemoryChecks/VibePanel.MemoryChecks.csproj` in Release x64, then run `tests/Measure-PreviewMemory.ps1`. It uses an isolated settings file and a fake engine. It covers fast preview, twenty theme choices, fades, appearance, colors, fonts, quality, simulated output and errors. It checks actual browser drawing and stops at 900 MB for the process family. Reports stay in `artifacts/memory-checks/`.
- Use the bundled Windows Python to run `tests/check_worker_memory.py`. It renders 400 synthetic frames through the real worker: twenty themes, light/dark, three sizes, font choices, four JPEG qualities, filters, artwork cache eviction and error recovery. It samples private bytes and stops the worker at 800 MB. It does not read live media or user artwork.
- Build `tests/native/UtfConversionChecks.vcxproj` with the existing C++ v143 tools, then run `artifacts/memory-checks/native/UtfConversionChecks.exe`. It checks Unicode, independent string ownership and 4,000 large UTF round trips with a memory-growth bound.

These are focused local checks. They do not establish long-term memory use, CPU cost, USB operation, ARM64 behavior or a completed security review.
