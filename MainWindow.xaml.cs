﻿using AudioTranscriptionApp.Models;
using AudioTranscriptionApp.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Markdig;
using System.Diagnostics;
using System.Windows.Media;


namespace AudioTranscriptionApp
{
    public partial class MainWindow : Window
    {
        private AudioCaptureService _audioCaptureService;
        private TranscriptionService _transcriptionService;
        private OpenAiChatService _openAiChatService;
        private bool _isRecording = false;
        private string _lastSaveDirectory = null;
        private int _audioTooShortWarningCount = 0;

        public MainWindow()
        {
            Logger.Info("Application starting.");
            InitializeComponent();

            Logger.Info("Initializing services...");
            _transcriptionService = new TranscriptionService(string.Empty);
            _openAiChatService = new OpenAiChatService(string.Empty);
            _audioCaptureService = new AudioCaptureService(_transcriptionService);

            // Set up event handlers
            Logger.Info("Setting up event handlers.");
            _audioCaptureService.SystemAudioLevelChanged += AudioCaptureService_SystemAudioLevelChanged; // Phase 3
            _audioCaptureService.MicrophoneLevelChanged += AudioCaptureService_MicrophoneLevelChanged; // Phase 3
            _audioCaptureService.TranscriptionReceived += AudioCaptureService_TranscriptionReceived;
            _audioCaptureService.StatusChanged += AudioCaptureService_StatusChanged;
            _audioCaptureService.ErrorOccurred += AudioCaptureService_ErrorOccurred;
            _audioCaptureService.RecordingTimeUpdate += AudioCaptureService_RecordingTimeUpdate;

            Logger.Info("Loading settings...");
            LoadApiKeys();

            // Add handler for auto-scrolling
            TranscriptionTextBox.TextChanged += TranscriptionTextBox_TextChanged;

            // Show instructions
            ShowInstructions();
        }

        private void LoadApiKeys()
        {
             try
            {
                // Whisper Key
                string encryptedApiKey = Properties.Settings.Default.ApiKey ?? string.Empty;
                string decryptedApiKey = EncryptionHelper.DecryptString(encryptedApiKey);
                if (!string.IsNullOrEmpty(decryptedApiKey))
                {
                     _transcriptionService.UpdateApiKey(decryptedApiKey);
                     Logger.Info("Whisper API key loaded from settings and applied to service.");
                }
                 else { Logger.Info("No Whisper API key found in settings."); }

                // Cleanup Key
                string encryptedCleanupKey = Properties.Settings.Default.CleanupApiKey ?? string.Empty;
                string decryptedCleanupKey = EncryptionHelper.DecryptString(encryptedCleanupKey);
                if (!string.IsNullOrEmpty(decryptedCleanupKey))
                {
                    _openAiChatService.UpdateApiKey(decryptedCleanupKey);
                    Logger.Info("Cleanup API key loaded from settings and applied to service.");
                }
                else { Logger.Info("No Cleanup API key found in settings."); }

                 // Summarize Key (Assuming it uses the same service instance for now)
                 string encryptedSummarizeKey = Properties.Settings.Default.SummarizeApiKey ?? string.Empty;
                 string decryptedSummarizeKey = EncryptionHelper.DecryptString(encryptedSummarizeKey);
                 if (!string.IsNullOrEmpty(decryptedSummarizeKey))
                 {
                      _openAiChatService.UpdateApiKey(decryptedSummarizeKey); // Update again if different key used
                      Logger.Info("Summarize API key loaded from settings and applied to service.");
                 }
                 else { Logger.Info("No Summarize API key found in settings."); }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load API keys on startup.", ex);
            }
        }

        private void ShowInstructions()
        {
            string instructions =
                "AUDIO TRANSCRIPTION APP INSTRUCTIONS:\n\n" +
                "1. Click 'Settings' to configure API Keys and select Audio Sources.\n" + // Updated
                "3. Click 'Start' to begin transcribing.\n" +
                "4. Click 'Stop' when finished. Transcription is saved automatically.\n" +
                "5. Click 'Clean Up' to refine the transcription using AI.\n" +
                "6. Click 'Summarize' to generate a summary using AI.\n" +
                "7. Click 'Clear' to clear the text box.\n\n" +
                "Note: Use Mute checkboxes below to control which audio goes into the recording."; // Added note

            TranscriptionTextBox.Text = instructions;
            Logger.Info("Instructions displayed.");
        }

        // --- Phase 3 Event Handlers ---
        private void AudioCaptureService_SystemAudioLevelChanged(object sender, float level)
        {
             Dispatcher.Invoke(() => { SystemLevelBar.Value = level; });
        }

        private void AudioCaptureService_MicrophoneLevelChanged(object sender, float level)
        {
             Dispatcher.Invoke(() => { MicLevelBar.Value = level; });
        }

        private void MuteMicCheckBox_Changed(object sender, RoutedEventArgs e)
        {
             _audioCaptureService?.ToggleMicrophoneMute(MuteMicCheckBox.IsChecked ?? false);
        }

        private void MuteSystemAudioCheckBox_Changed(object sender, RoutedEventArgs e)
        {
             _audioCaptureService?.ToggleSystemAudioMute(MuteSystemAudioCheckBox.IsChecked ?? false);
        }
        // --- End Phase 3 Event Handlers ---

        private void AudioCaptureService_TranscriptionReceived(object sender, string text)
        {
            Dispatcher.Invoke(() => { TranscriptionTextBox.AppendText($"{text}\n\n"); });
        }

         private void AudioCaptureService_StatusChanged(object sender, string status)
        {
            Logger.Info($"Status changed: {status}");
            Dispatcher.Invoke(() => StatusTextBlock.Text = status);
        }

         private void AudioCaptureService_RecordingTimeUpdate(object sender, TimeSpan elapsed)
        {
            Dispatcher.Invoke(() => { ElapsedTimeTextBlock.Text = $"Rec: {elapsed.TotalSeconds:F0}s"; });
        }

        private void AudioCaptureService_ErrorOccurred(object sender, Exception ex)
        {
            bool isAudioTooShortError = ex.Message != null &&
                                        ex.Message.Contains("audio_too_short"); // Simplified check

            if (isAudioTooShortError)
            {
                _audioTooShortWarningCount++;
                string warningMsg = $"Warning ({_audioTooShortWarningCount}/3): Audio too short or silent. Check audio device or speak clearly.";
                Logger.Warning($"Audio too short warning received. Count: {_audioTooShortWarningCount}. Full error: {ex.Message}");

                Dispatcher.Invoke(() =>
                {
                    TranscriptionTextBox.AppendText($"\n--- {warningMsg} ---\n");
                    StatusTextBlock.Text = warningMsg;

                    if (_audioTooShortWarningCount >= 3)
                    {
                        Logger.Warning("Stopping recording due to repeated 'audio too short' warnings.");
                        StatusTextBlock.Text = "Stopping recording: Repeated audio issues detected.";
                        StopButton_Click(null, null); // Trigger stop
                    }
                });
            }
            else
            {
                Logger.Error("Error occurred in AudioCaptureService.", ex);
                Dispatcher.Invoke(() =>
                {
                    StatusTextBlock.Text = $"Error: {ex.Message}";
                    System.Windows.MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info("StartButton clicked.");
            string encryptedKeyCheck = Properties.Settings.Default.ApiKey ?? string.Empty;
            if (string.IsNullOrEmpty(EncryptionHelper.DecryptString(encryptedKeyCheck))) // Check decrypted key
            {
                 Logger.Warning("Start recording attempted without Whisper API key configured.");
                 System.Windows.MessageBox.Show("Please configure your OpenAI API key for Whisper in the Settings window first.", "API Key Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
            }
             if (string.IsNullOrEmpty(Properties.Settings.Default.SystemAudioDeviceId) || string.IsNullOrEmpty(Properties.Settings.Default.MicrophoneDeviceId))
            {
                 Logger.Warning("Start recording attempted without audio devices selected.");
                 System.Windows.MessageBox.Show("Please select both a System Audio and Microphone source in Settings first.", "Audio Devices Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
            }


            TranscriptionTextBox.Text = string.Empty;
            _lastSaveDirectory = null;
            _audioTooShortWarningCount = 0;
            ElapsedTimeTextBlock.Text = "Rec: 0s";
            ElapsedTimeTextBlock.Visibility = Visibility.Visible;
            Logger.Info("Transcription text box cleared and warning count reset.");

            // Reset mute checkboxes on start
            MuteMicCheckBox.IsChecked = false;
            MuteSystemAudioCheckBox.IsChecked = false;

            Logger.Info("Starting recording...");
            _audioCaptureService.StartRecording(); // Service now handles initialization checks
            _isRecording = true;
            SetUiBusyState(true);
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info("StopButton clicked. Stopping recording...");
            if (!_isRecording) return; // Prevent multiple stops

            TimeSpan finalDuration = _audioCaptureService.RecordedDuration;
            _audioCaptureService.StopRecording();
            _isRecording = false;

            ElapsedTimeTextBlock.Text = $"Total: {finalDuration.TotalSeconds:F0}s";

            // --- Automatic Save Logic ---
            Dispatcher.InvokeAsync(() => // Use InvokeAsync for responsiveness
            {
                string fullTranscription = TranscriptionTextBox.Text;
                bool hasTextToSave = !string.IsNullOrWhiteSpace(fullTranscription) && !fullTranscription.StartsWith("AUDIO TRANSCRIPTION APP INSTRUCTIONS");

                if (!hasTextToSave)
                {
                    Logger.Info("No significant transcription text found to auto-save.");
                    StatusTextBlock.Text = "Recording stopped. No text to save.";
                    SetUiBusyState(false);
                    return;
                }

                try
                {
                    string baseSavePath = Properties.Settings.Default.DefaultSavePath;
                    if (string.IsNullOrEmpty(baseSavePath))
                    {
                        baseSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AudioTranscriptions");
                        Logger.Info($"DefaultSavePath not set, using default: {baseSavePath}");
                    }

                    Directory.CreateDirectory(baseSavePath); // Ensure directory exists

                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string sessionDirectory = Path.Combine(baseSavePath, timestamp);
                    Directory.CreateDirectory(sessionDirectory);
                    Logger.Info($"Created session directory: {sessionDirectory}");

                    string filePath = Path.Combine(sessionDirectory, "transcription.txt");
                    File.WriteAllText(filePath, fullTranscription);
                    _lastSaveDirectory = sessionDirectory;
                    StatusTextBlock.Text = $"Transcription automatically saved to: {sessionDirectory}";
                    Logger.Info($"Transcription automatically saved to: {filePath}");
                    SetUiBusyState(false);
                }
                catch (Exception ex)
                {
                    _lastSaveDirectory = null;
                    Logger.Error("Failed to automatically save transcription.", ex);
                    StatusTextBlock.Text = "Error saving transcription automatically.";
                    System.Windows.MessageBox.Show($"Failed to automatically save transcription: {ex.Message}", "Auto-Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    SetUiBusyState(false);
                }
            });
            // --- End Automatic Save Logic ---
        }

        private async void CleanupButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info("CleanupButton clicked.");
            ElapsedTimeTextBlock.Visibility = Visibility.Collapsed;

            if (_isRecording)
            {
                System.Windows.MessageBox.Show("Please stop recording before cleaning up text.", "Recording Active", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string originalText = TranscriptionTextBox.Text;
            if (string.IsNullOrWhiteSpace(originalText) || originalText.StartsWith("AUDIO TRANSCRIPTION APP INSTRUCTIONS"))
            {
                Logger.Warning("Cleanup attempted with no significant text.");
                System.Windows.MessageBox.Show("There is no transcription text to clean up.", "No Text", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrEmpty(_lastSaveDirectory) || !Directory.Exists(_lastSaveDirectory))
            {
                 Logger.Warning("Cleanup attempted but no valid save directory exists for this session.");
                 System.Windows.MessageBox.Show("Cannot clean up text as the automatic save directory for this session was not created or found.", "Save Directory Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                 CleanupButton.IsEnabled = false;
                 return;
            }

            string cleanupModel = Properties.Settings.Default.CleanupModel;
            string cleanupPrompt = Properties.Settings.Default.CleanupPrompt;
            string encryptedCleanupKey = Properties.Settings.Default.CleanupApiKey ?? string.Empty;
            string decryptedCleanupKey = EncryptionHelper.DecryptString(encryptedCleanupKey);

            if (string.IsNullOrEmpty(decryptedCleanupKey))
            {
                 Logger.Warning("Cleanup attempted without Cleanup API key configured.");
                 System.Windows.MessageBox.Show("Please configure your OpenAI API key for Cleanup in the Settings window first.", "API Key Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
            }
            _openAiChatService.UpdateApiKey(decryptedCleanupKey);

            SetUiBusyState(true, "Cleaning up text...");

            try
            {
                Logger.Info($"Calling OpenAI Chat API (Model: {cleanupModel}) for cleanup...");
                string cleanedText = await _openAiChatService.GetResponseAsync(cleanupPrompt, originalText, cleanupModel);

                if (cleanedText != null)
                {
                    TranscriptionTextBox.Text = cleanedText;
                    Logger.Info("Cleanup API call successful.");
                    try
                    {
                        string savePath = Path.Combine(_lastSaveDirectory, "cleaned.txt");
                        File.WriteAllText(savePath, cleanedText);
                        StatusTextBlock.Text = $"Cleanup complete. Saved cleaned.txt to: {_lastSaveDirectory}";
                         Logger.Info($"Cleaned text saved to: {savePath}");
                     }
                     catch (Exception saveEx)
                     {
                         Logger.Error($"Failed to save cleaned text to {_lastSaveDirectory}", saveEx);
                         StatusTextBlock.Text = "Cleanup complete, but failed to save cleaned.txt.";
                         System.Windows.MessageBox.Show($"Cleanup was successful, but failed to save cleaned.txt: {saveEx.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                     }
                 }
                else
                {
                    Logger.Error("Cleanup API call returned null response.");
                    StatusTextBlock.Text = "Cleanup failed: Received empty response from API.";
                    System.Windows.MessageBox.Show("Cleanup failed: Received an empty response from the API.", "Cleanup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error during cleanup process.", ex);
                StatusTextBlock.Text = $"Cleanup failed: {ex.Message}";
                System.Windows.MessageBox.Show($"Cleanup failed: {ex.Message}", "Cleanup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetUiBusyState(false);
            }
        }

        private async void SummarizeButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info("SummarizeButton clicked.");
            ElapsedTimeTextBlock.Visibility = Visibility.Collapsed;

            if (_isRecording)
            {
                System.Windows.MessageBox.Show("Please stop recording before summarizing text.", "Recording Active", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string textToSummarize = TranscriptionTextBox.Text;
            if (string.IsNullOrWhiteSpace(textToSummarize) || textToSummarize.StartsWith("AUDIO TRANSCRIPTION APP INSTRUCTIONS"))
            {
                Logger.Warning("Summarize attempted with no significant text.");
                System.Windows.MessageBox.Show("There is no text to summarize.", "No Text", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string summarizeModel = Properties.Settings.Default.SummarizeModel;
            string summarizePrompt = Properties.Settings.Default.SummarizePrompt;
            string encryptedSummarizeKey = Properties.Settings.Default.SummarizeApiKey ?? string.Empty;
            string decryptedSummarizeKey = EncryptionHelper.DecryptString(encryptedSummarizeKey);

            if (string.IsNullOrEmpty(decryptedSummarizeKey))
            {
                 Logger.Warning("Summarize attempted without Summarize API key configured.");
                 System.Windows.MessageBox.Show("Please configure your OpenAI API key for Summarize in the Settings window first.", "API Key Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
            }
            _openAiChatService.UpdateApiKey(decryptedSummarizeKey);

            SetUiBusyState(true, "Summarizing text...");

            try
            {
                Logger.Info($"Calling OpenAI Chat API (Model: {summarizeModel}) for summarization...");
                string summaryMd = await _openAiChatService.GetResponseAsync(summarizePrompt, textToSummarize, summarizeModel);

                if (summaryMd != null)
                {
                    TranscriptionTextBox.Text = summaryMd;
                    Logger.Info("Summarization API call successful.");

                    if (!string.IsNullOrEmpty(_lastSaveDirectory) && Directory.Exists(_lastSaveDirectory))
                    {
                        try
                        {
                            string savePath = Path.Combine(_lastSaveDirectory, "summary.md");
                            File.WriteAllText(savePath, summaryMd);
                            StatusTextBlock.Text = $"Summarization complete. Saved summary.md to: {_lastSaveDirectory}";
                            Logger.Info($"Summary saved to: {savePath}");
                        }
                        catch (Exception saveEx)
                        {
                            Logger.Error($"Failed to save summary.md to {_lastSaveDirectory}", saveEx);
                            StatusTextBlock.Text = "Summarization complete, but failed to save summary.md.";
                            System.Windows.MessageBox.Show($"Summarization was successful, but failed to save summary.md: {saveEx.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        StatusTextBlock.Text = "Summarization complete. Could not save summary.md (Save directory missing).";
                        Logger.Warning("Summarization complete but could not save file as _lastSaveDirectory was not set or invalid.");
                    }
                     try
                     {
                         string htmlContent = GenerateHtmlWithCopy(summaryMd);
                         string tempHtmlPath = Path.Combine(Path.GetTempPath(), $"summary_{DateTime.Now.Ticks}.html");
                         File.WriteAllText(tempHtmlPath, htmlContent);
                         Logger.Info($"Saved temporary HTML summary to: {tempHtmlPath}");

                         Process.Start(tempHtmlPath);
                         Logger.Info("Opened summary in default browser.");
                     }
                     catch(Exception browserEx)
                     {
                          Logger.Error("Failed to convert summary to HTML or open in browser.", browserEx);
                          System.Windows.MessageBox.Show($"Could not open summary in browser: {browserEx.Message}", "Browser Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                     }
                 }
                 else
                 {
                    Logger.Error("Summarization API call returned null response.");
                    StatusTextBlock.Text = "Summarization failed: Received empty response from API.";
                    System.Windows.MessageBox.Show("Summarization failed: Received an empty response from the API.", "Summarize Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error during summarization process.", ex);
                StatusTextBlock.Text = $"Summarization failed: {ex.Message}";
                System.Windows.MessageBox.Show($"Summarization failed: {ex.Message}", "Summarize Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetUiBusyState(false);
            }
        }


        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info("ClearButton clicked.");
            ElapsedTimeTextBlock.Visibility = Visibility.Collapsed;
            if (!string.IsNullOrEmpty(TranscriptionTextBox.Text))
            {
                if (System.Windows.MessageBox.Show("Are you sure you want to clear the transcription?",
                                    "Clear Transcription", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    Logger.Info("Transcription cleared by user.");
                    TranscriptionTextBox.Text = string.Empty;
                    StatusTextBlock.Text = "Transcription cleared.";
                    _lastSaveDirectory = null;
                    SetUiBusyState(false);
                }
            }
            else
            {
                StatusTextBlock.Text = "Transcription is already empty.";
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info("SettingsButton clicked.");
            var settingsWindow = new SettingsWindow { Owner = this };
            bool? result = settingsWindow.ShowDialog();
            Logger.Info($"Settings window closed with result: {result}");

            if (result == true)
            {
                Logger.Info("Reloading settings after save.");
                try
                {
                    // Reload API Keys
                    LoadApiKeys();

                    // V2: Reload selected device IDs and re-initialize service
                    string systemDeviceId = Properties.Settings.Default.SystemAudioDeviceId;
                    string micDeviceId = Properties.Settings.Default.MicrophoneDeviceId;
                    Logger.Info($"Retrieved System Audio Device ID from settings: {systemDeviceId ?? "None"}");
                    Logger.Info($"Retrieved Microphone Device ID from settings: {micDeviceId ?? "None"}");

                    // Apply devices to service instance (which triggers re-initialization)
                    _audioCaptureService.SetSystemAudioDevice(systemDeviceId);
                    _audioCaptureService.SetMicrophoneDevice(micDeviceId);

                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to reload settings or re-initialize audio service.", ex);
                    System.Windows.MessageBox.Show("Error applying settings. Please check logs.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                StatusTextBlock.Text = "Settings saved and applied.";
            }
        }

         private string GenerateHtmlWithCopy(string markdownContent)
         {
             var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
             string htmlBody = Markdown.ToHtml(markdownContent, pipeline);

             return $@"
 <!DOCTYPE html>
 <html lang=""en"">
 <head>
     <meta charset=""UTF-8"">
     <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
     <title>Summary</title>
     <style>
         body {{ font-family: sans-serif; line-height: 1.6; padding: 20px; }}
         h1, h2, h3 {{ margin-top: 1em; margin-bottom: 0.5em; }}
         ul, ol {{ margin-left: 20px; }}
         code {{ background-color: #f0f0f0; padding: 2px 4px; font-family: monospace; }}
         pre {{ background-color: #f4f4f4; padding: 10px; border: 1px solid #ddd; white-space: pre-wrap; word-wrap: break-word; }}
         button {{ padding: 8px 15px; margin-bottom: 15px; cursor: pointer; }}
     </style>
 </head>
 <body>
     <button id=""copyButton"" onclick=""copySummary()"">Copy Summary</button>
     <hr>
     <div id='summaryBody'>
     {htmlBody}
     </div>

     <script>
         function copySummary() {{
             const contentToCopy = document.getElementById('summaryBody');
             const button = document.getElementById('copyButton');
             let success = false;
             try {{
                 const range = document.createRange();
                 range.selectNodeContents(contentToCopy);
                 window.getSelection().removeAllRanges();
                 window.getSelection().addRange(range);
                 success = document.execCommand('copy');
                 window.getSelection().removeAllRanges();
             }} catch (err) {{
                 console.error('Failed to copy using execCommand:', err);
                 success = false;
             }}

             if (success) {{
                 button.textContent = 'Copied!';
                 setTimeout(() => {{ button.textContent = 'Copy Summary'; }}, 2000);
             }} else {{
                 alert('Failed to copy summary. Your browser might not support this action.');
             }}
         }}
     </script>
 </body>
 </html>";
         }

         private void SetUiBusyState(bool isBusy, string statusText = null)
        {
             StartButton.IsEnabled = !isBusy;
             StopButton.IsEnabled = isBusy && _isRecording;
             bool canProcessText = !isBusy && !string.IsNullOrEmpty(TranscriptionTextBox.Text) && !TranscriptionTextBox.Text.StartsWith("AUDIO TRANSCRIPTION APP INSTRUCTIONS");
             bool cleanedFileExists = !string.IsNullOrEmpty(_lastSaveDirectory) && File.Exists(Path.Combine(_lastSaveDirectory, "cleaned.txt"));
             CleanupButton.IsEnabled = canProcessText && !string.IsNullOrEmpty(_lastSaveDirectory) && !cleanedFileExists;
              SummarizeButton.IsEnabled = canProcessText;
              ClearButton.IsEnabled = !isBusy;
              SettingsButton.IsEnabled = !isBusy;
              // Mute checkboxes should ALWAYS be enabled if the service is initialized
              bool serviceReady = _audioCaptureService != null; // Add a check if needed, assume true for now
              MuteMicCheckBox.IsEnabled = serviceReady;
              MuteSystemAudioCheckBox.IsEnabled = serviceReady;


             if (isBusy)
             {
                 BusyIndicator.Visibility = Visibility.Visible;
                 if (_isRecording)
                 {
                     BusyIndicator.Foreground = Brushes.Red;
                 }
                 else
                 {
                     BusyIndicator.Foreground = SystemColors.HighlightBrush;
                 }
             }
             else
             {
                 BusyIndicator.Visibility = Visibility.Collapsed;
             }


             if (statusText != null)
             {
                 StatusTextBlock.Text = statusText;
             }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Logger.Info("Window closing. Disposing resources.");
            _audioCaptureService?.Dispose();
            _transcriptionService?.Dispose();
            _openAiChatService?.Dispose();
            Logger.Info("--- Log Session Ended ---");
        }

        // Auto-scroll TextBox
        private void TranscriptionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TranscriptionTextBox.ScrollToEnd();
        }
    }
}
