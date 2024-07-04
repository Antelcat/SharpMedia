using Antelcat.Media.Abstractions;
using Antelcat.Media.Windows.Abstractions;
using SharpDX.MediaFoundation;

namespace Antelcat.Media.Windows.Internal;

/// <summary>
/// 封装MFTransform
/// </summary>
internal class MediaFoundationTransformWrapper : IDisposable
{
    private readonly Transform transform;

    public MediaFoundationTransformWrapper(Guid transformCategory, MediaType inputMediaType, MediaType outputMediaType)
    {
        var inputTypeRef = new TRegisterTypeInformation
        {
            GuidMajorType = inputMediaType.Get(MediaTypeAttributeKeys.MajorType),
            GuidSubtype = inputMediaType.Get(MediaTypeAttributeKeys.Subtype)
        };
        var outputTypeRef = new TRegisterTypeInformation
        {
            GuidMajorType = outputMediaType.Get(MediaTypeAttributeKeys.MajorType),
            GuidSubtype = outputMediaType.Get(MediaTypeAttributeKeys.Subtype)
        };
        var clsIds = new Guid[1];
        MediaFactory.TEnum(transformCategory, 0, inputTypeRef, outputTypeRef, null, clsIds, out var count);
        if (count == 0)
        {
            throw new NotSupportedException();
        }

        //var guid = activates[0].Get(TransformAttributeKeys.MftTransformClsidAttribute);
        //var name = activates[0].Get(TransformAttributeKeys.MftFriendlyNameAttribute);
        transform = new Transform(clsIds[0]);
        transform.SetInputType(0, inputMediaType, 0);
        transform.SetOutputType(0, outputMediaType, 0);
    }

    ~MediaFoundationTransformWrapper()
    {
        Dispose();
    }

    private void EncodeFrame(RawFrame frame)
    {
        using var buffer = MediaFactory.CreateMemoryBuffer((int)frame.Length);
        var bufferPtr = buffer.Lock(out _, out _);
        unsafe
        {
            Buffer.MemoryCopy(frame.Data.ToPointer(), bufferPtr.ToPointer(), frame.Length, frame.Length);
        }
        buffer.Unlock();
        using var sample = MediaFactory.CreateSample();
        sample.AddBuffer(buffer);
        transform.ProcessInput(0, sample, 0);
    }

    public MediaFoundationVideoPacket EncodeVideoFrame(RawVideoFrame frame, EncodedVideoFormat format)
    {
        EncodeFrame(frame);
        var outputDataBuffer = new TOutputDataBuffer[1];
        transform.ProcessOutput(TransformProcessOutputFlags.None, outputDataBuffer, out _);
        return new MediaFoundationVideoPacket(outputDataBuffer[0].PSample, format);
    }

    public void Dispose()
    {
        transform.Dispose();
        GC.SuppressFinalize(this);
    }
}