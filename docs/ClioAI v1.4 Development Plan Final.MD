# ClioAI v1.4 Development Plan — Final

This document captures the finalized scope and decisions agreed for v1.4.

## Project Overview

**Objective**: Add Local/Cloud switching for Transcription, Cleanup, and Summarize services, plus a branded export footer for summaries, while keeping existing Cloud behavior unchanged.

**Key Points Agreed**
- Protocols: Local uses `http`; Cloud uses `https` (unchanged).
- Endpoints: All service calls are POST. The only GET is `/v1/models` used solely by the Local “Test connection” button and not exposed in settings.
- Local is OpenAI-API compatible for paths and payloads; only host/port differ.
- UI: Add Cloud/Local mode controls per service with textboxes for Host, Path, and Model; disable (gray) unselected mode fields; do not hide.
- Footer: Append to Summary exports only. Text: “ClioAi Capturing Conversation, preserving privacy” (with bold “ClioAi” and larger in MD/HTML), then website on next line. Default enabled.
- Timeouts: Apply a 30s timeout only for Local “Test connection” calls. Runtime calls use existing timeouts.
- Logging: Log endpoints, statuses, and exceptions; never log API keys.

## Endpoints, Defaults, and Settings

**Cloud behavior must remain unchanged.** Local behavior is configured via new settings.

### Endpoint Matrix & Defaults

- Transcription (Whisper)
  - Local Host (default): `http://localhost:5042`
  - Local Path (POST): `/v1/audio/transcriptions`
  - Local Default Model: `whisper-base`

- Cleanup (Chat Completions)
  - Local Host (default): `http://localhost:1234`
  - Local Path (POST): `/v1/chat/completions`
  - Local Default Model: `granite-3.1-8b-instruct`

- Summarize (Chat Completions)
  - Local Host (default): `http://localhost:1234`
  - Local Path (POST): `/v1/chat/completions`
  - Local Default Model: `granite-3.1-8b-instruct`

- Test Connection (Local only)
  - Method: `GET`
  - URL: `{Host}/v1/models` (must include protocol and port, e.g., `http://localhost:1234/v1/models`)
  - Timeout: 30 seconds; single attempt; no retries
  - Not shown in settings; used internally by test buttons

### Settings Schema (Properties.Settings)

Introduce new keys per service (names to be finalized to avoid conflicts):

- Transcription
  - `TranscriptionUseLocal` (bool; default `false`)
  - `TranscriptionLocalHost` (string; default `http://localhost:5042`)
  - `TranscriptionLocalPath` (string; default `/v1/audio/transcriptions`)
  - `TranscriptionLocalModel` (string; default `whisper-base`)

- Cleanup
  - `CleanupUseLocal` (bool; default `false`)
  - `CleanupLocalHost` (string; default `http://localhost:1234`)
  - `CleanupLocalPath` (string; default `/v1/chat/completions`)
  - `CleanupLocalModel` (string; default `granite-3.1-8b-instruct`)

- Summarize
  - `SummarizeUseLocal` (bool; default `false`)
  - `SummarizeLocalHost` (string; default `http://localhost:1234`)
  - `SummarizeLocalPath` (string; default `/v1/chat/completions`)
  - `SummarizeLocalModel` (string; default `granite-3.1-8b-instruct`)

- Export
  - `ExportAppendFooter` (bool; default `true`)

Validation rules:
- `Host` includes protocol and optional port (e.g., `http://localhost:5042` or `http://127.0.0.1:1234`).
- `Path` must start with `/` and is concatenated directly to `Host` (no trimming of slashes beyond ensuring a single `/` at the start of the path).
- Local mode requires no Authorization header.

## UI/UX Plan

Keep existing tabs in `SettingsWindow`. Implement clean grouping and disable (not hide) fields for the inactive mode.

### General Tab
- Group: “Transcription”
  - Cloud: Existing API key input (unchanged)
  - Mode: Radio buttons (Cloud / Local)
  - Local: Textboxes for `Host`, `Path`, and `Model` (with defaults above)
  - Local: “Test Connection” button (calls `GET {Host}/v1/models` with 30s timeout)

- Group: “Audio”
  - System Audio Source
  - Microphone Source
  - Chunk Duration

- Group: “Other Settings”
  - Default Save Path
  - Export Footer toggle (default ON)

### Clean Up Tab
- Mirror Transcription group style with Cloud/Local radios, Local `Host`/`Path`/`Model` textboxes, and a Local “Test Connection” button.

### Summarize Tab
- Mirror Transcription group style with Cloud/Local radios, Local `Host`/`Path`/`Model` textboxes, and a Local “Test Connection” button.

## Service Routing Logic

- TranscriptionService
  - Cloud: Preserve current behavior exactly.
  - Local: Build URL as `{TranscriptionLocalHost}{TranscriptionLocalPath}`; do not add `Authorization` header; use model from `TranscriptionLocalModel`.

- OpenAiChatService (used by Cleanup and Summarize)
  - Cloud: Preserve current behavior exactly.
  - Local: Build URL as `{CleanupLocalHost}{CleanupLocalPath}` or `{SummarizeLocalHost}{SummarizeLocalPath}` accordingly; no `Authorization` header; use corresponding `...LocalModel`.

## Connectivity Tests (Local only)

- Implement a “Test Connection” button in each Local group that:
  - Constructs `GET {Host}/v1/models` (example: `http://localhost:1234/v1/models`).
  - Uses a 30-second timeout; single attempt, no retries.
  - Shows a concise success/failure dialog. No model fetching/populating into the UI.
  - Logs full URL, status code, and exceptions (no secrets).

## Export Footer (Summaries only)

- Append footer only to Summary exports (both Cloud and Local).
- Content:
  - Line 1: “ClioAi Capturing Conversation, preserving privacy”
    - In MD/HTML: render “ClioAi” in bold and larger text.
    - In TXT: plain text.
  - Line 2: `http://ClioAi.app`
- Default: `ExportAppendFooter = true`.

## Quality Assurance

### Unit Tests (MSTest)
- Service routing switches correctly based on `UseLocal` flags (no auth in Local; Cloud unchanged).
- Settings persistence for new keys (defaults, save/load).
- UI enabling/disabling logic for mode toggles.
- Connectivity test helper respects 30s timeout and surfaces success/failure without retries.
- Footer appended only to Summary exports and formatted correctly per type.

### Integration Tests
- Mock Local endpoints with `HttpMessageHandler` for POST routes and `GET /v1/models` to validate connection testing behavior.
- Validate end-to-end flow remains unchanged for Cloud mode.

## Error Handling & Logging
- User-facing: concise, actionable error messages (e.g., “Connection failed: timed out after 30s”).
- Logging: log full URLs (host, port, path), status codes, and exception details/stack traces; never log API keys.

## Version Control Workflow

- Create and work on branches per phase:
  1. `V1.4.1` — UI & settings
  2. `V1.4.2` — Service routing
  3. `V1.4.3` — Connectivity tests
  4. `V1.4.4` — Export footer
- Commits: short, imperative messages; group related changes by feature/phase; include tests where applicable.

## Phase-Based Execution Plan

### Phase 1 — UI & Settings (V1.4.1)
- Add Cloud/Local radios and Local textboxes (Host, Path, Model) for Transcription (General), Cleanup, and Summarize tabs.
- Implement disabling of unselected mode fields.
- Add “Test Connection” buttons (wiring added in Phase 3).
- Add Export Footer toggle in “Other Settings” (default ON).
- Add new `Properties.Settings` keys with defaults; wire up persistence.

Acceptance Criteria
- Mode toggles enable/disable the right fields.
- New settings persist across app restarts with defaults applied.
- Clean grouping: Transcription, Audio, Other Settings on General.

### Phase 2 — Service Routing (V1.4.2)
- Implement Local routing in `TranscriptionService` and `OpenAiChatService` per settings.
- Ensure no `Authorization` header for Local; Cloud untouched.
- Keep request/response shapes identical to existing Cloud code paths.

Acceptance Criteria
- Services route correctly to Local or Cloud.
- Cloud behavior remains unchanged.
- Local requests POST to configured `Host + Path` with configured `Model`.

### Phase 3 — Connectivity Tests (V1.4.3)
- Implement Local test buttons to call `GET {Host}/v1/models`.
- Enforce 30s timeout, no retries; display success/failure dialogs.
- Log outcomes without secrets.

Acceptance Criteria
- Each Local test button reports success/failure based on `/v1/models`.
- Timeouts after 30s are handled gracefully.

### Phase 4 — Export Footer (V1.4.4)
- Append footer to all Summary exports (TXT/MD/HTML) when enabled.
- Format “ClioAi” bold and larger in MD/HTML; plain in TXT.

Acceptance Criteria
- Footer appears only for Summary exports.
- Footer default is enabled and matches the specified content and formatting.

## Pre-Release Checklist
- All unit tests passing.
- Manual verification of Cloud flows unchanged.
- Local routing tested for each service.
- Local test buttons validated with 30s timeout.
- Summary exports include footer per format.
- Documentation updated (README/settings description if applicable).

---

This final plan supersedes earlier drafts and reflects the agreed decisions for v1.4, emphasizing minimal disruption to existing Cloud behavior and a clean, user-friendly Local configuration experience.

