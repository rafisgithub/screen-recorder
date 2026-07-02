# Contributing

Thanks for your interest in improving Screen Recorder. This project
follows a fairly standard fork-and-PR workflow.

## Getting set up

- Windows 10 build 19041+ or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- FFmpeg 7.x shared libraries — `.\tools\Get-FFmpeg.ps1` downloads them and
  the build copies them beside the executable

```powershell
git clone https://github.com/rafisgithub/screen-recorder.git
cd screen-recorder
.\tools\Get-FFmpeg.ps1
dotnet restore
dotnet build -c Release
dotnet test
```

## Project layout

See [ARCHITECTURE.md](ARCHITECTURE.md) for the full module-by-module design.
In short:

```
App → Infrastructure → Core
```

`ScreenRecorder.Core` has no dependencies and holds models/interfaces.
`ScreenRecorder.Infrastructure` holds the concrete capture/audio/encode
implementations. `ScreenRecorder.App` is the WPF UI and composition root.
Keep that dependency direction — don't add references that point the
other way.

## Making a change

1. Open an issue first for anything beyond a small fix, so we can agree on
   the approach before you invest time in it.
2. Create a branch off `main`.
3. Keep the change focused — one concern per PR.
4. Add or update unit tests in `tests/ScreenRecorder.Tests` for any logic
   change. Code that depends on real display capture, GPU encoding, or
   native FFmpeg libraries can't be exercised in CI — isolate the testable
   logic behind an interface the way the existing services do, and mock the
   hardware-facing boundary.
5. Run `dotnet build` and `dotnet test` locally before opening the PR.
6. Open a PR against `main` and fill in the PR template.

## Coding conventions

- Nullable reference types and implicit usings are enabled solution-wide —
  don't suppress them per-file.
- `EnforceCodeStyleInBuild` is on; run `dotnet format` if the build flags
  style issues.
- Prefer constructor injection and interfaces at hardware/OS boundaries
  (capture, audio, encoding) so the logic above them stays unit-testable.

## Reporting bugs / requesting features

Use the issue templates. For bugs, include your Windows build number, GPU
vendor (NVIDIA/Intel/AMD), and the relevant log lines from the rolling
Serilog output.

## Security issues

Please don't open a public issue for security vulnerabilities — see
[SECURITY.md](SECURITY.md) instead.
