using System.Diagnostics;
using System.Reflection;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using Antelcat.Media.Abstractions.Extensions;
using Antelcat.Media.Windows.Extensions;
using Antelcat.Media.Windows.InputDevices.Audio;
using Antelcat.Media.Windows.InputDevices.Video;
using Antelcat.Media.Windows.Internal;
using EasyPathology.Abstractions.DataTypes;
using NAudio.CoreAudioApi;
using SharpDX.MediaFoundation;

namespace Antelcat.Media.Windows;

/// <summary>
/// 管理摄像头、麦克风的类
/// </summary>
public class InputDeviceManager : IInputDeviceManager
{
    private static MethodInfo? enumDeviceSourcesMethodInfo;

    private static Activate[] EnumDeviceSources(MediaAttributes attributesRef)
    {
        // 谢谢你，SharpDX
        enumDeviceSourcesMethodInfo ??= typeof(MediaFactory).GetMethod(
            "EnumDeviceSources",
            BindingFlags.Static | BindingFlags.NonPublic).NotNull();
        var args = new object?[] { attributesRef, null, null };
        enumDeviceSourcesMethodInfo.Invoke(null, args);
        var devicePtr = args[1].NotNull<IntPtr>();
        var devicesCount = args[2].NotNull<int>();

        var result = new Activate[devicesCount];

        unsafe
        {
            var address = (void**)devicePtr;
            for (var i = 0; i < devicesCount; i++)
            {
                result[i] = new Activate(new IntPtr(address[i]));
            }
        }

        return result;
    }

    public IEnumerable<MediaDeviceInformation> GetMicrophones()
    {
        using var attributes = new MediaAttributes();
        attributes.Set(CaptureDeviceAttributeKeys.SourceType, CaptureDeviceAttributeKeys.SourceTypeAudioCapture.Guid);
        foreach (var activate in EnumDeviceSources(attributes))
        {
            using (activate)
            {
                string uid, friendlyName;

                try
                {
                    uid = activate.Get(CaptureDeviceAttributeKeys.SourceTypeAudcapEndpointId);
                    friendlyName = activate.Get(CaptureDeviceAttributeKeys.FriendlyName);
                }
                catch
                {
                    continue;
                }

                yield return new MediaDeviceInformation(MediaDeviceType.Microphone, uid, friendlyName);
            }
        }
    }

    public IEnumerable<MediaDeviceInformation> GetCameras()
    {
        using var attributes = new MediaAttributes();
        attributes.Set(CaptureDeviceAttributeKeys.SourceType, CaptureDeviceAttributeKeys.SourceTypeVideoCapture.Guid);
        foreach (var activate in EnumDeviceSources(attributes))
        {
            using (activate)
            {
                string uid, friendlyName;

                try
                {
                    uid = activate.Get(CaptureDeviceAttributeKeys.SourceTypeVidcapSymbolicLink);
                    friendlyName = activate.Get(CaptureDeviceAttributeKeys.FriendlyName);
                }
                catch
                {
                    continue;
                }

                yield return new MediaDeviceInformation(MediaDeviceType.Camera, uid, friendlyName);
            }
        }
    }

    public IEnumerable<MediaDeviceInformation> GetSpeakers()
    {
        using var deviceEnumerator = new MMDeviceEnumerator();
        var devices = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        foreach (var device in devices)
        {
            yield return new MediaDeviceInformation(MediaDeviceType.Speaker, device.ID, device.FriendlyName);
        }
    }

    public MediaDeviceInformation? GetDefaultMicrophone()
    {
        var defaultAudio = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
        if (defaultAudio != null)
        {
            return GetMicrophones().FirstOrDefault(d => d.Uid == defaultAudio.ID);
        }

        return null;
    }

    public MediaDeviceInformation GetDefaultSpeaker()
    {
        using var deviceEnumerator = new MMDeviceEnumerator();
        var device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        return new MediaDeviceInformation(MediaDeviceType.Speaker, device.ID, device.FriendlyName);
    }

    public AudioInputDevice CreateMicrophone(MediaDeviceInformation information, AudioFrameFormat waveFormat)
    {
        if (information.Type != MediaDeviceType.Microphone)
        {
            throw new ArgumentException(nameof(information.Type), nameof(information));
        }

        // 提取出information.Uid从后往前的第一个GUID
        var guidStartIndex = information.Uid.LastIndexOf('{');
        var guidEndIndex = information.Uid.IndexOf('}', guidStartIndex) + 1;
        if (guidStartIndex == -1 ||
            guidEndIndex == -1 ||
            !Guid.TryParse(information.Uid.AsSpan(guidStartIndex, guidEndIndex - guidStartIndex), out var guid))
        {
            throw new ArgumentException("Invalid GUID");
        }

        return new DirectSoundAudioInputDevice(guid, waveFormat);
    }

    public VideoInputDevice? CreateCamera(MediaDeviceInformation information, VideoInputDevice.CreatePreference createPreference)
    {
        if (information.Type != MediaDeviceType.Camera)
        {
            throw new ArgumentException(nameof(information.Type), nameof(information));
        }

        using var collector = new DisposeCollector();
        var attributes = collector.Add(new MediaAttributes());
        attributes.Set(CaptureDeviceAttributeKeys.SourceType, CaptureDeviceAttributeKeys.SourceTypeVideoCapture.Guid);
        attributes.Set(CaptureDeviceAttributeKeys.SourceTypeVidcapSymbolicLink, information.Uid);

        MediaManager.Startup();
        MediaFactory.CreateDeviceSource(attributes, out var mediaSource);
        collector.Add(mediaSource);

        mediaSource.CreatePresentationDescriptor(out var pDesc);
        collector.Add(pDesc);
        pDesc.GetStreamDescriptorByIndex(0, out _, out var sDesc);
        collector.Add(sDesc);

        var satisfiedMediaTypeBags = new List<VideoMediaTypeBag>();
        var handler = collector.Add(sDesc.MediaTypeHandler);
        var count = handler.MediaTypeCount;
        for (var i = 0; i < count; i++)
        {
            var mediaType = handler.GetMediaTypeByIndex(i);

            var frameFormat = mediaType.Get(MediaTypeAttributeKeys.Subtype).ToVideoFrameFormat();
            if (createPreference.DesiredFormat != VideoFrameFormat.Unset && frameFormat != createPreference.DesiredFormat)
            {
                mediaType.Dispose();
                continue; // 不符合要求
            }

            mediaType.Get(MediaTypeAttributeKeys.FrameSize).Unpack(out var frameWidth, out var frameHeight);
            if (frameWidth < createPreference.DesiredFrameWidth || frameHeight < createPreference.DesiredFrameHeight)
            {
                mediaType.Dispose();
                continue; // 不符合要求
            }

            mediaType.Get(MediaTypeAttributeKeys.FrameRate).Unpack(out var frameRateNumber, out var frameRateDenominator);
            var frameRate = new Fraction(frameRateNumber, frameRateDenominator);
            if (frameRate.ToDouble() < createPreference.DesiredFrameRate.ToDouble())
            {
                mediaType.Dispose();
                continue; // 不符合要求
            }

            // 添加符合要求的，在之后进行筛选
            satisfiedMediaTypeBags.Add(new VideoMediaTypeBag(mediaType, (int)frameWidth, (int)frameHeight, frameRate, frameFormat)); 
        }

        if (satisfiedMediaTypeBags.Count == 0)
        {
            return null;
        }

        satisfiedMediaTypeBags = satisfiedMediaTypeBags
            .OrderBy(static b => b.Width * b.Height)
            .ThenBy(static b => b.FrameRate.ToDouble()).ToList();

#if DEBUG
        for (var i = 0; i < satisfiedMediaTypeBags.Count; i++)
        {
            var bag = satisfiedMediaTypeBags[i];
            Debug.WriteLine($"{(i == 0 ? "→ " : "  ")}{bag.Width}x{bag.Height}@{bag.FrameRate.ToDouble()}fps {bag.Format}");
        }
#endif

        foreach (var type in satisfiedMediaTypeBags.Skip(1))
        {
            type.Type.Dispose();
        }

        return new MediaFoundationVideoInputDevice(information.Uid, satisfiedMediaTypeBags[0]);
    }
}