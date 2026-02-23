# SteamPipeGUI Linux

A native Linux GUI for uploading games to Steam via SteamPipe (steamcmd).  
Built with Unity + UI Toolkit.

![Platform](https://img.shields.io/badge/platform-Linux-blue)
![Unity](https://img.shields.io/badge/Unity-2022.3%2B-black)
![License](https://img.shields.io/badge/license-MIT-green)

---

<img width="955" height="734" alt="image_2026-02-23_16-01-15" src="https://github.com/user-attachments/assets/da364fdd-e808-469c-86dc-199f4b292308" />


## Features

- Login to Steam with Mobile Authenticator support
- Build and upload depots via steamcmd
- Auto-detects Steamworks SDK bundled with the release
- Saves config between sessions (`~/.config/SteamPipeGUI/config.json`)
- No installation required — just unzip and run

---

## Requirements

- Linux x86_64
- A Steam account with developer access to the app you want to upload
- Steamworks partner account (to access Steam developer tools)

---

## Installation

Download the latest release — it already includes the Steamworks SDK.  
Just unzip and run `SteamPipeGUI.x86_64`.

```
SteamPipeGUI-Linux/
├── SteamPipeGUI.x86_64      ← run this
├── SteamPipeGUI_Data/
└── steamworks_sdk_163/      ← SDK already bundled
```

No additional setup needed. The app finds `steamcmd.sh` automatically.  
If it doesn't, set the path manually in **Settings**.

---

## Usage

### Login
Enter your Steam username and password. If your account uses Steam Mobile Authenticator, confirm the login in the Steam app on your phone when prompted.

### Build & Upload
- Enter your **App ID** and **Depot ID** (usually App ID + 1)
- Select the **content folder** to upload
- Choose a **branch** (default, beta, etc.)
- Click **Start Build**

### Settings
If steamcmd is not found automatically, set the **SDK folder** path or the direct path to `steamcmd.sh`.

---

## Building from Source

- Unity 2022.3 LTS or newer
- Package: `com.unity.nuget.newtonsoft-json` (add via Package Manager → Add by name)
- Target platform: Linux x86_64

> ⚠️ Do **not** place the Steamworks SDK inside the `Assets` folder — Unity will treat the `.so` files as plugins and fail to build. Keep it next to the built executable only.

---

## License

MIT
