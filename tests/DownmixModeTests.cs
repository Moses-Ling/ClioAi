using Microsoft.VisualStudio.TestTools.UnitTesting;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using AudioTranscriptionApp.Services;

namespace AudioTranscriptionApp.Tests
{
    [TestClass]
    public class DownmixModeTests
    {
        private class TestMultiChannelProvider : ISampleProvider
        {
            private readonly WaveFormat _format;
            private float _val;
            public TestMultiChannelProvider(int sampleRate, int channels)
            {
                _format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
                _val = 0f;
            }
            public WaveFormat WaveFormat => _format;
            public int Read(float[] buffer, int offset, int count)
            {
                // Fill frames with incremental values per channel for deterministic assertions
                int channels = _format.Channels;
                int frames = count / channels;
                int written = frames * channels;
                for (int f = 0; f < frames; f++)
                {
                    for (int c = 0; c < channels; c++)
                    {
                        buffer[offset + f * channels + c] = _val + c; // channel c contributes +c
                    }
                    _val += 1f; // next frame
                }
                return written;
            }
        }

        [TestMethod]
        public void AverageDownmix_TwoChannel_AveragesChannels()
        {
            var src = new TestMultiChannelProvider(48000, 2);
            var avg = new AudioCaptureService.DownmixToMonoSampleProvider(src);
            var outBuf = new float[20];
            int read = avg.Read(outBuf, 0, 10); // 10 mono samples
            Assert.AreEqual(10, read);
            // Frame 0: channels 0,1 -> (0 + 1)/2 = 0.5
            // Frame 1: channels 0,1 -> (1 + 2)/2 = 1.5, etc.
            Assert.AreEqual(0.5f, outBuf[0], 1e-6f);
            Assert.AreEqual(1.5f, outBuf[1], 1e-6f);
            Assert.AreEqual(2.5f, outBuf[2], 1e-6f);
        }

        [TestMethod]
        public void FirstChannel_TwoChannel_PicksChannelZero()
        {
            var src = new TestMultiChannelProvider(48000, 2);
            var first = new AudioCaptureService.SelectChannelSampleProvider(src, 0);
            var outBuf = new float[20];
            int read = first.Read(outBuf, 0, 10);
            Assert.AreEqual(10, read);
            // Frame 0: ch0 = 0
            // Frame 1: ch0 = 1, Frame 2: ch0 = 2
            Assert.AreEqual(0f, outBuf[0], 1e-6f);
            Assert.AreEqual(1f, outBuf[1], 1e-6f);
            Assert.AreEqual(2f, outBuf[2], 1e-6f);
        }

        [TestMethod]
        public void AverageDownmix_FourChannel_AveragesAll()
        {
            var src = new TestMultiChannelProvider(48000, 4);
            var avg = new AudioCaptureService.DownmixToMonoSampleProvider(src);
            var outBuf = new float[8];
            int read = avg.Read(outBuf, 0, 4);
            Assert.AreEqual(4, read);
            // Frame 0: (0+1+2+3)/4 = 1.5
            // Frame 1: (1+2+3+4)/4 = 2.5
            Assert.AreEqual(1.5f, outBuf[0], 1e-6f);
            Assert.AreEqual(2.5f, outBuf[1], 1e-6f);
        }
    }
}

