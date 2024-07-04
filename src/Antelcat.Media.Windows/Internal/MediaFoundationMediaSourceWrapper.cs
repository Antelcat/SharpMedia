using Antelcat.Media.Abstractions;
using SharpDX.MediaFoundation;

namespace Antelcat.Media.Windows.Internal;

internal class MediaFoundationMediaSourceWrapper : IDisposable
{
    public MediaType? MediaType { get; }

    public MediaSource? MediaSource { get; private set; }

    private readonly string uid;
    private readonly MediaDeviceType type;

    internal MediaFoundationMediaSourceWrapper(string uid, MediaType mediaType, MediaDeviceType type)
    {
        MediaType = mediaType;
        this.uid = uid;
        this.type = type;
    }

    ~MediaFoundationMediaSourceWrapper()
    {
        Dispose();
    }

    public void Open()
    {
        using var collector = new DisposeCollector();
        var attributes = collector.Add(new MediaAttributes());

        var sourceType = type switch
        {
            MediaDeviceType.Camera => CaptureDeviceAttributeKeys.SourceTypeVideoCapture.Guid,
            MediaDeviceType.Microphone => CaptureDeviceAttributeKeys.SourceTypeAudioCapture.Guid,
            _ => throw new NotSupportedException()
        };
        attributes.Set(CaptureDeviceAttributeKeys.SourceType, sourceType);

        var guidKey = type switch
        {
            MediaDeviceType.Camera => CaptureDeviceAttributeKeys.SourceTypeVidcapSymbolicLink,
            MediaDeviceType.Microphone => CaptureDeviceAttributeKeys.SourceTypeAudcapEndpointId,
            _ => throw new NotSupportedException()
        };
        attributes.Set(guidKey, uid);

        MediaFactory.CreateDeviceSource(attributes, out var mediaSource);
        MediaSource = mediaSource;
        mediaSource.CreatePresentationDescriptor(out var pDesc);
        collector.Add(pDesc);
        pDesc.SelectStream(0);
        pDesc.GetStreamDescriptorByIndex(0, out _, out var sDesc);
        collector.Add(sDesc);
        var handler = collector.Add(sDesc.MediaTypeHandler);
        handler.CurrentMediaType = MediaType;
    }

    public void Close()
    {
        if (MediaSource != null)
        {
            MediaSource.Dispose();
            MediaSource = null;
        }
    }

    public void Dispose()
    {
        MediaSource?.Dispose();
        MediaType?.Dispose();
        GC.SuppressFinalize(this);
    }
}