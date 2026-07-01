# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

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

[Unreleased]: https://github.com/rafisgithub/screen-recorder/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/rafisgithub/screen-recorder/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/rafisgithub/screen-recorder/releases/tag/v1.0.0
