using NAudio.CoreAudioApi;

namespace AudioTranscriptionApp.Models
{
    public class AudioDeviceModel
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public MMDevice Device { get; set; }
        public bool IsInput { get; set; } // Added to distinguish Mic (true) from System Output (false)
    }
}
