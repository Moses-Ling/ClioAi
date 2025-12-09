# Audio Transcription App

A WPF desktop application that captures audio from Windows devices and transcribes it using OpenAI's Whisper API.

## Features

- **Audio Capture**: Records system audio using WASAPI loopback capture
- **System Audio Capture**: Records audio output from any selected Windows audio device
- **Real-time Transcription**: Processes audio in 10-second chunks for continuous transcription
- **Audio Visualization**: Includes real-time audio level monitoring
- **Flexible Device Selection**: Choose from available audio output devices
- **Transcription Storage**: Save transcriptions to text files

## Requirements

- Windows operating system
- .NET Framework 4.8
- OpenAI API key for Whisper transcription

## Build Setup (CLI or VS)

You can build with Visual Studio or from the command line. For CLI builds on .NET Framework 4.8 WPF, MSBuild from Visual Studio Build Tools is required.

Recommended options:

### Shell/Terminal
- On Windows, run all commands from PowerShell (not Bash). This repo’s examples and scripts assume PowerShell.
- When using an AI assistant or automation, default to PowerShell for shell commands.

1) Visual Studio 2022 / Developer Command Prompt
- Open the solution `AudioTranscriptionApp.sln` in VS and Build, or
- Launch "Developer Command Prompt for VS 2022" and run:
  - `msbuild AudioTranscriptionApp.sln /t:Build /p:Configuration=Debug`

2) Repo MSBuild Wrapper (no PATH setup required)
- Use the included wrapper that locates MSBuild automatically:
  - `scripts\msbuild.cmd AudioTranscriptionApp.sln /t:Build /p:Configuration=Debug`
- Examples:
  - Debug: `scripts\msbuild.cmd AudioTranscriptionApp.csproj /t:Clean;Build /p:Configuration=Debug`
  - Release: `scripts\msbuild.cmd AudioTranscriptionApp.csproj /t:Clean;Build /p:Configuration=Release`

2b) One-liner Build Script
- Use `scripts\build.cmd` to build the solution quickly:
  - Default Debug: `scripts\build.cmd`
  - Release: `scripts\build.cmd Release`
  - Clean + Build Debug: `scripts\build.cmd Debug Clean`

3) Global Setup (optional, one-time)
- Install Visual Studio 2022 Build Tools components:
  - MSBuild, .NET Desktop Build Tools, .NET Framework 4.8 SDK + Targeting Pack.
- Add to PATH (recommended):
  - `C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin` (or `...\amd64`)
- Or set a global variable for scripts that respect it:
  - `MSBUILD_EXE_PATH=C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe`
  - Open a new terminal after changing PATH/environment variables.

## Cleanup

- Quick cleanup: `scripts\clean.cmd` removes local `bin/`, `obj/`, `.vs/`, `tests/bin`, `tests/obj`, and `*.log` files.
- Docs moved to `docs/`: PRDs, plans, manuals, and notes now live under the `docs/` folder to keep the root tidy. `README.md` and `AGENTS.md` remain at the root.

## Test

- Run tests (requires .NET SDK and .NET Framework 4.8 targeting pack):
  - `dotnet test tests/AudioTranscriptionApp.Tests.csproj`

## Dependencies

- NAudio 2.2.1 for audio processing
- Newtonsoft.Json 13.0.3 for JSON serialization
- Microsoft.Win32.Registry 4.7.0 (used by NAudio)

## Setup

1. Clone this repository
2. Open the solution in Visual Studio
3. Restore NuGet packages
4. Build and run the application
5. Enter your OpenAI API key in the application
6. Select an audio device from the dropdown
7. Click "Start Recording" to begin capturing and transcribing audio

## Troubleshooting

- `msbuild` not found: use `scripts\msbuild.cmd ...` or open the VS Developer Command Prompt.
- Missing reference assemblies (net48): ensure the .NET Framework 4.8 SDK and Targeting Pack are installed via VS Build Tools.
- After installing Build Tools or updating PATH, restart your terminal.

## Settings (v1.4)

- Cloud/Local per service:
  - Transcription can use Local or Cloud independently of Cleanup/Summarize.
  - Cleanup and Summarize can each be set to Cloud or Local individually.
- Local mode configuration:
  - Host textbox includes protocol and port (e.g., `http://localhost:1234`).
  - Path textbox must start with `/` (e.g., `/v1/audio/transcriptions`, `/v1/chat/completions`).
  - Model textbox is free text (defaults: `whisper-base` for Transcription; `granite-3.1-8b-instruct` for Cleanup/Summarize).
  - No API key is required or sent in Local mode.
- Cloud mode:
  - Behavior unchanged; uses OpenAI endpoints and your configured API keys.
- Test buttons (Local only):
  - Press "Test" to send `GET {Host}/v1/models` with a 30s timeout to verify connectivity.
  - Success/failure is shown via a dialog; no models are auto-populated.
- Export footer:
  - When enabled (default), appends to Summary exports only.
  - MD/HTML: bold, larger “ClioAi” line + link; TXT: plain text.

## License

MIT
