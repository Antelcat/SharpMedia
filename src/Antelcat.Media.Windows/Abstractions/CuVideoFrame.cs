using System.Runtime.InteropServices;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using Lennox.NvEncSharp;

namespace Antelcat.Media.Windows.Abstractions;

public sealed class CuVideoFrame(
    int width,
    int height,
    int length,
    VideoFrameFormat format,
    CuMemoryType memoryType,
    int pitch)
    : RawVideoFrame(width, height, length, AllocMemory(length, memoryType), format)
{
    public CuMemoryType MemoryType { get; } = memoryType;

    public override int Pitch => pitch;

    public override void Dispose()
    {
        if (MemoryType == CuMemoryType.Host)
        {
            Marshal.FreeHGlobal(Data);
        }
        else
        {
            LibCuda.MemFree(new CuDevicePtr(Data));
        }
    }

    private static IntPtr AllocMemory(int length, CuMemoryType memoryType)
    {
        return memoryType switch
        {
            CuMemoryType.Host => Marshal.AllocHGlobal(length),
            CuMemoryType.Device => CuDeviceMemory.Allocate(length),
            _ => throw new NotSupportedException(nameof(memoryType))
        };
    }
}