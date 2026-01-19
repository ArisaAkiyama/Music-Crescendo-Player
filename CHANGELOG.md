# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0-beta.2] - 2026-01-19

### Added
- **Auto Music Folder Sync**: Automatically monitors `Music` folder (System and Project local) for changes in real-time.
- **Smart Playlist Matching**: Songs added to folders (e.g., `.../Rock/song.mp3`) are automatically added to playlists with matching names (e.g., "Rock").
- **Bluetooth & SMTC Support**: Full integration with Windows System Media Transport Controls. Control playback (Play/Pause/Next/Prev) via Bluetooth headsets and keyboard media keys, even when the app is minimized.
- **Multi-Format Support**: Extended audio support to `.m4a`, `.wav`, `.flac`, `.wma`, `.aac`, and `.ogg`.
- **File Associations**: Installer now registers Crescendo as a handler for all supported audio formats (Open with...).

### Changed
- **Installer**: Switched to **Self-Contained** deployment. The installer now bundles the .NET 8 Runtime, removing the need for users to install it separately.
- **UI**: Improved Sidebar interaction with collapsible "My Tracks" section.

### Fixed
- Fixed an issue where the installer required a separate .NET Runtime installation.
- Fixed file locking issues during folder sync by implementing robust debounce logic (1.5s delay).
- Fixed Playlist Song Count UI not updating automatically when songs are added via background folder watch.
