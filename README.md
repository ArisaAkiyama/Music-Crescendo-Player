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
- **Smart Library & Playlists**: Organize local music, manage "Liked Songs", and create custom playlists.
- **Robust Audio Engine**: Powered by NAudio with support for precise **Seek-on-Click** and VBR MP3 compatibility.
- **USB/Flashdrive Support**: Import and play music directly from external drives.
- **Queue System**: Manage upcoming tracks with a dynamic play queue modal.

### 🛠️ Utilities
- **Global Media Controls**: Native Windows Media Transport integration.
- **Search**: Real-time filtering across your entire music library (Shortcut: `Ctrl + F`).
- **Drag & Drop**: Seamlessly import folders or files.

## Tech Stack

- **Framework**: .NET 8 (WPF)
- **Language**: C#
- **Audio Engine**: NAudio
- **Metadata**: TagLib#
- **Icons**: Segoe MDL2 Assets
- **Database**: SQLite (for playlists and history persistence)

## Installation

1. Go to the [Releases](https://github.com/ArisaAkiyama/Music-Crescendo-Player/releases) page.
2. Download the latest `v1.0.0-beta.2` (or latest stable).
3. Run the installer and follow the prompts.

## Building from Source

Requirements:
- Visual Studio 2022
- .NET 8 SDK

1. Clone functionality repository:
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
