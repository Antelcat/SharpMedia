using SharpDX.MediaFoundation;

namespace Antelcat.Media.Windows.Abstractions.Interfaces;

public interface IMediaFoundationPacket
{
    Sample Sample { get; }
}