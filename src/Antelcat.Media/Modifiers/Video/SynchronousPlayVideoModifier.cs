using System;
using System.Threading;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;

namespace Antelcat.Media.Modifiers;

/// <summary>
/// 播放视频时带有同步机制，阻碍下一个Modifier
/// </summary>
public sealed class SynchronousPlayVideoModifier(Func<TimeSpan> currentTimeGetter, IVideoModifier next) : IVideoModifier
{
    public VideoFrameFormat TargetFormat => VideoFrameFormat.Unset;

    /// <summary>
    /// 设为true允许跳过一帧
    /// </summary>
    public bool IsScrubbing { get; set; }

    public void Open(VideoInputDevice device, VideoFrameFormat srcFormat)
    {
        next.Open(device, srcFormat);
    }

    public RawVideoFrame ModifyFrame(VideoInputDevice device, RawVideoFrame frame, CancellationToken cancellationToken)
    {
        if (frame.Time == TimeSpan.MinValue || frame.Duration == TimeSpan.MinValue)
        {
            return next.ModifyFrame(device, frame, cancellationToken);
        }

        while (true)
        {
            if (device.CurrentState != InputDevice.State.Running)
            {
                return frame; // 设备当前不在运行状态，直接丢帧
            }

            if (IsScrubbing)
            { 
                // 当前需要跳帧，直接传递给下一个
                IsScrubbing = false;
                return next.ModifyFrame(device, frame, cancellationToken);
            }
            
            var deltaTime = currentTimeGetter().TotalMilliseconds - frame.Time.TotalMilliseconds;
            if (deltaTime < 0)
            {
                // 还没到这一帧的时间，等待一段时间，然后继续循环判断。
                // 等待的时间不能过长，目的是为了及时响应跳帧操作
                Thread.Sleep(Math.Clamp(-(int)deltaTime, 1, 100));
                continue;
            }

            if (deltaTime <= frame.Duration.TotalMilliseconds)
            {
                return next.ModifyFrame(device, frame, cancellationToken); // 当前时间处于当前帧时间范围内，直接传递给下一个
            }

            // 当前帧时间比当前时间早，直接丢帧
            return frame;
        }
    }

    public void Close(VideoInputDevice device)
    {
        next.Close(device);
    }
}