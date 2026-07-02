# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.1.0] - 2026-07-02

### Added

- FFmpeg 7.1 (BtbN GPL shared build, pinned and SHA-256-verified) is now
  bundled in the MSI — recording works out of the box with no manual FFmpeg
  setup. The GPL license text is installed alongside the DLLs.
- Application icon (window, taskbar, Start menu, and Apps & Features).
- Apps & Features metadata: product icon, install location, and support /
  project URLs.
- `install.ps1`: skips the download when the installed version is already
  current (`-Force` reinstalls), warns before the UAC prompt, offers to
  close a running instance before upgrading, shows the download size,
  treats exit code 3010 as success with a restart note, explains
  "another installation in progress" (1618), and offers to launch the app
  when done.
- `uninstall.ps1`: closes a running instance first and clarifies that
  recordings and settings are kept.

### Changed

- Executable, install folder, and data folders renamed from
  `YouTubeScreenRecorder` to `ScreenRecorder`; existing settings and logs
  are migrated automatically on first launch.
- Installer scripts elevate via UAC instead of failing with error 1603 in
  non-elevated shells, keep the console open on failure when piped into
  `iex`, and write a verbose MSI log to `%TEMP%` for troubleshooting.

### Fixed

- One-line installer no longer exits the user's terminal on failure.
- Downloads no longer fail on Windows PowerShell 5.1 systems without
  TLS 1.2 enabled by default.

## [1.0.1] - 2026-07-02

### Changed

- Renamed the product from "YouTube Screen Recorder" to "Screen Recorder"
  throughout the UI, installer, and docs.
- Reworked installation around `install.ps1` / `uninstall.ps1` one-line
  scripts instead of a manual `msiexec` command.

### Added

- Open-source project scaffolding: `LICENSE` (MIT), `CONTRIBUTING.md`,
  `CODE_OF_CONDUCT.md`, `SECURITY.md`, issue/PR templates, and a CI workflow.

## [1.0.0] - 2026-07-01

### Added

- Core screen recording pipeline: Windows Graphics Capture → Direct3D11
  interop → FFmpeg encode → MP4 mux.
- WASAPI system-audio (loopback) and microphone capture, mixed into the
  recorded track.
- Hardware-accelerated H.264/HEVC encoding via NVIDIA NVENC, Intel Quick
  Sync, and AMD AMF, with automatic encoder selection through
  `EncoderFactory`.
- WPF UI (`MainWindow`, `SettingsViewModel`, `RecordingViewModel`) with a
  settings page backed by `JsonSettingsService`.
- `RecordingClock` for accurate elapsed-time / A-V sync tracking.
- WiX 4 MSI installer and a `win-x64` publish profile for distribution.
- Unit test suite (xUnit + Moq + FluentAssertions) covering
  `EncoderFactory`, `OutputPathService`, `JsonSettingsService`,
  `RecordingViewModel`, and `RecordingClock`.

[Unreleased]: https://github.com/rafisgithub/screen-recorder/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/rafisgithub/screen-recorder/compare/v1.0.2...v1.1.0
[1.0.1]: https://github.com/rafisgithub/screen-recorder/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/rafisgithub/screen-recorder/releases/tag/v1.0.0
