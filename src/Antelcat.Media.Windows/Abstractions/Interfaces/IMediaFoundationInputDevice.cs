using SharpDX.MediaFoundation;

namespace Antelcat.Media.Windows.Abstractions.Interfaces;

public interface IMediaFoundationInputDevice {
	public MediaType? MediaType { get; }

	public MediaSource? MediaSource { get; }

	public bool IsReady { get; }

	public long AverageBytesPerSecond { get; }
}