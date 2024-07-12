using System;
using System.Threading;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Interfaces;

namespace Antelcat.Media.Modifiers;

/// <summary>
/// 播放视频时带有同步机制，阻碍下一个Modifier
/// </summary>
public sealed class SynchronousPlayAudioModifier
    (Func<TimeSpan> currentTimeGetter, IAudioModifier next) : IAudioModifier {
    public AudioFrameFormat? TargetFormat => null;

    /// <summary>
    /// 设为true允许跳过一帧
    /// </summary>
    public bool IsScrubbing { get; set; }
    
    public void Open(AudioInputDevice device, AudioFrameFormat srcFormat) {
        next.Open(device, srcFormat);
    }

    public RawAudioFrame ModifyFrame(AudioInputDevice device, RawAudioFrame frame, CancellationToken cancellationToken) {
        if (frame.Time.Ticks <= 0 || frame.Duration.Ticks <= 0) {
            return next.ModifyFrame(device, frame, cancellationToken);
        }

        while (true) {
            if (device.CurrentState != InputDevice.State.Running) {
                return frame;  // 设备当前不在运行状态，直接丢帧
            }
            
            if (IsScrubbing) {  // 当前需要跳帧，直接传递给下一个
                IsScrubbing = false;
                return next.ModifyFrame(device, frame, cancellationToken);
            }

            var deltaTime = currentTimeGetter() - frame.Time;
            if (deltaTime < TimeSpan.Zero) {
                // 还没到这一帧的时间，等待一段时间，然后继续循环判断。
                // 等待的时间不能过长，目的是为了及时响应跳帧操作
                Thread.Sleep(Math.Clamp(-(int)deltaTime.TotalMilliseconds, 1, 100));
                continue;
            }
            
            if (deltaTime <= frame.Duration) {
                return next.ModifyFrame(device, frame, cancellationToken);  // 当前时间处于当前帧时间范围内，直接传递给下一个
            }
            
            // 当前帧时间比当前时间早，直接丢帧
            return frame;
        }
    }

    public void Close(AudioInputDevice device) {
        next.Close(device);
    }
}