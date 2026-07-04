# Architecture

## Goals

- **Separation of concerns** — capture, audio, encoding, orchestration, and UI
  are independent and swappable behind interfaces.
- **Testability** — all policy/logic lives behind `ScreenRecorder.Core`
  contracts so it can be tested with mocks; no Windows/native types leak into
  view models.
- **Performance** — GPU frames stay on the GPU as long as possible; the encoder
  prefers hardware (NVENC/QSV/AMF) and gracefully falls back to x264.

## Layering

```
┌───────────────────────────────────────────────┐
│ ScreenRecorder.App  (WPF, MVVM)                 │
│   Views ── bind ──► ViewModels ── use ─┐        │
│   Composition root builds the IHost    │        │
└─────────────────────────────────────────┼───────┘
                                          │ (Core interfaces only)
┌──────────────────────────────────────────▼──────┐
│ ScreenRecorder.Infrastructure                    │
│   GraphicsCaptureService     (Windows.Graphics)  │
│   WasapiLoopbackCaptureService / Microphone      │
│   FFmpegVideoEncoder / AudioEncoder / Muxer      │
│   EncoderFactory + HardwareCapabilityService     │
│   RecordingOrchestrator (wires the pipeline)     │
└──────────────────────────────────────────┬───────┘
                                           │ implements
┌───────────────────────────────────────────▼──────┐
│ ScreenRecorder.Core   (no dependencies)           │
│   Models · Enums · Events · Abstractions (I*)     │
└───────────────────────────────────────────────────┘
```

## The recording pipeline

```
 Windows Graphics Capture ─► VideoFrame ─┐
                                         ├─► IRecordingOrchestrator ─► IVideoEncoder ─┐
 WASAPI loopback (system) ─► AudioFrame ─┤                          ─► IAudioEncoder ─┼─► IMediaWriter ─► out.mp4
 WASAPI capture (mic)     ─► AudioFrame ─┘                                            ─┘
```

- **`ICaptureService`** produces `VideoFrame`s from a `CaptureTarget`
  (monitor or window) using the Windows Graphics Capture API.
- **`ISystemAudioCaptureService`** / **`IMicrophoneCaptureService`** produce
  `AudioFrame`s via WASAPI loopback and WASAPI capture respectively.
- **`IEncoderFactory`** inspects **`IHardwareCapabilityService`** and the
  requested **`RecordingSettings`** to choose the best **`EncoderDescriptor`**
  (e.g. `h264_nvenc`, `h264_qsv`, `h264_amf`, or `h264_mf`).
- **`IVideoEncoder`** / **`IAudioEncoder`** encode raw frames; **`IMediaWriter`**
  muxes the encoded packets into a faststart MP4.
- **`IRecordingOrchestrator`** owns the lifecycle (start/pause/resume/stop),
  A/V timestamp synchronization, and surfaces `RecordingState` + live
  `RecordingStatistics` to the UI.

## Implementation roadmap (incremental modules)

Each module is implemented and reviewed before the next begins.

| #  | Module                          | Key types                                                       |
|----|---------------------------------|-----------------------------------------------------------------|
| 0  | **Scaffold** ✅                 | Solution, projects, DI, models, interfaces, UI shell            |
| 1  | **Settings + capture targets** ✅ | `JsonSettingsService`, `CaptureTargetProvider`, `AudioDeviceProvider` |
| 2  | **Hardware capability detection** ✅ | `HardwareCapabilityService`, `EncoderFactory`                   |
| 3  | **Video capture** ✅            | `GraphicsCaptureService`, D3D11 interop                         |
| 4  | **Audio capture** ✅            | `WasapiLoopbackCaptureService`, `WasapiMicrophoneCaptureService`|
| 5  | **Encoding + muxing** ✅        | `FFmpegVideoEncoder`, `FFmpegAudioEncoder`, `FFmpegMuxer`       |
| 6  | **Orchestration** ✅            | `RecordingOrchestrator` (A/V sync, lifecycle)                   |
| 7  | UI polish + previews            | live preview, device meters, hotkeys                            |

## Threading model

- Graphics Capture raises frame-arrived callbacks on a pool thread; frames are
  queued to the encoder to keep the capture callback non-blocking.
- WASAPI capture runs on its own NAudio thread; audio packets are timestamped
  against a shared monotonic clock (`ISystemClock`) for A/V sync.
- The UI thread only ever touches view models; services marshal back via
  `IUiDispatcher`.

## Dependency injection

`App.OnStartup` builds a `Microsoft.Extensions.Hosting.IHost`:

- `AddInfrastructure()` registers capture/audio/encoding/orchestration services.
- `AddPresentation()` registers view models, dialog/dispatcher services, and the
  `MainWindow`.
- Serilog is attached as the logging provider (console + rolling file).
