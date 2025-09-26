using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms; // Requires reference to System.Windows.Forms assembly
using System.Reflection; // For Assembly version
using System.Diagnostics; // For Process.Start
using System.Collections.ObjectModel; // For ObservableCollection
using System.Linq; // For LINQ filtering
using Newtonsoft.Json.Linq; // For parsing /config response
using AudioTranscriptionApp.Models; // For AudioDeviceModel
using AudioTranscriptionApp.Services; // For AudioCaptureService

namespace AudioTranscriptionApp
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        // Properties for device list binding
        public ObservableCollection<AudioDeviceModel> SystemAudioDevices { get; set; }
        public ObservableCollection<AudioDeviceModel> MicrophoneDevices { get; set; }

        // Temporary service instance for device listing (Consider proper DI later)
        private AudioCaptureService _tempAudioCaptureService;

        public SettingsWindow()
        {
            InitializeComponent();
            Logger.Info("Settings window opened.");

            SystemAudioDevices = new ObservableCollection<AudioDeviceModel>();
            MicrophoneDevices = new ObservableCollection<AudioDeviceModel>();
            this.DataContext = this; // Set DataContext for binding

            PopulateDeviceLists(); // Populate lists before loading settings

            LoadSettings();
            DisplayVersion(); // Display version info

            // Hook selection changed for quick diagnostics
            SystemAudioDeviceComboBox.SelectionChanged += (s, e) => UpdateDeviceFormatTexts();
            MicrophoneDeviceComboBox.SelectionChanged += (s, e) => UpdateDeviceFormatTexts();
            UpdateDeviceFormatTexts();
        }

        private void PopulateDeviceLists()
        {
            try
            {
                // Instantiate necessary services temporarily
                // NOTE: This is not ideal. Proper Dependency Injection should be used later.
                // We only need the service to call GetAudioDevices. Keys aren't needed here.
                var tempTranscriptionService = new TranscriptionService(string.Empty);
                _tempAudioCaptureService = new AudioCaptureService(tempTranscriptionService);

                var allDevices = _tempAudioCaptureService.GetAudioDevices();

                // Clear existing lists
                SystemAudioDevices.Clear();
                MicrophoneDevices.Clear();

                // Filter devices
                foreach (var device in allDevices.OrderBy(d => d.DisplayName)) // Sort for better UI
                {
                    if (device.IsInput)
                    {
                        MicrophoneDevices.Add(device);
                    }
                    else
                    {
                        SystemAudioDevices.Add(device);
                    }
                }
                Logger.Info($"Populated device lists: {SystemAudioDevices.Count} System, {MicrophoneDevices.Count} Microphones.");

                // Set default selection if lists are populated and nothing is saved yet
                // (LoadSettings will override this if values exist)
                if (SystemAudioDeviceComboBox.ItemsSource == null) SystemAudioDeviceComboBox.ItemsSource = SystemAudioDevices;
                if (MicrophoneDeviceComboBox.ItemsSource == null) MicrophoneDeviceComboBox.ItemsSource = MicrophoneDevices;

            }
            catch (Exception ex)
            {
                Logger.Error("Failed to populate device lists in SettingsWindow.", ex);
                System.Windows.MessageBox.Show("Error loading audio devices. Please check logs.", "Device Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplayVersion()
        {
            // Official version update
            VersionTextBlock.Text = "Version: 1.4";
            // The commented out try-catch block was causing the brace issue.
            // Removing the erroneous closing brace and the extra Logger call inside the comment.
            /*
            catch (Exception ex)
            {
                 Logger.Error("Failed to get assembly version.", ex);
                 VersionTextBlock.Text = "Version: Unknown";
            }
            */
        }

        private void UpdateDeviceFormatTexts()
        {
            try
            {
                var sys = SystemAudioDeviceComboBox.SelectedItem as AudioDeviceModel;
                if (sys?.Device != null)
                {
                    var fmt = sys.Device.AudioClient.MixFormat;
                    SystemFormatTextBlock.Text = $"{fmt.SampleRate} Hz, {fmt.Channels} ch, {fmt.BitsPerSample} bit";
                }
                else SystemFormatTextBlock.Text = string.Empty;

                var mic = MicrophoneDeviceComboBox.SelectedItem as AudioDeviceModel;
                if (mic?.Device != null)
                {
                    var fmt = mic.Device.AudioClient.MixFormat;
                    MicFormatTextBlock.Text = $"{fmt.SampleRate} Hz, {fmt.Channels} ch, {fmt.BitsPerSample} bit";
                }
                else MicFormatTextBlock.Text = string.Empty;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to update device format texts: {ex.Message}");
            }
        }


        private void LoadSettings()
        {
            // Load General Settings
            string encryptedApiKey = Properties.Settings.Default.ApiKey ?? string.Empty;
            ApiKeyBox.Password = EncryptionHelper.DecryptString(encryptedApiKey);
            ApiKeyTextBox.Text = ApiKeyBox.Password; // Sync textbox initially

            int chunkDuration = Properties.Settings.Default.ChunkDurationSeconds;
            if (chunkDuration < ChunkDurationSlider.Minimum) chunkDuration = (int)ChunkDurationSlider.Minimum;
            if (chunkDuration > ChunkDurationSlider.Maximum) chunkDuration = (int)ChunkDurationSlider.Maximum;
            ChunkDurationSlider.Value = chunkDuration;

            SavePathTextBox.Text = Properties.Settings.Default.DefaultSavePath ?? string.Empty;
            ExportFooterCheckBox.IsChecked = Properties.Settings.Default.ExportAppendFooter;

            // Downmix Mode
            string downmix = Properties.Settings.Default.DownmixMode ?? "Average";
            if (string.Equals(downmix, "FirstChannel", StringComparison.OrdinalIgnoreCase))
            {
                DownmixModeComboBox.SelectedIndex = 1;
            }
            else
            {
                DownmixModeComboBox.SelectedIndex = 0; // Average
            }

            // Transcription Local/Cloud Settings
            TranscriptionLocalHostTextBox.Text = Properties.Settings.Default.TranscriptionLocalHost ?? "http://localhost:5042";
            TranscriptionLocalPathTextBox.Text = Properties.Settings.Default.TranscriptionLocalPath ?? "/v1/audio/transcriptions";
            TranscriptionLocalModelTextBox.Text = Properties.Settings.Default.TranscriptionLocalModel ?? "whisper-base";
            if (Properties.Settings.Default.TranscriptionUseLocal)
                TranscriptionLocalRadio.IsChecked = true;
            else
                TranscriptionCloudRadio.IsChecked = true;
            UpdateTranscriptionModeEnabled();

            // Load Cleanup Settings
            string encryptedCleanupKey = Properties.Settings.Default.CleanupApiKey ?? string.Empty;
            CleanupApiKeyBox.Password = EncryptionHelper.DecryptString(encryptedCleanupKey);
            CleanupApiKeyTextBox.Text = CleanupApiKeyBox.Password; // Sync textbox initially
            CleanupModelTextBox.Text = Properties.Settings.Default.CleanupModel ?? string.Empty;
            CleanupLocalHostTextBox.Text = Properties.Settings.Default.CleanupLocalHost ?? "http://localhost:1234";
            CleanupLocalPathTextBox.Text = Properties.Settings.Default.CleanupLocalPath ?? "/v1/chat/completions";
            if (Properties.Settings.Default.CleanupUseLocal)
                CleanupLocalRadio.IsChecked = true;
            else
                CleanupCloudRadio.IsChecked = true;
            UpdateCleanupModeEnabled();
            CleanupPromptTextBox.Text = Properties.Settings.Default.CleanupPrompt ?? string.Empty;

            // Load Summarize Settings
            string encryptedSummarizeKey = Properties.Settings.Default.SummarizeApiKey ?? string.Empty;
            SummarizeApiKeyBox.Password = EncryptionHelper.DecryptString(encryptedSummarizeKey);
            SummarizeApiKeyTextBox.Text = SummarizeApiKeyBox.Password; // Sync textbox initially
            SummarizeModelTextBox.Text = Properties.Settings.Default.SummarizeModel ?? string.Empty;
            SummarizePromptTextBox.Text = Properties.Settings.Default.SummarizePrompt ?? string.Empty;
            SummarizeLocalHostTextBox.Text = Properties.Settings.Default.SummarizeLocalHost ?? "http://localhost:1234";
            SummarizeLocalPathTextBox.Text = Properties.Settings.Default.SummarizeLocalPath ?? "/v1/chat/completions";
            if (Properties.Settings.Default.SummarizeUseLocal)
                SummarizeLocalRadio.IsChecked = true;
            else
                SummarizeCloudRadio.IsChecked = true;
            UpdateSummarizeModeEnabled();

            // Load Device Selections
            string savedSystemDeviceId = Properties.Settings.Default.SystemAudioDeviceId;
            string savedMicDeviceId = Properties.Settings.Default.MicrophoneDeviceId;

            if (!string.IsNullOrEmpty(savedSystemDeviceId) && SystemAudioDevices.Any(d => d.Id == savedSystemDeviceId))
            {
                SystemAudioDeviceComboBox.SelectedValue = savedSystemDeviceId;
                Logger.Info($"Loaded saved System Audio Device: {savedSystemDeviceId}");
            }
            else if (SystemAudioDevices.Count > 0)
            {
                 SystemAudioDeviceComboBox.SelectedIndex = 0; // Default to first if none saved or invalid
                 Logger.Info("No valid saved System Audio Device found, defaulting to first available.");
            }

            if (!string.IsNullOrEmpty(savedMicDeviceId) && MicrophoneDevices.Any(d => d.Id == savedMicDeviceId))
            {
                MicrophoneDeviceComboBox.SelectedValue = savedMicDeviceId;
                Logger.Info($"Loaded saved Microphone Device: {savedMicDeviceId}");
            }
             else if (MicrophoneDevices.Count > 0)
            {
                 MicrophoneDeviceComboBox.SelectedIndex = 0; // Default to first if none saved or invalid
                 Logger.Info("No valid saved Microphone Device found, defaulting to first available.");
            }


            Logger.Info("Settings loaded into UI.");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                 // Validate Save Path (optional, but good practice)
                 string savePath = SavePathTextBox.Text;
                 if (!string.IsNullOrEmpty(savePath) && !Directory.Exists(savePath))
                 {
                    // Use System.Windows.MessageBox explicitly
                    if (System.Windows.MessageBox.Show($"The specified save path does not exist:\n{savePath}\n\nDo you want to create it?",
                                       "Create Directory?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        try
                        {
                            Logger.Info($"Attempting to create directory: {savePath}");
                            Directory.CreateDirectory(savePath);
                            Logger.Info("Directory created successfully.");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Failed to create directory: {savePath}", ex);
                            // Use System.Windows.MessageBox explicitly
                            System.Windows.MessageBox.Show($"Failed to create directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return; // Don't save if directory creation failed
                        }
                    }
                   else
                   {
                       // User chose not to create, maybe highlight the field or just don't save path
                       // For simplicity, we'll just proceed but the path might be invalid later
                   }
                }


                // Save General Settings (Get key from visible control)
                string whisperKey = ShowApiKeyCheckBox.IsChecked == true ? ApiKeyTextBox.Text : ApiKeyBox.Password;
                Properties.Settings.Default.ApiKey = EncryptionHelper.EncryptString(whisperKey);
                Properties.Settings.Default.ChunkDurationSeconds = (int)ChunkDurationSlider.Value;
                Properties.Settings.Default.DefaultSavePath = SavePathTextBox.Text;
                Properties.Settings.Default.DownmixMode = (DownmixModeComboBox.SelectedIndex == 1) ? "FirstChannel" : "Average";
                Properties.Settings.Default.ExportAppendFooter = ExportFooterCheckBox.IsChecked == true;

                // Save Transcription Local/Cloud
                Properties.Settings.Default.TranscriptionUseLocal = TranscriptionLocalRadio.IsChecked == true;
                Properties.Settings.Default.TranscriptionLocalHost = (TranscriptionLocalHostTextBox.Text ?? string.Empty).Trim();
                Properties.Settings.Default.TranscriptionLocalPath = NormalizePath(TranscriptionLocalPathTextBox.Text);
                Properties.Settings.Default.TranscriptionLocalModel = (TranscriptionLocalModelTextBox.Text ?? string.Empty).Trim();

                // Save Cleanup Settings (Get key from visible control)
                string cleanupKey = ShowCleanupApiKeyCheckBox.IsChecked == true ? CleanupApiKeyTextBox.Text : CleanupApiKeyBox.Password;
                Properties.Settings.Default.CleanupApiKey = EncryptionHelper.EncryptString(cleanupKey);
                Properties.Settings.Default.CleanupModel = CleanupModelTextBox.Text;
                Properties.Settings.Default.CleanupPrompt = CleanupPromptTextBox.Text;
                Properties.Settings.Default.CleanupUseLocal = CleanupLocalRadio.IsChecked == true;
                Properties.Settings.Default.CleanupLocalHost = (CleanupLocalHostTextBox.Text ?? string.Empty).Trim();
                Properties.Settings.Default.CleanupLocalPath = NormalizePath(CleanupLocalPathTextBox.Text);

                // Save Summarize Settings (Get key from visible control)
                string summarizeKey = ShowSummarizeApiKeyCheckBox.IsChecked == true ? SummarizeApiKeyTextBox.Text : SummarizeApiKeyBox.Password;
                Properties.Settings.Default.SummarizeApiKey = EncryptionHelper.EncryptString(summarizeKey);
                Properties.Settings.Default.SummarizeModel = SummarizeModelTextBox.Text;
                Properties.Settings.Default.SummarizePrompt = SummarizePromptTextBox.Text;
                Properties.Settings.Default.SummarizeUseLocal = SummarizeLocalRadio.IsChecked == true;
                Properties.Settings.Default.SummarizeLocalHost = (SummarizeLocalHostTextBox.Text ?? string.Empty).Trim();
                Properties.Settings.Default.SummarizeLocalPath = NormalizePath(SummarizeLocalPathTextBox.Text);

                // Save Device Selections
                Properties.Settings.Default.SystemAudioDeviceId = SystemAudioDeviceComboBox.SelectedValue as string;
                Properties.Settings.Default.MicrophoneDeviceId = MicrophoneDeviceComboBox.SelectedValue as string;
                Logger.Info($"Saving System Device ID: {Properties.Settings.Default.SystemAudioDeviceId}");
                Logger.Info($"Saving Microphone Device ID: {Properties.Settings.Default.MicrophoneDeviceId}");


                // Persist settings
                Properties.Settings.Default.Save();
                Logger.Info("Settings saved.");

                this.DialogResult = true; // Indicate settings were saved
                this.Close();
             }
             catch (Exception ex)
             {
                 Logger.Error("Error occurred while saving settings.", ex);
                 // Use System.Windows.MessageBox explicitly
                 System.Windows.MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
             }
         }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info("Settings window cancelled.");
            this.DialogResult = false;
            this.Close();
        }

        private string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "/";
            path = path.Trim();
            if (!path.StartsWith("/")) path = "/" + path;
            return path;
        }

        // Mode toggle handlers
        private void TranscriptionModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            UpdateTranscriptionModeEnabled();
        }
        private void CleanupModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            UpdateCleanupModeEnabled();
        }
        private void SummarizeModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            UpdateSummarizeModeEnabled();
        }

        private void UpdateTranscriptionModeEnabled()
        {
            bool useLocal = TranscriptionLocalRadio.IsChecked == true;
            ApiKeyBox.IsEnabled = !useLocal;
            ApiKeyTextBox.IsEnabled = !useLocal;
            ShowApiKeyCheckBox.IsEnabled = !useLocal;
            TranscriptionLocalHostTextBox.IsEnabled = useLocal;
            TranscriptionLocalPathTextBox.IsEnabled = useLocal;
            TranscriptionTestButton.IsEnabled = useLocal;
            TranscriptionLocalModelTextBox.IsEnabled = useLocal;
        }

        private void UpdateCleanupModeEnabled()
        {
            bool useLocal = CleanupLocalRadio.IsChecked == true;
            CleanupApiKeyBox.IsEnabled = !useLocal;
            CleanupApiKeyTextBox.IsEnabled = !useLocal;
            ShowCleanupApiKeyCheckBox.IsEnabled = !useLocal;
            CleanupModelTextBox.IsEnabled = !useLocal;
            CleanupLocalHostTextBox.IsEnabled = useLocal;
            CleanupLocalPathTextBox.IsEnabled = useLocal;
            CleanupTestButton.IsEnabled = useLocal;
        }

        private void UpdateSummarizeModeEnabled()
        {
            bool useLocal = SummarizeLocalRadio.IsChecked == true;
            SummarizeApiKeyBox.IsEnabled = !useLocal;
            SummarizeApiKeyTextBox.IsEnabled = !useLocal;
            ShowSummarizeApiKeyCheckBox.IsEnabled = !useLocal;
            SummarizeModelTextBox.IsEnabled = !useLocal;
            SummarizeLocalHostTextBox.IsEnabled = useLocal;
            SummarizeLocalPathTextBox.IsEnabled = useLocal;
            SummarizeTestButton.IsEnabled = useLocal;
        }

        // Local Test Buttons
        private async void TranscriptionTestButton_Click(object sender, RoutedEventArgs e)
        {
            await TestTranscriptionLocalAndPopulateModelAsync(TranscriptionLocalHostTextBox.Text);
        }

        private async void CleanupTestButton_Click(object sender, RoutedEventArgs e)
        {
            await TestLocalEndpointAsync(CleanupLocalHostTextBox.Text);
        }

        private async void SummarizeTestButton_Click(object sender, RoutedEventArgs e)
        {
            await TestLocalEndpointAsync(SummarizeLocalHostTextBox.Text);
        }

        private async System.Threading.Tasks.Task TestLocalEndpointAsync(string host)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(host))
                {
                    System.Windows.MessageBox.Show("Enter Local Host including protocol and port, e.g., http://localhost:1234", "Missing Host", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                string url = host.Trim().TrimEnd('/') + "/v1/models";
                Logger.Info($"Testing local endpoint: {url}")
                ;using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    var response = await client.GetAsync(url);
                    Logger.Info($"Local test response: {(int)response.StatusCode} {response.StatusCode}");
                    if (response.IsSuccessStatusCode)
                    {
                        System.Windows.MessageBox.Show("Connection successful.", "Local Test", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        Logger.Warning($"Local test failed: {response.StatusCode} - {content}");
                        System.Windows.MessageBox.Show($"Connection failed: {(int)response.StatusCode} {response.StatusCode}", "Local Test", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                Logger.Warning("Local test failed: timed out after 30s");
                System.Windows.MessageBox.Show("Connection failed: timed out after 30s", "Local Test", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Logger.Error("Local test error", ex);
                System.Windows.MessageBox.Show($"Connection failed: {ex.Message}", "Local Test", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task TestTranscriptionLocalAndPopulateModelAsync(string host)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(host))
                {
                    System.Windows.MessageBox.Show("Enter Local Host including protocol and port, e.g., http://localhost:5042", "Missing Host", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                string url = host.Trim().TrimEnd('/') + "/config";
                Logger.Info($"Testing local transcription server config: {url}");
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var response = await client.GetAsync(url);
                    Logger.Info($"Local config response: {(int)response.StatusCode} {response.StatusCode}");
                    if (response.IsSuccessStatusCode)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        try
                        {
                            var jo = JObject.Parse(content);
                            // Try both PascalCase and camelCase paths
                            string modelName =
                                (string)(jo.SelectToken("Whisper.ModelName") ?? jo.SelectToken("whisper.modelName")) ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(modelName))
                            {
                                TranscriptionLocalModelTextBox.Text = modelName;
                                System.Windows.MessageBox.Show("Connection successful.", "Local Test", MessageBoxButton.OK, MessageBoxImage.Information);
                                return;
                            }
                            else
                            {
                                Logger.Warning("/config response did not include a recognizable Whisper model name.");
                                System.Windows.MessageBox.Show("Connection successful, but model not reported.", "Local Test", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"Failed to parse /config JSON: {ex.Message}");
                            System.Windows.MessageBox.Show("Connection successful, but invalid config format.", "Local Test", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Server not found", "Local Test", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                Logger.Warning("Local config test failed: timed out after 5s");
                System.Windows.MessageBox.Show("Server not found", "Local Test", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Logger.Error("Local config test error", ex);
                System.Windows.MessageBox.Show("Server not found", "Local Test", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void CleanupListModelsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string key = ShowCleanupApiKeyCheckBox.IsChecked == true ? CleanupApiKeyTextBox.Text : CleanupApiKeyBox.Password;
                if (string.IsNullOrWhiteSpace(key)) { System.Windows.MessageBox.Show("Enter Cleanup API key first.", "Missing Key", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                using (var svc = new OpenAiChatService(key))
                {
                    var models = await svc.ListModelsAsync();
                    if (models == null || models.Count == 0)
                    {
                        System.Windows.MessageBox.Show("No models returned.", "OpenAI Models", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    string sample = string.Join("\n", models.Take(30));
                    System.Windows.MessageBox.Show($"Available models (showing up to 30):\n\n{sample}", "OpenAI Models", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to list Cleanup models.", ex);
                System.Windows.MessageBox.Show($"Failed to list models: {ex.Message}", "OpenAI Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SummarizeListModelsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string key = ShowSummarizeApiKeyCheckBox.IsChecked == true ? SummarizeApiKeyTextBox.Text : SummarizeApiKeyBox.Password;
                if (string.IsNullOrWhiteSpace(key)) { System.Windows.MessageBox.Show("Enter Summarize API key first.", "Missing Key", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                using (var svc = new OpenAiChatService(key))
                {
                    var models = await svc.ListModelsAsync();
                    if (models == null || models.Count == 0)
                    {
                        System.Windows.MessageBox.Show("No models returned.", "OpenAI Models", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    string sample = string.Join("\n", models.Take(30));
                    System.Windows.MessageBox.Show($"Available models (showing up to 30):\n\n{sample}", "OpenAI Models", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to list Summarize models.", ex);
                System.Windows.MessageBox.Show($"Failed to list models: {ex.Message}", "OpenAI Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info("Browse button clicked.");
            using (var dialog = new FolderBrowserDialog())
            {
                // Set initial directory if one is already set
                if (!string.IsNullOrEmpty(SavePathTextBox.Text) && Directory.Exists(SavePathTextBox.Text))
                {
                    dialog.SelectedPath = SavePathTextBox.Text;
                }
                else
                {
                     // Optionally set a default starting path, e.g., MyDocuments
                     dialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }

                dialog.Description = "Select Default Save Folder";
                dialog.ShowNewFolderButton = true;

                // ShowDialog requires a handle, need to get it from the WPF window
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                DialogResult result = dialog.ShowDialog(new Win32Window(helper.Handle));

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    Logger.Info($"User selected save path: {dialog.SelectedPath}");
                    SavePathTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void ChunkDurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // The TextBox is bound to the slider value, so this handler might not be strictly necessary
            // unless you need to perform additional actions when the value changes.
        }

        // --- API Key Visibility Toggle Handlers ---

        private void ShowApiKeyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            ApiKeyTextBox.Text = ApiKeyBox.Password;
            ApiKeyTextBox.Visibility = Visibility.Visible;
            ApiKeyBox.Visibility = Visibility.Collapsed;
            ShowApiKeyCheckBox.Content = "Hide";
        }

        private void ShowApiKeyCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            ApiKeyBox.Password = ApiKeyTextBox.Text;
            ApiKeyTextBox.Visibility = Visibility.Collapsed;
            ApiKeyBox.Visibility = Visibility.Visible;
            ShowApiKeyCheckBox.Content = "Show";
        }

        private void ShowCleanupApiKeyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CleanupApiKeyTextBox.Text = CleanupApiKeyBox.Password;
            CleanupApiKeyTextBox.Visibility = Visibility.Visible;
            CleanupApiKeyBox.Visibility = Visibility.Collapsed;
            ShowCleanupApiKeyCheckBox.Content = "Hide";
        }

        private void ShowCleanupApiKeyCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CleanupApiKeyBox.Password = CleanupApiKeyTextBox.Text;
            CleanupApiKeyTextBox.Visibility = Visibility.Collapsed;
            CleanupApiKeyBox.Visibility = Visibility.Visible;
            ShowCleanupApiKeyCheckBox.Content = "Show";
        }

         private void ShowSummarizeApiKeyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            SummarizeApiKeyTextBox.Text = SummarizeApiKeyBox.Password;
            SummarizeApiKeyTextBox.Visibility = Visibility.Visible;
            SummarizeApiKeyBox.Visibility = Visibility.Collapsed;
            ShowSummarizeApiKeyCheckBox.Content = "Hide";
        }

        private void ShowSummarizeApiKeyCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SummarizeApiKeyBox.Password = SummarizeApiKeyTextBox.Text;
            SummarizeApiKeyTextBox.Visibility = Visibility.Collapsed;
            SummarizeApiKeyBox.Visibility = Visibility.Visible;
            ShowSummarizeApiKeyCheckBox.Content = "Show";
        }

        // --- End API Key Visibility Toggle Handlers ---

        // --- About Tab Logic ---
        private void ViewLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string licenseFilePath = Path.Combine(baseDirectory, "LICENSE");

                if (!File.Exists(licenseFilePath))
                {
                    Logger.Warning($"LICENSE file not found at: {licenseFilePath}");
                    System.Windows.MessageBox.Show("LICENSE file not found.", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string licenseText = File.ReadAllText(licenseFilePath);
                var textBlock = new System.Windows.Controls.TextBox
                {
                    Text = licenseText,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(10),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                };

                var dialog = new Window
                {
                    Title = "License",
                    Owner = this,
                    Width = 700,
                    Height = 600,
                    Content = textBlock,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to display LICENSE file.", ex);
                System.Windows.MessageBox.Show($"Could not display LICENSE file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // --- End About Tab Logic ---


        // Helper to find ComboBoxItem by content
        private System.Windows.Controls.ComboBoxItem FindComboBoxItem(System.Windows.Controls.ComboBox comboBox, string content)
        {
            foreach (System.Windows.Controls.ComboBoxItem item in comboBox.Items)
            {
                 if (item.Content?.ToString() == content)
                {
                    return item;
                }
            }
            // Return default if not found (or handle error)
             Logger.Warning($"Could not find ComboBoxItem with content '{content}'. Returning first item or null.");
             return comboBox.Items.Count > 0 ? (System.Windows.Controls.ComboBoxItem)comboBox.Items[0] : null;
        }

        // Helper class to wrap the WPF window handle for FolderBrowserDialog
        private class Win32Window : System.Windows.Forms.IWin32Window
        {
            public IntPtr Handle { get; private set; }
            public Win32Window(IntPtr handle)
            {
                Handle = handle;
            }
        }
    }
}

