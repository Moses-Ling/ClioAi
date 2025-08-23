using AudioTranscriptionApp.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders; // Added for MixingSampleProvider
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading; // Added for Interlocked
using System.Threading.Tasks;

namespace AudioTranscriptionApp.Services
{
    public class AudioCaptureService : IDisposable
    {
        private ISampleProvider EnsureMono(ISampleProvider source, string sourceName)
        {
            if (source.WaveFormat.Channels == 1) return source;

            var mode = Properties.Settings.Default.DownmixMode;
            bool useFirst = string.Equals(mode, "FirstChannel", StringComparison.OrdinalIgnoreCase);

            if (useFirst)
            {
                Logger.Warning($"Downmixing {sourceName} by selecting first channel from {source.WaveFormat.Channels} channels");
                return new SelectChannelSampleProvider(source, 0);
            }
            else
            {
                if (source.WaveFormat.Channels == 2)
                {
                    Logger.Warning($"Downmixing {sourceName} from stereo to mono (average)");
                    return new StereoToMonoSampleProvider(source);
                }
                Logger.Warning($"Downmixing {sourceName} with {source.WaveFormat.Channels} channels to mono by averaging");
                return new DownmixToMonoSampleProvider(source);
            }
        }

        private ISampleProvider EnsureSampleRate(ISampleProvider source, int targetSampleRate, string sourceName)
        {
            if (source.WaveFormat.SampleRate == targetSampleRate) return source;
            Logger.Warning($"Resampling {sourceName} from {source.WaveFormat.SampleRate}Hz to {targetSampleRate}Hz");
            return new WdlResamplingSampleProvider(source, targetSampleRate);
        }

        // Simple downmixer that averages all channels to mono (float)
        internal sealed class DownmixToMonoSampleProvider : ISampleProvider
        {
            private readonly ISampleProvider _source;
            private readonly WaveFormat _waveFormat;
            private float[] _buffer;

            public DownmixToMonoSampleProvider(ISampleProvider source)
            {
                _source = source;
                _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
            }

            public WaveFormat WaveFormat => _waveFormat;

            public int Read(float[] buffer, int offset, int count)
            {
                int channels = _source.WaveFormat.Channels;
                int framesRequested = count; // since output is mono, frames == samples
                if (_buffer == null) _buffer = new float[framesRequested * channels];

                int sourceSamplesNeeded = framesRequested * channels;
                if (_buffer.Length < sourceSamplesNeeded) _buffer = new float[sourceSamplesNeeded];

                int samplesRead = _source.Read(_buffer, 0, sourceSamplesNeeded);
                int framesRead = samplesRead / channels;

                for (int n = 0; n < framesRead; n++)
                {
                    float sum = 0f;
                    int baseIdx = n * channels;
                    for (int c = 0; c < channels; c++) sum += _buffer[baseIdx + c];
                    buffer[offset + n] = sum / channels;
                }

                return framesRead; // mono samples written equals framesRead
            }
        }

        // Selects a single channel from a multi-channel source as mono
        internal sealed class SelectChannelSampleProvider : ISampleProvider
        {
            private readonly ISampleProvider _source;
            private readonly int _channelIndex;
            private readonly WaveFormat _waveFormat;
            private float[] _buffer;

            public SelectChannelSampleProvider(ISampleProvider source, int channelIndex)
            {
                _source = source;
                _channelIndex = Math.Max(0, Math.Min(channelIndex, source.WaveFormat.Channels - 1));
                _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
            }

            public WaveFormat WaveFormat => _waveFormat;

            public int Read(float[] buffer, int offset, int count)
            {
                int channels = _source.WaveFormat.Channels;
                int framesRequested = count; // mono output
                if (_buffer == null) _buffer = new float[framesRequested * channels];
                int sourceSamplesNeeded = framesRequested * channels;
                if (_buffer.Length < sourceSamplesNeeded) _buffer = new float[sourceSamplesNeeded];

                int samplesRead = _source.Read(_buffer, 0, sourceSamplesNeeded);
                int framesRead = samplesRead / channels;

                for (int n = 0; n < framesRead; n++)
                {
                    buffer[offset + n] = _buffer[n * channels + _channelIndex];
                }
                return framesRead;
            }
        }
        // V1 Capture (System Audio) - Renamed for clarity
        private WasapiLoopbackCapture _systemAudioCapture;
        // V2 Capture (Microphone)
        private WasapiCapture _microphoneCapture;

        // V2 Mixing
        private MixingSampleProvider _mixer;
        private BufferedWaveProvider _systemAudioBuffer;
        private BufferedWaveProvider _microphoneBuffer;

        // V1 Writer (Now writes mixed output)
        private WaveFileWriter _writer;
        private string _tempFilePath;
        private bool _isRecording = false;
        // Use int for Interlocked: 0 = false, 1 = true
        private int _processingChunkFlag = 0;
        private Task _writeLoopTask = null; // Keep track of the write loop task
        private bool _isSystemAudioMuted = false; // Mute flag for system audio
        private bool _isMicrophoneMuted = false; // Mute flag for microphone

        // V2 Selected Device IDs
        private string _selectedSystemDeviceId;
        private string _selectedMicrophoneDeviceId;
        private DateTime _chunkStartTime; // Start time of the current chunk file
        private DateTime _sessionStartTime; // Start time of the entire recording session
        private int _audioChunkSeconds; // Process in configurable chunks
        private readonly TranscriptionService _transcriptionService;
        private readonly object _reinitLock = new object();
        private System.Threading.Timer _reinitTimer;

        // Audio level monitoring - Phase 3
        private float _systemAudioLevel = 0;
        private float _micAudioLevel = 0;
        private readonly float _audioLevelSmoothingFactor = 0.2f; // Re-added for level calculation

        // Events - Phase 3
        public event EventHandler<float> SystemAudioLevelChanged; // New event for system audio level
        public event EventHandler<float> MicrophoneLevelChanged; // New event for microphone level
        public event EventHandler<string> TranscriptionReceived;
        public event EventHandler<string> StatusChanged;
        public event EventHandler<Exception> ErrorOccurred;
        public event EventHandler<TimeSpan> RecordingTimeUpdate; // Added event for timer

        public bool IsRecording => _isRecording;
        public TimeSpan RecordedDuration { get; private set; } // To store final duration

        public AudioCaptureService(TranscriptionService transcriptionService)
        {
            Logger.Info("AudioCaptureService initializing.");
            _transcriptionService = transcriptionService;
            // Load chunk duration from settings
            _audioChunkSeconds = Properties.Settings.Default.ChunkDurationSeconds;
            // Add validation if needed (e.g., ensure it's within 5-60 range)
            if (_audioChunkSeconds < 5) _audioChunkSeconds = 5;
            if (_audioChunkSeconds > 60) _audioChunkSeconds = 60;
            Logger.Info($"Using audio chunk duration: {_audioChunkSeconds} seconds.");

            // V2: Load selected device IDs on initialization
            _selectedSystemDeviceId = Properties.Settings.Default.SystemAudioDeviceId;
            _selectedMicrophoneDeviceId = Properties.Settings.Default.MicrophoneDeviceId;
            InitializeDevicesAndMixer(); // Attempt initial setup
        }

        public List<AudioDeviceModel> GetAudioDevices()
        {
            var audioDevices = new List<AudioDeviceModel>();
            Logger.Info("Getting audio devices (System Output and Microphone Input)...");
            var deviceEnumerator = new MMDeviceEnumerator();

            try
            {
                // Get System Output Devices (Render)
                Logger.Info("Enumerating System Output (Render) devices...");
                var outputDevices = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                foreach (var device in outputDevices)
                {
                    audioDevices.Add(new AudioDeviceModel
                    {
                        Id = device.ID,
                        DisplayName = $"[System] {device.FriendlyName}", // Add prefix
                        Device = device,
                        IsInput = false // Mark as output
                    });
                    Logger.Info($"Found System Output: {device.FriendlyName} (ID: {device.ID})");
                }

                // Get Microphone Input Devices (Capture)
                Logger.Info("Enumerating Microphone Input (Capture) devices...");
                var inputDevices = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                foreach (var device in inputDevices)
                {
                    audioDevices.Add(new AudioDeviceModel
                    {
                        Id = device.ID,
                        DisplayName = $"[Mic] {device.FriendlyName}", // Add prefix
                        Device = device,
                        IsInput = true // Mark as input
                    });
                    Logger.Info($"Found Microphone Input: {device.FriendlyName} (ID: {device.ID})");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to enumerate audio devices (Render or Capture).", ex);
                ErrorOccurred?.Invoke(this, ex);
            }

            Logger.Info($"Found {audioDevices.Count} active audio devices.");
            return audioDevices;
        }

        // V2: Methods to set selected devices called from UI/Settings
        public void SetSystemAudioDevice(string deviceId)
        {
            if (_selectedSystemDeviceId == deviceId) return; // No change
            _selectedSystemDeviceId = deviceId;
            Properties.Settings.Default.SystemAudioDeviceId = deviceId; // Save setting immediately
            Properties.Settings.Default.Save();
            Logger.Info($"System Audio Device ID set and saved: {deviceId ?? "None"}");
            // Re-initialize if not recording
            ScheduleReinit();
        }

        public void SetMicrophoneDevice(string deviceId)
        {
             if (_selectedMicrophoneDeviceId == deviceId) return; // No change
            _selectedMicrophoneDeviceId = deviceId;
             Properties.Settings.Default.MicrophoneDeviceId = deviceId; // Save setting immediately
            Properties.Settings.Default.Save();
            Logger.Info($"Microphone Device ID set and saved: {deviceId ?? "None"}");
            // Re-initialize if not recording
            ScheduleReinit();
        }

        // V2: Mute Toggle Methods - Phase 3
        public void ToggleSystemAudioMute(bool isMuted)
        {
            _isSystemAudioMuted = isMuted;
            Logger.Info($"System Audio Mute Toggled: {_isSystemAudioMuted}");
            // Optionally clear buffer when muted? Might prevent stale audio if unmuted later.
            // if (_isSystemAudioMuted) _systemAudioBuffer?.ClearBuffer();
        }

        public void ToggleMicrophoneMute(bool isMuted)
        {
            _isMicrophoneMuted = isMuted;
            Logger.Info($"Microphone Mute Toggled: {_isMicrophoneMuted}");
            // if (_isMicrophoneMuted) _microphoneBuffer?.ClearBuffer();
        }


        // V2: Renamed and refactored initialization for dual capture and mixing
        private void InitializeDevicesAndMixer()
        {
            Logger.Info("Attempting to initialize devices and mixer...");
            DisposeCurrentCapturesAndMixer(); // Clean up previous instances first

            if (string.IsNullOrEmpty(_selectedSystemDeviceId) || string.IsNullOrEmpty(_selectedMicrophoneDeviceId))
            {
                Logger.Warning("Cannot initialize: System Audio or Microphone device not selected.");
                StatusChanged?.Invoke(this, "Select System Audio and Microphone in Settings.");
                return;
            }

            try
            {
                var deviceEnumerator = new MMDeviceEnumerator();
                MMDevice systemDevice = null;
                MMDevice micDevice = null;

                // Get selected devices
                try { systemDevice = deviceEnumerator.GetDevice(_selectedSystemDeviceId); }
                catch (Exception ex) { Logger.Error($"Failed to get system audio device with ID: {_selectedSystemDeviceId}", ex); ErrorOccurred?.Invoke(this, new Exception($"Failed to get system audio device: {ex.Message}")); return; }

                try { micDevice = deviceEnumerator.GetDevice(_selectedMicrophoneDeviceId); }
                catch (Exception ex) { Logger.Error($"Failed to get microphone device with ID: {_selectedMicrophoneDeviceId}", ex); ErrorOccurred?.Invoke(this, new Exception($"Failed to get microphone device: {ex.Message}")); return; }

                if (systemDevice == null || micDevice == null)
                {
                    Logger.Error("Failed to retrieve one or both selected audio devices.");
                    StatusChanged?.Invoke(this, "Error retrieving selected audio devices.");
                    return;
                }

                Logger.Info($"Initializing System Audio Capture: {systemDevice.FriendlyName}");
                _systemAudioCapture = new WasapiLoopbackCapture(systemDevice);
                _systemAudioCapture.DataAvailable += SystemAudioCapture_DataAvailable;
                _systemAudioCapture.RecordingStopped += Capture_RecordingStopped;

                Logger.Info($"Initializing Microphone Capture: {micDevice.FriendlyName}");
                _microphoneCapture = new WasapiCapture(micDevice);
                _microphoneCapture.DataAvailable += MicrophoneCapture_DataAvailable;
                _microphoneCapture.RecordingStopped += Capture_RecordingStopped;

                // --- Setup Mixer ---
                var mixerFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2); // Target format (48kHz, stereo, float)
                Logger.Info($"Mixer Target Format: {mixerFormat}");

                _mixer = new MixingSampleProvider(mixerFormat);
                _mixer.ReadFully = true;

                // Prepare System Audio chain -> mono -> resample
                _systemAudioBuffer = new BufferedWaveProvider(_systemAudioCapture.WaveFormat);
                var sysSource = _systemAudioBuffer.ToSampleProvider();
                Logger.Info($"System source format: {sysSource.WaveFormat.SampleRate}Hz, {sysSource.WaveFormat.Channels}ch");
                var sysMono = EnsureMono(sysSource, sourceName: "System Audio");
                Logger.Info($"System mono format: {sysMono.WaveFormat.SampleRate}Hz, {sysMono.WaveFormat.Channels}ch");
                var sysResampled = EnsureSampleRate(sysMono, mixerFormat.SampleRate, sourceName: "System Audio");
                Logger.Info($"System resampled format: {sysResampled.WaveFormat.SampleRate}Hz, {sysResampled.WaveFormat.Channels}ch");

                // Prepare Microphone chain -> mono -> resample
                _microphoneBuffer = new BufferedWaveProvider(_microphoneCapture.WaveFormat);
                var micSource = _microphoneBuffer.ToSampleProvider();
                Logger.Info($"Mic source format: {micSource.WaveFormat.SampleRate}Hz, {micSource.WaveFormat.Channels}ch");
                var micMono = EnsureMono(micSource, sourceName: "Microphone");
                Logger.Info($"Mic mono format: {micMono.WaveFormat.SampleRate}Hz, {micMono.WaveFormat.Channels}ch");
                var micResampled = EnsureSampleRate(micMono, mixerFormat.SampleRate, sourceName: "Microphone");
                Logger.Info($"Mic resampled format: {micResampled.WaveFormat.SampleRate}Hz, {micResampled.WaveFormat.Channels}ch");

                // Route mono inputs to stereo L/R using MultiplexingSampleProvider
                var mux = new MultiplexingSampleProvider(new[] { micResampled, sysResampled }, 2);
                mux.ConnectInputToOutput(0, 0); // Mic -> Left
                mux.ConnectInputToOutput(1, 1); // System -> Right

                _mixer.AddMixerInput(mux);
                Logger.Info("Added multiplexed inputs to mixer (Mic->L, System->R).");

                StatusChanged?.Invoke(this, $"Ready. System: {systemDevice.FriendlyName}, Mic: {micDevice.FriendlyName}");
                Logger.Info("Devices and mixer initialized successfully.");

            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize devices or mixer.", ex);
                StatusChanged?.Invoke(this, $"Error initializing devices: {ex.Message}");
                ErrorOccurred?.Invoke(this, ex);
                DisposeCurrentCapturesAndMixer();
            }
        }

        public void StartRecording()
        {
            Logger.Info("Attempting to start recording.");
            if (_isRecording)
            {
                Logger.Warning("StartRecording called while already recording.");
                return;
            }
            if (_systemAudioCapture == null || _microphoneCapture == null || _mixer == null)
            {
                 Logger.Error("Cannot start recording: Devices or mixer not initialized. Please check settings.");
                 StatusChanged?.Invoke(this, "Error: Select System Audio and Microphone in Settings.");
                 InitializeDevicesAndMixer(); // Attempt re-initialization
                 if (_systemAudioCapture == null || _microphoneCapture == null || _mixer == null)
                 {
                      ErrorOccurred?.Invoke(this, new InvalidOperationException("Audio devices or mixer failed to initialize."));
                      return;
                 }
            }

            _isRecording = true;
            // Ensure initialization succeeded before starting
            if (_systemAudioCapture == null || _microphoneCapture == null || _mixer == null)
            {
                 Logger.Error("StartRecording called but initialization failed previously. Check logs.");
                 StatusChanged?.Invoke(this, "Error: Initialization failed. Check Settings/Logs.");
                 _isRecording = false; // Reset flag if init check fails here
                 return;
            }

            // Reload chunk duration from settings right before starting
            _audioChunkSeconds = Properties.Settings.Default.ChunkDurationSeconds;
            if (_audioChunkSeconds < 5) _audioChunkSeconds = 5;
            if (_audioChunkSeconds > 25) _audioChunkSeconds = 25; // Ensure it respects the new max
            Logger.Info($"Using audio chunk duration for this session: {_audioChunkSeconds} seconds.");

            CreateNewAudioFile(); // Creates the initial temp file and starts write loop
            if (_writer == null) // Check if file creation failed
            {
                 Logger.Error("StartRecording failed because CreateNewAudioFile failed.");
                 _isRecording = false; // Ensure flag is reset
                 return;
            }

            try
            {
                // Clear buffers
                _systemAudioBuffer?.ClearBuffer();
                _microphoneBuffer?.ClearBuffer();

                // Reset mute states
                _isSystemAudioMuted = false;
                _isMicrophoneMuted = false;

                // Reset levels - Phase 3
                _systemAudioLevel = 0;
                _micAudioLevel = 0;
                SystemAudioLevelChanged?.Invoke(this, 0);
                MicrophoneLevelChanged?.Invoke(this, 0);


                // Start recording
                _sessionStartTime = DateTime.Now;
                _chunkStartTime = _sessionStartTime;
                RecordedDuration = TimeSpan.Zero;

                Logger.Info("Starting System Audio Capture...");
                _systemAudioCapture.StartRecording();
                Logger.Info("Starting Microphone Capture...");
                _microphoneCapture.StartRecording();

                StatusChanged?.Invoke(this, "Recording (System + Mic)...");
                Logger.Info("Recording started successfully for both sources.");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to start recording.", ex);
                StatusChanged?.Invoke(this, $"Error starting recording: {ex.Message}");
                ErrorOccurred?.Invoke(this, ex);
                _isRecording = false;
                DisposeCurrentCapturesAndMixer();
            }
        }

        // V2: Separate handlers for each source feeding into buffers
        private void SystemAudioCapture_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (!_isRecording || _systemAudioBuffer == null) return;

            // Calculate level BEFORE muting
            CalculateAndRaiseLevel(e.Buffer, e.BytesRecorded, _systemAudioCapture.WaveFormat, ref _systemAudioLevel, SystemAudioLevelChanged);

            // Add samples to buffer only if not muted
            if (!_isSystemAudioMuted)
            {
                _systemAudioBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
            }
        }

        private void MicrophoneCapture_DataAvailable(object sender, WaveInEventArgs e)
        {
             if (!_isRecording || _microphoneBuffer == null) return;

             // Calculate level BEFORE muting
             CalculateAndRaiseLevel(e.Buffer, e.BytesRecorded, _microphoneCapture.WaveFormat, ref _micAudioLevel, MicrophoneLevelChanged);

             // Add samples to buffer only if not muted
             if (!_isMicrophoneMuted)
             {
                 _microphoneBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
             }
        }

        // Phase 3: Helper method for level calculation
        private void CalculateAndRaiseLevel(byte[] buffer, int bytesRecorded, WaveFormat format, ref float currentLevelField, EventHandler<float> levelChangedEvent)
        {
            if (format == null || bytesRecorded == 0) return;

            float peakValue = 0; // Peak for this buffer
            float maxValue = 0; // Max sample value in this buffer

            try
            {
                int bytesPerSample = format.BitsPerSample / 8;
                int channels = format.Channels;

                if (bytesPerSample == 2) // 16-bit
                {
                    for (int i = 0; i < bytesRecorded; i += (bytesPerSample * channels))
                    {
                        if (i + 1 < bytesRecorded)
                        {
                            short sample = (short)((buffer[i + 1] << 8) | buffer[i]);
                            maxValue = Math.Max(maxValue, Math.Abs(sample));
                        }
                    }
                    peakValue = maxValue / 32768f; // Normalize
                }
                else if (bytesPerSample == 4 && format.Encoding == WaveFormatEncoding.IeeeFloat) // 32-bit float
                {
                    for (int i = 0; i < bytesRecorded; i += (bytesPerSample * channels))
                    {
                        if (i + 3 < bytesRecorded)
                        {
                            float sample = BitConverter.ToSingle(buffer, i);
                            maxValue = Math.Max(maxValue, Math.Abs(sample));
                        }
                    }
                    peakValue = maxValue; // Already normalized (0-1)
                }
                // Add other formats if needed

                // Apply scaling
                peakValue = Math.Min(1.0f, peakValue * 2.0f);

                // Apply smoothing (using the specific field for this source)
                currentLevelField = (_audioLevelSmoothingFactor * peakValue) + ((1 - _audioLevelSmoothingFactor) * currentLevelField);

                // Raise the specific event
                levelChangedEvent?.Invoke(this, currentLevelField);
            }
            catch (Exception ex)
            {
                 Logger.Error($"Error calculating audio level: {ex.Message}", ex);
                 // Don't raise event on error
            }
        }

        // Helper method to downsample audio file
        private string DownsampleAudioFile(string originalFilePath)
        {
            if (string.IsNullOrEmpty(originalFilePath) || !File.Exists(originalFilePath))
            {
                Logger.Error($"DownsampleAudioFileAsync: Original file not found or path empty: {originalFilePath}");
                return null;
            }

            string downsampledFilePath = Path.Combine(Path.GetTempPath(), $"{Path.GetFileNameWithoutExtension(originalFilePath)}_downsampled.wav");
            Logger.Info($"Downsampling: Original '{originalFilePath}' to '{downsampledFilePath}' at 16kHz, 16-bit Mono PCM.");

            try
            {
                using (var reader = new WaveFileReader(originalFilePath))
                {
                    // Step B: Convert to Mono (if needed) - Assuming input from mixer is stereo float
                    ISampleProvider monoProvider;
                    if (reader.WaveFormat.Channels == 2)
                    {
                        Logger.Info("Downsampling: Converting stereo to mono.");
                        var stereoSampleProvider = reader.ToSampleProvider();
                        monoProvider = new StereoToMonoSampleProvider(stereoSampleProvider);
                    }
                    else if (reader.WaveFormat.Channels == 1)
                    {
                        Logger.Info("Downsampling: Input is already mono.");
                        monoProvider = reader.ToSampleProvider();
                    }
                    else
                    {
                        Logger.Error($"DownsampleAudioFileAsync: Unsupported number of channels ({reader.WaveFormat.Channels}) in '{originalFilePath}'.");
                        return null;
                    }
                    
                    // Step C: Resample to 16000 Hz
                    Logger.Info($"Downsampling: Resampling from {monoProvider.WaveFormat.SampleRate} Hz to 16000 Hz.");
                    var resampler = new WdlResamplingSampleProvider(monoProvider, 16000);

                    // Step D: Convert to 16-bit PCM - SampleToWaveProvider16 handles this conversion.
                    Logger.Info("Downsampling: Preparing 16-bit PCM wave provider.");
                    var pcm16WaveProvider = new SampleToWaveProvider16(resampler);

                    // Step E: Write the Downsampled Audio to a New Temporary File
                    Logger.Info($"Downsampling: Writing to new file: {downsampledFilePath}");
                    WaveFileWriter.CreateWaveFile(downsampledFilePath, pcm16WaveProvider); // Use CreateWaveFile with IWaveProvider
                }

                Logger.Info($"Downsampling successful: '{downsampledFilePath}' created.");
                return downsampledFilePath;
            }
            catch (Exception ex)
            {
                Logger.Error($"DownsampleAudioFileAsync: Error during downsampling of '{originalFilePath}'.", ex);
                ErrorOccurred?.Invoke(this, new Exception($"Failed to downsample audio file {Path.GetFileName(originalFilePath)}: {ex.Message}", ex));
                // Clean up partially created downsampled file if it exists
                if (File.Exists(downsampledFilePath))
                {
                    try { File.Delete(downsampledFilePath); }
                    catch (Exception delEx) { Logger.Warning($"Failed to delete partial downsampled file '{downsampledFilePath}': {delEx.Message}"); }
                }
                return null; // Return null if downsampling failed
            }
        }

        private void ScheduleReinit(int delayMs = 350)
        {
            lock (_reinitLock)
            {
                _reinitTimer?.Dispose();
                _reinitTimer = new System.Threading.Timer(_ =>
                {
                    try
                    {
                        if (!_isRecording)
                        {
                            InitializeDevicesAndMixer();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Scheduled re-initialization failed.", ex);
                    }
                }, null, delayMs, System.Threading.Timeout.Infinite);
            }
        }

        // V2: This method now reads from the MIXER and writes to the file writer
        private void WriteMixerOutputToFile(int requiredBytes)
        {
             // Add extra null checks for safety within the loop
             if (!_isRecording || _writer == null || _mixer == null) return;

             try
             {
                 var buffer = new float[requiredBytes / 4]; // Assuming 32-bit float
                 int bytesRead = _mixer.Read(buffer, 0, buffer.Length);

                 if (bytesRead > 0)
                 {
                      var byteBuffer = new byte[bytesRead * 4];
                      Buffer.BlockCopy(buffer, 0, byteBuffer, 0, byteBuffer.Length);
                      _writer.Write(byteBuffer, 0, byteBuffer.Length);

                      // V2: Check chunk duration AFTER writing data
                      TimeSpan chunkElapsed = DateTime.Now - _chunkStartTime;
                      if (chunkElapsed.TotalSeconds >= _audioChunkSeconds)
                      {
                           // Use Interlocked with int: Attempt to change 0 to 1. If original was 0, proceed.
                           if (Interlocked.CompareExchange(ref _processingChunkFlag, 1, 0) == 0)
                           {
                                Task.Run(() => ProcessCurrentAudioChunk()); // Process chunk off-loop
                           }
                      }
                 }
             }
             catch (Exception ex)
             {
                  Logger.Error("Error writing mixer output to file", ex);
                  ErrorOccurred?.Invoke(this, ex);
             }
        }

        // V2: Background task to read from mixer and write to file
        private void ReadAndWriteLoop()
        {
             // Check essential components before starting loop
             if (_mixer == null || _systemAudioBuffer == null || _microphoneBuffer == null)
             {
                  Logger.Error("ReadAndWriteLoop cannot start: Mixer or buffers are null.");
                  _isRecording = false; // Stop recording if setup is invalid
                  ErrorOccurred?.Invoke(this, new InvalidOperationException("Mixer or buffers not initialized for write loop."));
                  return;
             }
             int bufferMilliseconds = 100;
             int requiredBytes = _mixer.WaveFormat.ConvertLatencyToByteSize(bufferMilliseconds);
             Logger.Info($"Starting ReadAndWriteLoop. Buffer size: {requiredBytes} bytes for {bufferMilliseconds}ms.");

             while (_isRecording) // Loop continues as long as recording is intended
             {
                 try
                 {
                      // Check the flag using integer comparison
                      if (Volatile.Read(ref _processingChunkFlag) == 1)
                      {
                           Task.Delay(10).Wait(); // Short pause while chunk is processed
                           continue; // Skip writing this iteration
                      }

                      if (_systemAudioBuffer?.BufferedBytes > 0 || _microphoneBuffer?.BufferedBytes > 0)
                      {
                           WriteMixerOutputToFile(requiredBytes);
                      }
                      else
                      {
                           Task.Delay(20).Wait(); // Prevent busy-waiting
                      }

                      // Update timer periodically
                      TimeSpan totalElapsed = DateTime.Now - _sessionStartTime;
                      RecordedDuration = totalElapsed;
                      if (totalElapsed.Milliseconds % 500 < 20)
                      {
                           RecordingTimeUpdate?.Invoke(this, totalElapsed);
                      }

                      // Removed chunk duration check from here - moved to WriteMixerOutputToFile
                 }
                 catch (Exception ex)
                 {
                      Logger.Error("Error in ReadAndWriteLoop", ex);
                      ErrorOccurred?.Invoke(this, ex);
                      break; // Exit loop on error
                 }
             }
             Logger.Info("Exited ReadAndWriteLoop.");
        }

        private void ProcessCurrentAudioChunk()
        {
            // Flag was set by Interlocked before Task.Run was called.

            string originalFileToProcess = _tempFilePath; // Store original path
            WaveFileWriter oldWriter = _writer;
            _writer = null; // Prevent loop from accidentally using old writer

            // Close the old writer
            if (oldWriter != null)
            {
                Logger.Info($"Closing current audio chunk file: {originalFileToProcess}");
                try { oldWriter.Close(); } catch (Exception ex) { Logger.Error($"Error closing writer for chunk {originalFileToProcess}", ex); }
            }
            else
            {
                 Logger.Warning("ProcessCurrentAudioChunk called but oldWriter was null.");
            }

            // Create new file AFTER closing old one
            if (_isRecording)
            {
                 CreateNewAudioFileInternal(); // Creates new _writer and _tempFilePath for the next chunk
            }

            Volatile.Write(ref _processingChunkFlag, 0); // Clear the flag using Volatile write
            Logger.Info("ProcessCurrentAudioChunk: Cleared flag.");

            // Process the completed chunk
            if (!string.IsNullOrEmpty(originalFileToProcess) && File.Exists(originalFileToProcess))
            {
                Logger.Info($"Queueing chunk for downsampling and transcription: {originalFileToProcess}");
                Task.Run(async () =>
                {
                    string fileToTranscribe = null;
                    string downsampledFilePath = null;
                    try
                    {
                        // Step 1 & 3: Downsample before sending to Whisper
                        downsampledFilePath = await Task.Run(() => DownsampleAudioFile(originalFileToProcess));

                        if (string.IsNullOrEmpty(downsampledFilePath))
                        {
                            Logger.Error($"Downsampling failed for chunk: {originalFileToProcess}. Transcription will be skipped.");
                            ErrorOccurred?.Invoke(this, new Exception($"Downsampling failed for {Path.GetFileName(originalFileToProcess)}."));
                            return; // Exit if downsampling failed
                        }
                        fileToTranscribe = downsampledFilePath;

                        string statusMsg = $"Transcribing chunk {Path.GetFileName(fileToTranscribe)}...";
                        StatusChanged?.Invoke(this, statusMsg);
                        
                        string transcriptionText = await _transcriptionService.TranscribeAudioFileAsync(fileToTranscribe); // Send downsampled file
                        TranscriptionReceived?.Invoke(this, transcriptionText);
                       
                        // The transcription service should handle deletion of its input file (the downsampled one)
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error processing chunk {originalFileToProcess}", ex);
                        ErrorOccurred?.Invoke(this, ex);
                    }
                    finally
                    {
                        // Clean up original chunk file
                        if (File.Exists(originalFileToProcess))
                        {
                            try { File.Delete(originalFileToProcess); }
                            catch (Exception delEx) { Logger.Warning($"Failed to delete original chunk file '{originalFileToProcess}': {delEx.Message}"); }
                        }
                    }
                });
            }
            else
            {
                Logger.Warning($"ProcessCurrentAudioChunk: Original file to process not found: {originalFileToProcess}");
            }
        }

        // V2: Create file based on MIXER format (Internal version)
        private void CreateNewAudioFileInternal()
        {
             if (_mixer == null)
             {
                  Logger.Error("Cannot create new audio file: Mixer is not initialized.");
                  return;
             }
             try
             {
                 _tempFilePath = Path.Combine(Path.GetTempPath(), $"audio_chunk_{DateTime.Now.Ticks}.wav");
                 _writer = new WaveFileWriter(_tempFilePath, _mixer.WaveFormat);
                 _chunkStartTime = DateTime.Now;
                 Logger.Info($"Created new audio chunk file: {_tempFilePath} with format {_mixer.WaveFormat}");
             }
             catch (Exception ex)
             {
                 Logger.Error("Failed to create new audio chunk file.", ex);
                 ErrorOccurred?.Invoke(this, ex);
                 StopRecording(); // Stop recording if file creation fails
             }
        }

        // V2: Create file and start background writing task
        private void CreateNewAudioFile()
        {
             CreateNewAudioFileInternal();
             // Start the write loop task only if it's not already running or has completed
             if (_isRecording && _writer != null && (_writeLoopTask == null || _writeLoopTask.IsCompleted))
             {
                  Logger.Info("Starting ReadAndWriteLoop task.");
                  _writeLoopTask = Task.Run(() => ReadAndWriteLoop());
             }
        }

        // V2: Shared stop handler
        private void Capture_RecordingStopped(object sender, StoppedEventArgs e)
        {
             bool isSystemCapture = sender == _systemAudioCapture;
             Logger.Info($"Capture_RecordingStopped event received. SystemCapture? {isSystemCapture}");

             if (e.Exception != null)
             {
                 Logger.Error($"Recording stopped with exception (SystemCapture? {isSystemCapture}).", e.Exception);
                 ErrorOccurred?.Invoke(this, e.Exception);
                 StopRecording(); // Trigger main stop logic
             }
        }

        // V2: Centralized final chunk processing logic
        private void ProcessFinalChunk()
        {
            // Flag was set by Interlocked before Task.Run was called.

            string originalFileToProcess = _tempFilePath; // Store original path
            WaveFileWriter oldWriter = _writer;
            _writer = null; // Prevent loop from accidentally using old writer

            // Close the old writer
            if (oldWriter != null)
            {
                Logger.Info($"Closing current audio chunk file: {originalFileToProcess}");
                try { oldWriter.Close(); } catch (Exception ex) { Logger.Error($"Error closing writer for chunk {originalFileToProcess}", ex); }
            }
            else
            {
                 Logger.Warning("ProcessCurrentAudioChunk called but oldWriter was null.");
            }

            // Create new file AFTER closing old one
            if (_isRecording)
            {
                 CreateNewAudioFileInternal(); // Creates new _writer and _tempFilePath for the next chunk
            }

            Volatile.Write(ref _processingChunkFlag, 0); // Clear the flag using Volatile write
            Logger.Info("ProcessCurrentAudioChunk: Cleared flag.");

            // Process the completed chunk
            if (!string.IsNullOrEmpty(originalFileToProcess) && File.Exists(originalFileToProcess))
            {
                Logger.Info($"Queueing chunk for downsampling and transcription: {originalFileToProcess}");
                Task.Run(async () =>
                {
                    string fileToTranscribe = null;
                    string downsampledFilePath = null;
                    try
                    {
                        // Step 1 & 3: Downsample before sending to Whisper
                        downsampledFilePath = await Task.Run(() => DownsampleAudioFile(originalFileToProcess));

                        if (string.IsNullOrEmpty(downsampledFilePath))
                        {
                            Logger.Error($"Downsampling failed for chunk: {originalFileToProcess}. Transcription will be skipped.");
                            ErrorOccurred?.Invoke(this, new Exception($"Downsampling failed for {Path.GetFileName(originalFileToProcess)}."));
                            return; // Exit if downsampling failed
                        }
                        fileToTranscribe = downsampledFilePath;

                        string statusMsg = $"Transcribing chunk {Path.GetFileName(fileToTranscribe)}...";
                        StatusChanged?.Invoke(this, statusMsg);
                        
                        string transcriptionText = await _transcriptionService.TranscribeAudioFileAsync(fileToTranscribe); // Send downsampled file
                        TranscriptionReceived?.Invoke(this, transcriptionText);
                       
                        // The transcription service should handle deletion of its input file (the downsampled one)
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error processing chunk {originalFileToProcess}", ex);
                        ErrorOccurred?.Invoke(this, ex);
                    }
                    finally
                    {
                        // Clean up original chunk file
                        if (File.Exists(originalFileToProcess))
                        {
                            try { File.Delete(originalFileToProcess); }
                            catch (Exception delEx) { Logger.Warning($"Failed to delete original chunk file '{originalFileToProcess}': {delEx.Message}"); }
                        }
                    }
                });
            }
            else
            {
                Logger.Warning($"ProcessCurrentAudioChunk: Original file to process not found: {originalFileToProcess}");
            }
        }

        // V2: Refined StopRecording
        public void StopRecording()
        {
             Logger.Info("Attempting to stop recording.");
             if (!_isRecording)
             {
                 Logger.Info("StopRecording called but not currently recording.");
                 return;
             }

             _isRecording = false; // Set flag immediately

             // Wait for the write loop task to complete if it's running
             if (_writeLoopTask != null && !_writeLoopTask.IsCompleted)
             {
                  Logger.Info("Waiting for ReadAndWriteLoop task to complete...");
                  // Add a timeout to prevent indefinite waiting
                  bool completed = _writeLoopTask.Wait(TimeSpan.FromSeconds(1)); // Wait up to 1 second
                  if (!completed)
                  {
                       Logger.Warning("ReadAndWriteLoop task did not complete within timeout during stop.");
                  }
                  else
                  {
                       Logger.Info("ReadAndWriteLoop task completed.");
                  }
             }
             _writeLoopTask = null; // Clear the task reference

             Logger.Info("Stopping System Audio Capture...");
             _systemAudioCapture?.StopRecording();
             Logger.Info("Stopping Microphone Capture...");
             _microphoneCapture?.StopRecording();

             ProcessFinalChunk(); // Process whatever was last written

             // Reset levels - Phase 3
             SystemAudioLevelChanged?.Invoke(this, 0);
             MicrophoneLevelChanged?.Invoke(this, 0);

             StatusChanged?.Invoke(this, "Processing complete.");
        }

        // V2: Helper to dispose all capture and mixer related objects
        private void DisposeCurrentCapturesAndMixer()
        {
             Logger.Info("Disposing current captures and mixer resources...");
             try
             {
                 _systemAudioCapture?.Dispose();
                 _systemAudioCapture = null;

                 _microphoneCapture?.Dispose();
                 _microphoneCapture = null;

                 _mixer = null;
                 _systemAudioBuffer = null;
                 _microphoneBuffer = null;

                 if (_writer != null)
                 {
                     _writer.Close();
                     _writer.Dispose();
                     _writer = null;
                 }
             }
             catch (Exception ex)
             {
                 Logger.Error("Error disposing captures and mixer resources.", ex);
                 ErrorOccurred?.Invoke(this, ex);
             }
             finally { _tempFilePath = null; }
        }

        public void Dispose()
        {
            Logger.Info("Disposing AudioCaptureService resources.");
            DisposeCurrentCapturesAndMixer();
        }

        // Helper class for comparing WaveFormats
        private static class WaveFormatComparer
        {
            public static bool AreEqual(WaveFormat a, WaveFormat b)
            {
                if (a == null || b == null) return false;
                return a.SampleRate == b.SampleRate &&
                       a.Channels == b.Channels &&
                       a.BitsPerSample == b.BitsPerSample &&
                       a.Encoding == b.Encoding;
            }
        }
    }
}
