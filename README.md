# 🐱 MeetingReminder (Windows)

A Windows app that flies a Catppuccin-themed cat across your screen before each
calendar meeting, trailing a banner with the meeting title — e.g.
**"Standup in 5 min"**.

Windows port of [conniexu444/meeting-reminder](https://github.com/conniexu444/meeting-reminder)
(macOS). Supports **Windows Calendar** (Outlook.com, iCloud, Exchange) and
**Google Calendar** (direct OAuth2 integration).

---

## Requirements

- **Windows 10** (1809+) or **Windows 11**
- **.NET 8 SDK** or later (for building from source)
- At least one of:
  - Windows Calendar app with an account configured, **or**
  - A Google account + OAuth2 credentials from Google Cloud Console

---

## Install

### From MSIX (recommended)

1. Install the signing certificate (one-time, as Administrator):
   ```powershell
   Import-Certificate -FilePath MeetingReminder.cer -CertStoreLocation Cert:\LocalMachine\Root
   ```
2. Double-click **`dist\MeetingReminder.msix`** → **Install**

See [BUILD-MSIX.md](BUILD-MSIX.md) for building the MSIX yourself.

### From source

```powershell
git clone <repo-url>
cd meeting-reminder-win
dotnet build
dotnet run --project MeetingReminder.App
```

Or open `MeetingReminder.sln` in Visual Studio and press **F5**.

---

## Build Scripts

| Command                    | What it does                                          |
|----------------------------|-------------------------------------------------------|
| `build.bat`                | Debug build                                           |
| `build.bat release`        | Release build                                         |
| `build.bat test`           | Build + run xUnit tests                               |
| `build.bat publish`        | Self-contained single-file exe (win-x64)              |
| `build.bat msix`           | Full MSIX pipeline: cert → assets → tests → package   |
| `build.bat clean`          | Wipe bin/obj                                          |
| `build-msix-quick.bat`     | One-click MSIX build with default dev password        |
| `recreate-cert.bat`        | Recreate the signing certificate                      |

---

## Usage

1. The app launches with a Catppuccin Mocha-themed window and a system tray icon
2. Connect a calendar source:
   - Click **Windows Calendar** to use the built-in Calendar app, **or**
   - Click **Google Calendar** to sign in via your browser (OAuth2)
3. That's it — every 60 seconds the app checks your calendar and shows the
   flying cat ~5 minutes before each meeting
4. The **Test airplane** button triggers the animation on demand

Closing or minimising the window sends it to the system tray. Right-click the
tray icon for quick actions (Test airplane, Settings, Exit).

---

## Calendar Sources

### Windows Calendar

Reads from the Windows Calendar app via the WinRT Appointments API. Any account
you've connected (Outlook.com, Google via the Calendar app, iCloud, Exchange)
shows up automatically.

### Google Calendar (direct)

Direct OAuth2 integration via the Google Calendar API. Your browser opens for
consent; tokens are cached locally so you only sign in once.

**Setup:**

1. Create a project in [Google Cloud Console](https://console.cloud.google.com/)
2. Enable the **Google Calendar API**
3. Create an **OAuth 2.0 Client ID** (Desktop application)
4. Enter your credentials in MeetingReminder:
   - Open **Settings → Google Calendar**
   - Paste the **Client ID** and **Client Secret**
   - Click **Connect Google Calendar** → sign in via browser

Alternatively, download the credentials JSON from Google Cloud Console and save
it as `client_secrets.json` next to the exe or in `%LOCALAPPDATA%\MeetingReminder\`.
The app will use that file if the Settings fields are empty.

Credentials and tokens are persisted to `%LOCALAPPDATA%\MeetingReminder\` and
automatically restored on subsequent launches. Click **Disconnect** to revoke
and delete cached tokens.

---

## Customization

### Settings tab

| What                            | Where                        |
|---------------------------------|------------------------------|
| Alert timing (1–30 min)         | Settings → Alert slider      |
| Flight speed (Slow/Normal/Fast) | Settings → Plane speed       |
| Google Client ID / Secret       | Settings → Google Calendar   |
| Theme (Mocha / Latte)           | Settings → Appearance        |
| Accent colour (14 options)      | Settings → Accent picker     |
| Start with Windows              | Settings → Startup           |
| Start minimised to tray         | Settings → Startup           |

### Theming

Uses the **Catppuccin** design system:
- **Mocha** (dark, default) and **Latte** (light)
- 14 accent colours to choose from
- DWM dark-mode title bar on Windows 10/11
- Runtime theme + accent swapping (no restart needed)

---

## Project Structure

```
meeting-reminder-win/
├── MeetingReminder.sln
├── build.bat                        # Build convenience wrapper
├── build-msix-quick.bat             # One-click MSIX build
├── recreate-cert.bat                # Recreate signing cert
├── BUILD-MSIX.md                    # MSIX packaging docs
├── scripts/
│   ├── create-certificate.ps1       # Self-signed code-signing cert
│   ├── generate-assets.ps1          # Procedural MSIX logo PNGs
│   ├── build-msix.ps1               # Publish → stage → pack → sign
│   └── build-all.ps1                # End-to-end MSIX pipeline
├── MeetingReminder.Core/            # Platform-agnostic logic
│   ├── Models/
│   │   ├── AppConfig.cs             # Persisted config record
│   │   └── CalendarEvent.cs         # Calendar event DTO
│   ├── ICalendarService.cs          # Calendar provider interface
│   ├── CalendarPoller.cs            # 60s timer, alert dedup
│   └── ConfigService.cs             # JSON config at %LOCALAPPDATA%
├── MeetingReminder.App/             # WPF application
│   ├── Assets/
│   │   └── cat.png                  # Cat mascot for the overlay
│   ├── Themes/
│   │   ├── Mocha.xaml               # Catppuccin Mocha palette
│   │   ├── Latte.xaml               # Catppuccin Latte palette
│   │   └── Styles.xaml              # All WPF control styles
│   ├── Services/
│   │   ├── WindowsCalendarService   # WinRT Appointments API
│   │   ├── GoogleCalendarService    # Google Calendar OAuth2
│   │   ├── ThemeManager             # Runtime Mocha/Latte swap
│   │   ├── TrayManager              # System tray + context menu
│   │   ├── TrayIconRenderer         # Procedural Catppuccin icon
│   │   ├── NotificationService      # Toast + balloon fallback
│   │   ├── StartupManager           # HKCU Run registry
│   │   └── RollingFileLoggerProvider
│   ├── ViewModels/                  # MVVM (CommunityToolkit.Mvvm)
│   │   ├── MainViewModel
│   │   ├── SettingsViewModel
│   │   └── LogViewModel
│   ├── Views/
│   │   └── AirplaneOverlayWindow    # Transparent animated overlay
│   ├── Converters/
│   │   ├── BoolToVisibilityConverter
│   │   └── InvertedBoolToVisibilityConverter
│   ├── Package.appxmanifest         # MSIX manifest
│   ├── MainWindow.xaml              # Tabbed main UI
│   └── App.xaml                     # Application root
└── MeetingReminder.Tests/           # xUnit tests
    ├── CalendarPollerTests.cs
    └── ConfigServiceTests.cs
```

---

## How It Works

- **System tray app** — `H.NotifyIcon` for the tray icon, main window hides to
  tray on close/minimise
- **Calendar access** — two providers, selectable from the Upcoming tab:
  - **Windows Calendar**: `Windows.ApplicationModel.Appointments` (WinRT),
    reads any connected account
  - **Google Calendar**: `Google.Apis.Calendar.v3` via OAuth2 browser flow,
    tokens cached to disk for silent restore
- **Polling** — every 60 seconds, fetches the next hour of events; an in-memory
  `HashSet` prevents firing the same alert twice
- **The cat** — a borderless, transparent `Window` with `AllowsTransparency`
  and `Topmost=True`. A `DoubleAnimation` slides the cat mascot + banner from
  off-left to off-right, fading out at the end
- **Config** — JSON at `%LOCALAPPDATA%\MeetingReminder\config.json`, atomic
  writes (tmp + rename), corrupt-file recovery with `.bak`
- **Logging** — rolling daily log files in `%LOCALAPPDATA%\MeetingReminder\logs\`,
  auto-pruned after 30 days, viewable in the in-app Log tab
- **MSIX packaging** — self-signed cert, procedural asset generation, full
  pipeline from `build.bat msix`

---

## Theme: Catppuccin

All colours come from the [Catppuccin palette](https://github.com/catppuccin/palette).
No hex codes are used in components — everything references `DynamicResource`
brushes defined in `Mocha.xaml` / `Latte.xaml`.

| Role            | Mocha default | Latte default |
|-----------------|---------------|---------------|
| Primary action  | Mauve         | Blue          |
| Page background | Base          | Base          |
| Borders         | Overlay1      | Overlay1      |
| Headings        | Text          | Text          |
| Body text       | Subtext1      | Subtext1      |

---

## Credits

- **[meeting-reminder](https://github.com/conniexu444/meeting-reminder)** by
  [conniexu444](https://github.com/conniexu444) — the original macOS app this
  project is a Windows port of. The core concept (calendar polling, flying
  banner animation, menu-bar UX) comes from that project.
- **[Catppuccin](https://github.com/catppuccin/catppuccin)** — the soothing
  pastel colour palette used throughout the UI. All Mocha and Latte hex values
  come from the official [Catppuccin palette](https://github.com/catppuccin/palette).

---

## License

MIT — do what you want.
