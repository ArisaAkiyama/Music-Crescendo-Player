# Music Crescendo Player

A modern, elegant, and lightweight desktop music player built for Windows. **Crescendo** combines a premium visual aesthetic with powerful library management, delivering a seamless listening experience.

![Crescendo Main View](Assets/Screenshot%202026-01-17%20185355.png)
*Experience your music in a beautifully crafted dark interface.*

## Gallery

<p float="left" align="center">
  <img src="Assets/Screenshot%202026-01-17%20185414.png" width="45%" alt="Playlist View" />
  <img src="Assets/Screenshot%202026-01-15%20221909.png" width="45%" alt="Mini Player & Settings" /> 
</p>

## Features

### 🎨 Visual & Aesthetic
- **Premium Radial Gradient Theme**: Elegant "Top Glow" effect with a curated palette (Sky Blue to Deep Blue-Grey).
- **Glass Interface**: Transparent navigation and footer with frosted glass components.
- **Dynamic Animations**: 
  - Vinyl record spin (works with or without cover art).
  - Smooth 360° button transitions.
  - Interactive hover hints (e.g., Search shortcut hint).

### 🎵 Playback & Control
- **Smart Library & Folder Sync**: 
  - **Auto-Sync**: Automatically detects new music files in your Music folder (System or Project).
  - **Smart Playlist Matching**: Drag-and-drop a folder (e.g., "K-Pop") and if a matching playlist exists, songs are auto-added there.
- **Bluetooth & Media Keys**: Full support for Play/Pause, Next/Prev via Bluetooth headphones and keyboard media keys, even when minimized (SMTC Integration).
- **Multi-Format Support**: Plays `.mp3`, `.m4a`, `.wav`, `.flac`, `.wma`, `.aac`, `.ogg`.
- **Robust Audio Engine**: Powered by NAudio with support for precise **Seek-on-Click** and VBR MP3 compatibility.
- **Queue System**: Manage upcoming tracks with a dynamic play queue modal.

### 🛠️ Utilities
- **Installer & File Associations**: Automatic registration for audio files (Open with Crescendo).
- **Global Media Controls**: Native Windows Media Transport integration shows song info in the volume overlay.
- **Search**: Real-time filtering across your entire music library (Shortcut: `Ctrl + F`).
- **Drag & Drop**: Seamlessly import folders or files to immediate play.

## Tech Stack

- **Framework**: .NET 8 (WPF)
- **Language**: C#
- **Audio Engine**: NAudio
- **Metadata**: TagLib#
- **Icons**: Segoe MDL2 Assets
- **Database**: SQLite (for playlists and history persistence)
- **Installer**: Inno Setup (Self-contained, includes .NET Runtime)

## Installation

1. Go to the [Releases](https://github.com/ArisaAkiyama/Music-Crescendo-Player/releases) page.
2. Download the latest `v1.0.0-beta.2`.
3. Run `Crescendo_Setup_v1.0.0-beta.2.exe`. The installer will automatically register file associations.
4. Enjoy your music!

See the [CHANGELOG](CHANGELOG.md) for a detailed history of changes.

## Building from Source

Requirements:
- Visual Studio 2022
- .NET 8 SDK
- Inno Setup 6 (for building installer)

1. Clone repository:
   ```bash
   git clone https://github.com/ArisaAkiyama/Music-Crescendo-Player.git
   ```
2. Open `DesktopMusicPlayer.sln` in Visual Studio.
3. Restore NuGet packages.
4. Build and Run (`Ctrl+F5`).

## Contributing

Contributions are welcome! Feel free to submit a Pull Request or open an Issue for bugs/feature requests.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---
*Built with love for music lovers.*
