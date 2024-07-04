using Antelcat.Media.Abstractions;
using SharpDX.Multimedia;

namespace Antelcat.Media.Windows.Extensions;

internal static class DirectSoundExtension {
	public static WaveFormat ToWaveFormat(this AudioFrameFormat frameFormat) {
		return new WaveFormat(frameFormat.SampleRate, frameFormat.BitsPerSample, frameFormat.ChannelCount);
	}
}