using Antelcat.Media.Abstractions;
using Antelcat.Media.Windows.Extensions;
using SharpDX.Direct3D11;

namespace Antelcat.Media.Windows.Internal;

internal class D3D11TextureVideoFrame(Texture2D texture)
    : RawVideoFrame(
        texture.Description.Width, 
        texture.Description.Height, 
        1, 
        texture.NativePointer, 
        texture.Description.Format.ToVideoFrameFormat())
{
    public Texture2D Texture2D { get; } = texture;

    public override int Pitch => (Width * 32 + 7) / 8;
}