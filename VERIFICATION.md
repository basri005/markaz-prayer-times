# Verification — 2.0.3

Paused-video correction: Windows reported the user's existing session as Paused. Removed unconditional full-screen/presentation suppression. The regression check that a paused session permits an otherwise due idle display passed. Native integration checks also passed: a real muted Windows MediaPlayer session was detected while playing, and suppression released when it was paused. The user's existing media was left unchanged. Earlier results below describe their respective releases.

Media protection tested on 15 November 2026: checks passed for playback suppression, paused/stopped states, full idle delay after playback and full-screen/presentation states. The native Windows media query succeeded and detected existing playback on the laptop. The controlled play/pause test was skipped because other media was already playing; the user's playback was not changed. No end-to-end movie-length idle session or every browser/player combination was tested. `--test --media` runs an additional muted native media test when no other session is playing.

Startup fix tested on 3 November 2026: installer created the per-user Startup shortcut with the installed executable, `--background` and its working directory; removed the legacy Run entry; launching that shortcut started one watcher with no main window. Enable/disable and uninstall cleanup passed. Existing application self-tests also passed. An actual Windows restart/sign-in was not performed, to avoid interrupting the user's session.

Tested on the development Windows laptop on 1 November 2026.

- Release executable: all 22 self-test checks passed, including all matching-time adjustments, countdowns, rounded midnight, missing next-day Fajr, month/year/leap-year rollover and catch-up, idle settings, and rejection of invalid or incomplete imports.
- November image: two OCR passes agreed; all 300 extracted times matched the verified bundled CSV. The same image was rejected when December was requested.
- Isolated December preview: missing-month screen, live website failure/retry, and explicitly labelled 30 November fallback were visually checked. Fallback suppressed countdown and midnight.
- Upload: local file picker and image/table review dialog were visually checked using the November poster.
- Installer: silent installation into a temporary folder, installed executable self-tests, and silent uninstall passed. Existing startup configuration and preferences were restored after this check.

Limits: no clean Windows machine was available to exercise missing-runtime installation. Future monthly posters and changed website layouts cannot be tested in advance. The executable/installer are unsigned. Automatic imports support the current JPG/PNG layout; unsupported or unvalidated files require user intervention. Manual taskbar pinning remains a Windows action.
