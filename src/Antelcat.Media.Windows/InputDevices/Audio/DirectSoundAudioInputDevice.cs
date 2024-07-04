using Antelcat.Media.Abstractions;
using Antelcat.Media.Windows.Extensions;
using SharpDX;
using SharpDX.DirectSound;
using SharpDX.Mathematics.Interop;

namespace Antelcat.Media.Windows.InputDevices.Audio;

/// <summary>
/// 通过麦克风录音
/// </summary>
internal sealed class DirectSoundAudioInputDevice : AudioInputDevice
{
    public override bool IsReady => capture != null;

    public override AudioFrameFormat OriginalFormat { get; }

    private CaptureBuffer? captureBuffer;
    private readonly DirectSoundCapture? capture;
    private readonly AutoResetEvent notifyEvent = new(false);

    private DirectSoundAudioInputDevice()
    {
        OriginalFormat = new AudioFrameFormat();
    }

    public static DirectSoundAudioInputDevice Empty => new();

    internal DirectSoundAudioInputDevice(Guid guid, AudioFrameFormat waveFormat)
    {
        capture = new DirectSoundCapture(guid);
        Uid = guid.ToString();
        waveFormat.IsPlanar = false;
        if (waveFormat.BitsPerSample >= 32)
        {
            waveFormat.IsFloat = true;
        }
        OriginalFormat = waveFormat;
        StateChanging += OnStateChanging;
    }

    private void OnStateChanging(State oldState, State newState)
    {
        if (newState == State.Closing)
        {
            captureBuffer?.Start(new RawBool(true));
            notifyEvent.Set();
        }
    }

    private const int BufferCount = 16;
    private const int BufferSize = 1024;

    protected override void Opening()
    {
        captureBuffer?.Dispose();

        var captureBufferDescription = new CaptureBufferDescription
        {
            Format = OriginalFormat.ToWaveFormat(),
            BufferBytes = BufferCount * BufferSize,
            Flags = CaptureBufferCapabilitiesFlags.ControlEffects | CaptureBufferCapabilitiesFlags.WaveMapped,
            EffectDescriptions =
            [
                // new() {
                // 	CaptureEffectClass = new Guid("BF963D80-C559-11D0-8A2B-00A0C9255AC1"),
                // 	CaptureEffectInstance = new Guid("1C22C56D-9879-4f5b-A389-27996DDC2810"),
                // 	Flags = CaptureEffectResult.LocatedInSoftware
                // },
                new CaptureEffectDescription
                {
                    CaptureEffectClass = new Guid("E07F903F-62FD-4e60-8CDD-DEA7236665B5"),
                    CaptureEffectInstance = new Guid("5AB0882E-7274-4516-877D-4EEE99BA4FD0"),
                    Flags = CaptureEffectResult.LocatedInSoftware
                },
            ]

            // Acoustic Echo Canceller {BF963D80-C559-11D0-8A2B-00A0C9255AC1}
            // Microsoft AEC {CDEBB919-379A-488a-8765-F53CFD36DE40}
            // System AEC {1C22C56D-9879-4f5b-A389-27996DDC2810}
            // Noise Suppression {E07F903F-62FD-4e60-8CDD-DEA7236665B5}
            // Microsoft NS {BB11C46F-EC2F-4EAC-A5E4-0B7A42EFC3FD}
            // System NS {5AB0882E-7274-4516-877D-4EEE99BA4FD0}
        };
        captureBuffer = new CaptureBuffer(capture, captureBufferDescription);

        notifyEvent.Reset();
        var nps = new NotificationPosition[BufferCount]; // 设置缓冲区通知个数
        for (var i = 0; i < BufferCount; i++)
        {
            nps[i] = new NotificationPosition
            {
                Offset = BufferSize + i * BufferSize - 1, // 设置具体每个的位置
                WaitHandle = notifyEvent
            };
        }

        captureBuffer.SetNotificationPositions(nps);

        base.Opening();
    }

    protected override void RunLoop(CancellationToken cancellationToken)
    {
        byte[]? buf = null;
        var bufferOffset = 0;

        while (true)
        {
            notifyEvent.WaitOne();
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            CurrentState = State.Running;

            if (captureBuffer == null)
            {
                continue;
            }

            var capturePos = captureBuffer.CurrentCapturePosition;
            var byteSize = captureBuffer.Capabilities.BufferBytes; // 这个大小就是我们可以安全读取的大小
            int lockBytes;

            if (capturePos == bufferOffset)
            {
                continue;
            }

            if (capturePos < bufferOffset)
            {
                lockBytes = capturePos + (byteSize - bufferOffset);
            }
            else
            {
                lockBytes = capturePos - bufferOffset;
            }

            lockBytes -= lockBytes % (byteSize / BufferCount);
            if (lockBytes == 0)
            {
                continue;
            }

            if (buf == null || lockBytes > buf.Length)
            {
                buf = new byte[lockBytes];
            }

            captureBuffer.Read(buf, 0, lockBytes, bufferOffset, LockFlags.None);

            unsafe
            {
                fixed (void* ptr = buf)
                {
                    var sampleCount = lockBytes * 8 / OriginalFormat.ChannelCount / OriginalFormat.BitsPerSample;
                    var frame = new RawAudioFrame(sampleCount, new IntPtr(ptr), lockBytes, OriginalFormat);
                    if (Modifier != null)
                    {
                        frame = Modifier.ModifyFrame(this, frame, cancellationToken);
                    }

                    // var duration = TimeSpan.FromSeconds((double)sampleCount / OriginalFormat.SampleRate);
                    Encoder?.EncodeFrame(this, frame);
                }
            }

            bufferOffset += lockBytes;
            bufferOffset %= BufferCount * BufferSize; // 取模是因为缓冲区是循环的。
        }

        Utilities.Dispose(ref captureBuffer);
    }

    public override void Start()
    {
        base.Start();
        captureBuffer?.Start(new RawBool(true));
    }

    public override void Pause()
    {
        base.Pause();
        captureBuffer?.Stop();
    }
}