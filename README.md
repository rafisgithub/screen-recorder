# YouTube Screen Recorder

A professional Windows screen recorder built for high-quality YouTube content:
**1080p / 60 FPS**, hardware-accelerated H.264/HEVC encoding, system + microphone
audio, and a clean modern WPF interface.

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
- FFmpeg shared libraries on the `PATH` (or beside the executable) — wired up
  in the encoding milestone.

## Build & run

```powershell
dotnet restore
dotnet build -c Release
dotnet run --project src/ScreenRecorder.App
```

## Status

🚧 **Scaffolding complete.** The full project structure, contracts, models,
dependency injection, and UI are in place. Concrete capture/audio/encoding
services are stubbed and implemented one module at a time — see the roadmap in
[ARCHITECTURE.md](ARCHITECTURE.md).
