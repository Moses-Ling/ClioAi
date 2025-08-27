# GEMINI.md

## Project Overview

This is a C# WPF desktop application for Windows that transcribes audio in real-time. It captures audio from both system output and a microphone, mixes them, and sends 10-second chunks to the OpenAI Whisper API for transcription. The application also provides features for cleaning up and summarizing the transcription using an AI chat model.

**Key Technologies:**

*   **GUI:** Windows Presentation Foundation (WPF)
*   **Audio Processing:** NAudio library
*   **API Communication:** HttpClient for REST API calls to OpenAI
*   **JSON Serialization:** Newtonsoft.Json
*   **Language:** C#
*   **Framework:** .NET Framework 4.8

**Architecture:**

The application follows a Model-View-ViewModel (MVVM) like pattern, with a clear separation between the UI (XAML files), UI logic (`MainWindow.xaml.cs`), and backend services.

*   **`MainWindow.xaml.cs`**: The main UI logic, handling user interactions and coordinating the services.
*   **`AudioCaptureService.cs`**: Manages audio capture from system and microphone using NAudio. It mixes the audio, creates chunks, and sends them for transcription.
*   **`TranscriptionService.cs`**: Handles communication with the OpenAI Whisper API for audio transcription.
*   **`OpenAiChatService.cs`**: Handles communication with an OpenAI chat model for cleaning up and summarizing the transcription.
*   **`SettingsWindow.xaml.cs`**: Manages application settings, including API keys and audio devices.

## Building and Running

To build and run this project:

1.  **Open the solution:** Open the `AudioTranscriptionApp.sln` file in Visual Studio.
2.  **Restore NuGet packages:** Visual Studio should automatically restore the required NuGet packages. If not, you can do it manually through the NuGet Package Manager.
3.  **Build the solution:** Build the solution by pressing `Ctrl+Shift+B` or from the "Build" menu. The MSBuild.exe is located at `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`. It is recommended to build the solution after every phase of development.
4.  **Run the application:** Press `F5` to run the application in debug mode.

**Running Tests:**

The project includes a test suite using MSTest. To run the tests:

1.  Open the "Test Explorer" in Visual Studio.
2.  Click on "Run All Tests".

## Development Conventions

*   **Coding Style:** The code follows standard C# and WPF conventions.
*   **Logging:** The application uses a simple `Logger` class to log information, warnings, and errors to a file.
*   **Error Handling:** The application includes error handling for API calls and other potential issues.
*   **Settings:** Application settings, such as API keys and selected audio devices, are stored using the `Properties.Settings` mechanism.
*   **Dependencies:** NuGet packages are managed through `packages.config`.