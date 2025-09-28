# Repository Guidelines

## Project Structure & Module Organization
- Root WPF app (net48): `App.xaml`, `MainWindow.xaml(.cs)`, `SettingsWindow.xaml(.cs)`.
- Services: `Services/` — audio capture/mixing (`AudioCaptureService`), transcription (`TranscriptionService`), chat cleanup/summarize (`OpenAiChatService`).
- Models: `Models/` — DTOs (e.g., `WhisperResponse`, `AudioDeviceModel`).
- Properties: `Properties/` — .settings, resources, assembly info.
- Tests: `tests/` — MSTest project `AudioTranscriptionApp.Tests.csproj`.
- Assets: icons/images in repo root. Solution: `AudioTranscriptionApp.sln`.

## Build, Test, and Development Commands
- Shell preference: Use PowerShell for all commands on Windows. When running through the Codex CLI/agent, default to PowerShell rather than Bash.
- Build (Visual Studio): open `AudioTranscriptionApp.sln`, Restore NuGet, Build.
- Build (CLI, Developer Command Prompt):
  - `msbuild AudioTranscriptionApp.sln /t:Build /p:Configuration=Debug`
  - msbuild location 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
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

## Coding Standard 
- After making changes, ALWAYS make sure to start up a new server so I can test it.
- Always look for existing code to iterate on instead of creating new code.
- Do not drastically change the patterns before trying to iterate on existing patterns.
- Always kill all existing related servers that may have been created in previous testing before trying to start a new server.
- Always prefer simple solutions
- Avoid duplication of code whenever possible, which means checking for other areas of the codebase that might already have similar code and functionality
- Write code that takes into account the different environments: dev, test, and prod
- You are careful to only make changes that are requested or you are confident are well understood and related to the change being requested
- When fixing an issue or bug, do not introduce a new pattern or technology without first exhausting all options for the existing implementation. And if you finally do this, make sure to remove the old implementation afterwards so we don't have duplicate logic.
- Keep the codebase very clean and organized
- Avoid writing scripts in files if possible, especially if the script is likely only to be run once
- Avoid having files over 200-300 lines of code. Refactor at that point.
- Mocking data is only needed for tests, never mock data for dev or prod
- Never add stubbing or fake data patterns to code that affects the dev or prod environments
- Never overwrite my .env file without first asking and confirming
- Focus on the areas of code relevant to the task
- Do not touch code that is unrelated to the task
- Write thorough tests for all major functionality
- Avoid making major changes to the patterns and architecture of how a feature works, after it has shown to work well, unless explicitly instructed
- Always think about what other methods and areas of code might be affected by code changes
