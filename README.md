# Screen Recorder

[![CI](https://github.com/rafisgithub/screen-recorder/actions/workflows/ci.yml/badge.svg)](https://github.com/rafisgithub/screen-recorder/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/rafisgithub/screen-recorder)](https://github.com/rafisgithub/screen-recorder/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A professional Windows screen recorder built for high-quality YouTube content:
**1080p / 60 FPS**, hardware-accelerated H.264/HEVC encoding, system + microphone
audio, and a clean modern WPF interface.

## Features

- Windows Graphics Capture at up to 1080p/60fps, with GPU frames kept on the
  GPU as long as possible
- Hardware-accelerated H.264/HEVC encoding via NVENC, Quick Sync, or AMF,
  with automatic fallback to `libx264`
- System audio (WASAPI loopback) + microphone capture, mixed into the output
- Faststart MP4 output, ready to upload
- JSON-backed settings with pause/resume recording control

## Tech stack

| Concern              | Technology                                              |
|----------------------|---------------------------------------------------------|
| Language / runtime   | C# 12 on .NET 8                                          |
| UI                   | WPF + MVVM                                               |
| Video capture        | Windows Graphics Capture API (`Windows.Graphics.Capture`) |
| System audio         | WASAPI **loopback** (via NAudio)                        |
| Microphone           | WASAPI capture (via NAudio)                             |
| Encoding / muxing    | FFmpeg (`FFmpeg.AutoGen`)                               |
| HW acceleration      | NVIDIA **NVENC**, Intel **Quick Sync**, AMD **AMF**     |
| Container / codecs   | MP4 (H.264 / HEVC video, **AAC** audio)                 |
| Composition / DI     | `Microsoft.Extensions.Hosting` + DI container           |
| Logging              | Serilog (console + rolling file)                        |
| Tests                | xUnit + Moq + FluentAssertions                          |

## Solution layout

```
ScreenRecorder.sln
├─ src/
│  ├─ ScreenRecorder.Core            (net8.0, no deps)  — models, enums, interfaces
│  ├─ ScreenRecorder.Infrastructure  (net8.0-windows)   — capture / audio / encode impls
│  └─ ScreenRecorder.App             (WPF)              — MVVM UI + composition root
└─ tests/
   └─ ScreenRecorder.Tests           (xUnit)            — unit tests
```

The dependency direction is strictly one-way:
`App → Infrastructure → Core` and `App → Core`. Core depends on nothing,
which keeps the orchestration and view-model logic testable in isolation.

See [ARCHITECTURE.md](ARCHITECTURE.md) for the full design and the
module-by-module implementation roadmap.

## Prerequisites

- Windows 10 build 19041+ or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- FFmpeg shared libraries on the `PATH` (or beside the executable)

## Install

```powershell
irm https://raw.githubusercontent.com/rafisgithub/screen-recorder/main/install.ps1 | iex
```

Uninstall any time:

```powershell
irm https://raw.githubusercontent.com/rafisgithub/screen-recorder/main/uninstall.ps1 | iex
```

Prefer to do it by hand? Grab the MSI from the [latest release](https://github.com/rafisgithub/screen-recorder/releases/latest) and run it.

## Build & run

```powershell
dotnet restore
dotnet build -c Release
dotnet run --project src/ScreenRecorder.App
```

## Status

✅ **v1.0.0 released.** The full capture → encode → mux pipeline, WASAPI
audio, hardware encoder selection, orchestration, and the MSI installer are
all in place — see the roadmap in [ARCHITECTURE.md](ARCHITECTURE.md) and
[CHANGELOG.md](CHANGELOG.md) for what shipped in each release.

## Contributing

Contributions are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md) for the
workflow, coding conventions, and how to run the test suite. Please also
read the [Code of Conduct](CODE_OF_CONDUCT.md).

## Security

Found a vulnerability? Please report it privately per [SECURITY.md](SECURITY.md)
rather than opening a public issue.

## License

MIT — see [LICENSE](LICENSE). Note that FFmpeg is a separate runtime
dependency distributed under its own license (LGPL or GPL, depending on the
build you install); this project does not bundle or statically link it.
