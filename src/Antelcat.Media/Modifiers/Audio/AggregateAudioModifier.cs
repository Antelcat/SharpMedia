using System.Linq;
using System.Threading;
using Antelcat.Media.Abstractions;

namespace Antelcat.Media.Modifiers;

public class AggregateAudioModifier(params IAudioModifier[] modifiers) : IAudioModifier {
	public AudioFrameFormat? TargetFormat {
		get {
			var i = modifiers.Length - 1;
			var format = modifiers[i].TargetFormat;
			while (format == null && i > 0) {
				i--;
				format = modifiers[i].TargetFormat;
			}

			return format;
		}
	}

	public void Open(AudioInputDevice device, AudioFrameFormat srcFormat) {
		foreach (var modifier in modifiers) {
			modifier.Open(device, srcFormat);
			if (modifier.TargetFormat != null) {
				srcFormat = modifier.TargetFormat;
			}
		}
	}

	public RawAudioFrame ModifyFrame(AudioInputDevice device, RawAudioFrame frame, CancellationToken cancellationToken) {
		// ReSharper disable once LoopCanBeConvertedToQuery
		foreach (var modifier in modifiers) {
			frame = modifier.ModifyFrame(device, frame, cancellationToken);
		}

		return frame;
	}

	public void Close(AudioInputDevice device) {
		foreach (var modifier in modifiers) {
			modifier.Close(device);
		}
	}
}

public static class AggregateAudioModifierExtension {
	public static void AggregateModifier(this AudioInputDevice device, params IAudioModifier[] modifiers) {
		if (device.Modifier == null) {
			device.Modifier = modifiers.Length == 1 ? modifiers[0] : new AggregateAudioModifier(modifiers);
		} else {
			device.Modifier = new AggregateAudioModifier(new[] {
				device.Modifier
			}.Concat(modifiers).ToArray());
		}
	}
}