# Prayer Times — Markaz us-Sunnah

A Windows 11 desktop timetable and automatic idle display for Markaz us-Sunnah, Alperton. This is an independent community project, not an official masjid app.

## Install and use

Download `Prayer-Times-Setup-2.0.0.exe` from this project's Releases, then choose your options:

- Start quietly when signing in to Windows.
- Open automatically after a chosen idle delay (1–120 minutes).
- Restrict automatic display to mains power.
- Check the masjid website for monthly timetables.
- Add desktop and Start menu shortcuts.
- Open the app folder to complete taskbar pinning: right-click **Prayer Times.exe → Show more options → Pin to taskbar**. Windows owns that final pin action.

The installer targets Windows 11 x64. It installs per user, includes offline English image recognition and installs Microsoft's signed Visual C++ x64 runtime if missing (that prerequisite may request administrator approval). The app uses Windows' .NET Framework 4.8. No account, API key or cloud OCR is needed. This build is not code-signed; Windows may show an unknown-publisher warning. Signing is recommended before broad distribution.

Click Prayer Times to open it immediately. Move the mouse or press a key to return. The tray menu provides **Settings**, **Attach timetable**, and **Check masjid website**. The missing-timetable screen stays open for interaction; press Escape to dismiss it. Windows locking and lid behavior remain in force.

## Monthly updates

The running app checks `https://musunnah.com/prayer-timetable/` for the next month on the last calendar day in London time. It retries missing months every six hours, and catches up on the next launch or wake if the laptop was off. There is no separate cloud service and nothing runs while the laptop is off.

Only images hosted on the official website and linked from that page are considered. The reader supports the masjid's current full-page JPG/PNG layout with one Maghrib column. It verifies the printed month/year, date/weekday pairs, all days, ten times per day, ordering and day-to-day consistency. Two differently processed readings must agree before an automatic import. These checks reduce OCR errors; they cannot guarantee that every future image is correct. Changed layouts, PDFs, unreadable images and disagreements are rejected rather than guessed. A human should still check new timetables against the masjid's notice.

If next month's timetable is missing on the last day, an attention screen names the upcoming month and offers **Continue today's timetable**, since today's valid times remain available. Once the current month is missing, the screen offers **Attach timetable**, **Check website again**, and **Display yesterday temporarily**. The fallback is enabled only if the actual previous day's data exists. It is labelled with its source date, does not provide a misleading live countdown or midnight value, lasts only for the current day/session, and is replaced as soon as valid current data arrives. It never chains an older fallback forward.

Uploads accept full-month JPG, PNG or CSV. Image reading happens locally. An image and extracted table are shown for review before saving. Invalid/partial imports leave saved timetables unchanged. The app stores raw published times, so the display adjustment below is applied only once.

## Time conventions

- Where a printed jama‘ah time equals its adhan/start time, the display and countdown use **start + 5 minutes**. Maghrib's single printed time is treated as its start time, so 16:38 displays jama‘ah 16:43. This is a requested app convention; the current printed November note says Maghrib is prayed at its start time. Confirm the convention with the masjid before distributing the app as an authoritative timetable.
- Distinct printed jama‘ah times are unchanged. Friday's Dhuhr-column time is labelled as khutbah.
- Midnight is the elapsed-time midpoint between today's **raw Maghrib start** and tomorrow's **Fajr start**, rounded down to a whole minute. Missing next-day Fajr gives no calculated midnight.
- The countdown selects jama‘ah, sunrise and midnight chronologically.
- All dates/times use Europe/London (Windows `GMT Standard Time`).

## Storage and privacy

Data lives under `%LOCALAPPDATA%\MarkazPrayerTimes\Data`, separate from installed binaries. It contains settings, monthly CSVs, short provenance notes and last check status. Uploaded source paths/images are not sent anywhere. Network access is limited to retrieving the public masjid page and images. There is no telemetry. The bundled November 2026 CSV is a starting dataset; later months download independently on each laptop.

Uninstall through Windows Installed Apps. Timetable data is preserved so reinstalling does not lose imports. The taskbar pin may need manual removal after uninstall.

## Build

On Windows with Windows PowerShell 5.1+ and Inno Setup 7 (or compatible Inno Setup 6):

```powershell
.\build.ps1 -InnoCompiler 'C:\Program Files\Inno Setup 7\ISCC.exe'
```

The script uses the installed .NET Framework C# compiler, downloads pinned Tesseract 5.2.0 and English model files with SHA-256 verification, downloads Microsoft's signed runtime installer and builds into `build/` and `dist/`. No .NET SDK is required. `Prayer Times.exe --test` runs deterministic checks in isolated temporary data. Add `--image "path\to\November-2026.jpeg"` to verify OCR against all 300 bundled times. Original uploaded images are deliberately excluded from the repository.

For visual testing only, `--inspect --data-dir <isolated-folder> --inspect-date 2026-12-01T12:00:00` opens an isolated missing-month preview. Never pass test-date flags for normal use.

## GitHub distribution

Publish this source folder as a public repository, keep generated binaries out of Git, and attach the installer and `SHA256SUMS.txt` to a versioned GitHub Release. The included Actions workflow builds and tests on Windows, uploading a release-candidate artifact; it does not publish releases automatically. Before a public release, test on a clean Windows 11 laptop, confirm the jama‘ah convention with Markaz us-Sunnah, and consider code signing. Keep the masjid timetable source separate from software-update distribution: this app does not download or execute new program code.

See `VERIFICATION.md` for what was actually tested and `THIRD-PARTY-NOTICES.md` for dependency licensing.
