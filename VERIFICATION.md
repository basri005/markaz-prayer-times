# Verification — 2.0.1

Startup fix tested on 3 November 2026: installer created the per-user Startup shortcut with the installed executable, `--background` and its working directory; removed the legacy Run entry; launching that shortcut started one watcher with no main window. Enable/disable and uninstall cleanup passed. Existing application self-tests also passed. An actual Windows restart/sign-in was not performed, to avoid interrupting the user's session.

Tested on the development Windows laptop on 1 November 2026.

- Release executable: all 22 self-test checks passed, including all matching-time adjustments, countdowns, rounded midnight, missing next-day Fajr, month/year/leap-year rollover and catch-up, idle settings, and rejection of invalid or incomplete imports.
- November image: two OCR passes agreed; all 300 extracted times matched the verified bundled CSV. The same image was rejected when December was requested.
- Isolated December preview: missing-month screen, live website failure/retry, and explicitly labelled 30 November fallback were visually checked. Fallback suppressed countdown and midnight.
- Upload: local file picker and image/table review dialog were visually checked using the November poster.
- Installer: silent installation into a temporary folder, installed executable self-tests, and silent uninstall passed. Existing startup configuration and preferences were restored after this check.

Limits: no clean Windows machine was available to exercise missing-runtime installation. Future monthly posters and changed website layouts cannot be tested in advance. The executable/installer are unsigned. Automatic imports support the current JPG/PNG layout; unsupported or unvalidated files require user intervention. Manual taskbar pinning remains a Windows action.
