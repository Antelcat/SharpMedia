using System.Threading;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Interfaces;

namespace Antelcat.Media.Modifiers;

/// <summary>
/// 低通滤波器
/// </summary>
public class LowPassFilterModifier : IAudioModifier {
	public AudioFrameFormat? TargetFormat => null;

	private short prevSample;

	public void Open(AudioInputDevice device, AudioFrameFormat srcFormat) {
		prevSample = 0;
	}

	public RawAudioFrame ModifyFrame(AudioInputDevice device, RawAudioFrame frame, CancellationToken cancellationToken) {
		if (frame.Format.BitsPerSample != 8) {
			return frame;
		}

		var span = frame.AsSpan<short>();
		for (var i = 0; i < span.Length; i++) {
			prevSample = span[i] = (short)((span[i] + prevSample * 7) >> 3);
		}

		return frame;
	}

	public void Close(AudioInputDevice device) { }
}