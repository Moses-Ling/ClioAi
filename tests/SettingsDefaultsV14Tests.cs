using Microsoft.VisualStudio.TestTools.UnitTesting;
using AudioTranscriptionApp.Properties;

namespace AudioTranscriptionApp.Tests
{
    [TestClass]
    public class SettingsDefaultsV14Tests
    {
        [TestMethod]
        public void Transcription_Local_Defaults()
        {
            Assert.IsFalse(Settings.Default.TranscriptionUseLocal);
            Assert.AreEqual("http://localhost:5042", Settings.Default.TranscriptionLocalHost);
            Assert.AreEqual("/v1/audio/transcriptions", Settings.Default.TranscriptionLocalPath);
            Assert.AreEqual("whisper-base", Settings.Default.TranscriptionLocalModel);
        }

        [TestMethod]
        public void Cleanup_Local_Defaults()
        {
            Assert.IsFalse(Settings.Default.CleanupUseLocal);
            Assert.AreEqual("http://localhost:1234", Settings.Default.CleanupLocalHost);
            Assert.AreEqual("/v1/chat/completions", Settings.Default.CleanupLocalPath);
            Assert.AreEqual("granite-3.1-8b-instruct", Settings.Default.CleanupLocalModel);
        }

        [TestMethod]
        public void Summarize_Local_Defaults()
        {
            Assert.IsFalse(Settings.Default.SummarizeUseLocal);
            Assert.AreEqual("http://localhost:1234", Settings.Default.SummarizeLocalHost);
            Assert.AreEqual("/v1/chat/completions", Settings.Default.SummarizeLocalPath);
            Assert.AreEqual("granite-3.1-8b-instruct", Settings.Default.SummarizeLocalModel);
        }

        [TestMethod]
        public void Export_AppendFooter_DefaultTrue()
        {
            Assert.IsTrue(Settings.Default.ExportAppendFooter);
        }
    }
}

