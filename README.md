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
