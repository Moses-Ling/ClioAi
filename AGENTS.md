# Repository Guidelines

## Project Structure & Module Organization
- Root WPF app (net48): `App.xaml`, `MainWindow.xaml(.cs)`, `SettingsWindow.xaml(.cs)`.
- Services: `Services/` — audio capture/mixing (`AudioCaptureService`), transcription (`TranscriptionService`), chat cleanup/summarize (`OpenAiChatService`).
- Models: `Models/` — DTOs (e.g., `WhisperResponse`, `AudioDeviceModel`).
- Properties: `Properties/` — .settings, resources, assembly info.
- Tests: `tests/` — MSTest project `AudioTranscriptionApp.Tests.csproj`.
- Assets: icons/images in repo root. Solution: `AudioTranscriptionApp.sln`.

## Build, Test, and Development Commands
- Build (Visual Studio): open `AudioTranscriptionApp.sln`, Restore NuGet, Build.
- Build (CLI, Developer Command Prompt):
  - `msbuild AudioTranscriptionApp.sln /t:Build /p:Configuration=Debug`
- Run: `bin\Debug\ClioAi.exe` after build (WPF UI starts).
- Tests (Visual Studio Test Explorer) or CLI:
  - `dotnet test tests/AudioTranscriptionApp.Tests.csproj` (requires .NET SDK + .NET Framework 4.8 installed).

## Coding Style & Naming Conventions
- C#: 4‑space indentation; UTF‑8; one class per file named after the class.
- Naming: Classes/Methods/Properties PascalCase; private fields `_camelCase`; events PascalCase; async methods suffix `Async`.
- XAML: Controls named PascalCase; event handlers `Name_Event` (e.g., `StartButton_Click`).
- Logging: use `Logger` (no secrets). Avoid UI in helpers/services.

## Testing Guidelines
- Framework: MSTest (`[TestClass]`, `[TestMethod]`). Test project under `tests/`.
- Naming: `ClassNameTests`, methods `Method_Scenario_Expected`.
- Scope: unit-test services (HTTP via mocked `HttpMessageHandler`); avoid network/UI popups.
- Run locally with `dotnet test` or VS Test Explorer.

## Commit & Pull Request Guidelines
- Commits: short, imperative subject (<=72 chars), body explains why; group related changes.
- PRs: clear description, linked issues, test plan, and screenshots/GIFs for UI changes.
- Keep PRs focused; include notes on user settings/schema changes.

## Security & Configuration Tips
- API keys are stored per‑user via DPAPI (`EncryptionHelper`) and edited in the Settings window. Never commit keys.
- Default save path: `Documents/AudioTranscriptions` (session timestamped). Logs: `app.log` next to the executable.

## Architecture Overview
- UI in WPF windows; core logic in services: capture/mix audio (NAudio), send to Whisper, cleanup/summarize via OpenAI Chat; autosave and basic retries.
- Prefer adding new logic in services/VMs to keep UI thin; consider MVVM for new features.

