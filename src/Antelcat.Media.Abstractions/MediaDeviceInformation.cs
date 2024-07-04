namespace Antelcat.Media.Abstractions;

/// <summary>
/// 描述一个多媒体设备
/// </summary>
public record MediaDeviceInformation(MediaDeviceType Type, string Uid, string FriendlyName)
{
    public override string ToString()
    {
        return FriendlyName;
    }
}

public enum MediaDeviceType
{
    Unknown,
    Camera,
    Microphone,
    Speaker
}