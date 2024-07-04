using SharpDX.MediaFoundation;

namespace Antelcat.Media.Windows.Abstractions.Interfaces;

public interface IMediaFoundationEncoder {
	MediaType InputMediaType { get; }

	MediaType OutputMediaType { get; }
}