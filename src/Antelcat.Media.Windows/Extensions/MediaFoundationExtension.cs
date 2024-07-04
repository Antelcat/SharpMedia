using System.Diagnostics;
using System.Reflection;
using Antelcat.Media.Abstractions.Enums;
using Antelcat.Media.Abstractions.Extensions;
using EasyPathology.Abstractions.DataTypes;
using SharpDX.MediaFoundation;

namespace Antelcat.Media.Windows.Extensions;

internal static class MediaFoundationExtension
{
    public static void Unpack(this long l, out uint i0, out uint i1)
    {
        i0 = (uint)(l >> 32);
        i1 = (uint)(l & (long.MaxValue >> 32));
    }

    public static long Pack(this (uint, uint) pair)
    {
        return (long)pair.Item1 << 32 | pair.Item2;
    }

    public static long Pack(this Fraction fraction)
    {
        return (long)fraction.Number << 32 | fraction.Denominator;
    }

    public static VideoFrameFormat ToVideoFrameFormat(this Guid guid)
    {
        return guid.ToString() switch
        {
            "32315659-0000-0010-8000-00aa00389b71" => VideoFrameFormat.Yv12,
            "3231564e-0000-0010-8000-00aa00389b71" => VideoFrameFormat.NV12,
            "32595559-0000-0010-8000-00aa00389b71" => VideoFrameFormat.YUY2,
            "00000014-0000-0010-8000-00aa00389b71" => VideoFrameFormat.RGB24,
            "47504a4d-0000-0010-8000-00aa00389b71" => VideoFrameFormat.MJPG,
            _ => VideoFrameFormat.Unset
        };
    }

    public static double ToDouble(this Ratio ratio)
    {
        return (double)ratio.Numerator / ratio.Denominator;
    }

    public static Fraction ToFraction(this Ratio ratio)
    {
        return new Fraction((uint)ratio.Numerator, (uint)ratio.Denominator);
    }

    private static readonly Dictionary<Guid, MediaAttributeKey> KeyMap;
    private static readonly Dictionary<Guid, string> GuidMap;

    static MediaFoundationExtension()
    {
        KeyMap = typeof(MediaTypeAttributeKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType.IsSubclassOf(typeof(MediaAttributeKey)))
            .Select(f => f.GetValue(null))
            .Cast<MediaAttributeKey>()
            .ToDictionary(k => k.Guid);

        GuidMap = new Dictionary<Guid, string>();
        var guidTypes = new[]
        {
            typeof(MediaTypeGuids),
            typeof(AudioFormatGuids),
            typeof(TranscodeContainerTypeGuids),
            typeof(TransformCategoryGuids),
            typeof(VideoFormatGuids)
        };
        foreach (var fieldInfo in guidTypes.Select(t => t
                     .GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(f => f.FieldType == typeof(Guid)))
                     .SelectMany(x => x))
        {
            GuidMap[fieldInfo.GetValue(null).NotNull<Guid>()] = fieldInfo.Name;
        }
    }

    public static void Dump(this MediaType mediaType)
    {
        for (var i = 0; i < mediaType.Count; i++)
        {
            var obj = mediaType.GetByIndex(i, out var guid);
            if (KeyMap.TryGetValue(guid, out var key))
            {
                if (obj is Guid guidObj && GuidMap.TryGetValue(guidObj, out var guidName))
                {
                    obj = guidName;
                }

                Debug.WriteLine($"{guid}: {key.Name} = {obj}");
            }
        }
    }
}