using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using Antelcat.Media.Windows.Abstractions.Interfaces;
using Antelcat.Media.Windows.Internal;
using EasyPathology.Abstractions.DataTypes;
using SharpDX.MediaFoundation;

namespace Antelcat.Media.Windows.InputDevices.Video;

internal sealed class MediaFoundationVideoInputDevice : VideoInputDevice, IMediaFoundationInputDevice
{
    public override bool IsReady => Uid != null;
    public override Fraction FrameRate { get; }
    public override int FrameWidth { get; }
    public override int FrameHeight { get; }
    public MediaType? MediaType => mediaSourceWrapper.MediaType;
    public MediaSource? MediaSource => mediaSourceWrapper.MediaSource;
    public override VideoFrameFormat OriginalFormat { get; }

    private readonly MediaFoundationMediaSourceWrapper mediaSourceWrapper;

    public MediaFoundationVideoInputDevice(string symbolicLink, VideoMediaTypeBag videoMediaTypeBag)
    {
        mediaSourceWrapper = new MediaFoundationMediaSourceWrapper(symbolicLink, videoMediaTypeBag.Type, MediaDeviceType.Camera);
        Uid = symbolicLink;
        OriginalFormat = videoMediaTypeBag.Format;
        FrameWidth = videoMediaTypeBag.Width;
        FrameHeight = videoMediaTypeBag.Height;
        FrameRate = videoMediaTypeBag.FrameRate;
    }

    protected override void Opening()
    {
        mediaSourceWrapper.Open();
        base.Opening();
    }

    protected override void RunLoop(CancellationToken cancellationToken)
    {
        using var reader = new SourceReader(MediaSource);
        reader.SetStreamSelection(SourceReaderIndex.FirstVideoStream, true);
        reader.SetCurrentMediaType(SourceReaderIndex.FirstVideoStream, MediaType);
        var firstSampleTime = -1L; // sampleTime不从0开始

        while (!cancellationToken.IsCancellationRequested)
        {
            waitHandle.WaitOne();
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            CurrentState = State.Running;
            using var sample = reader.ReadSample(SourceReaderIndex.AnyStream, SourceReaderControlFlags.None, out var index, out _, out _);
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            if (sample == null)
            {
                continue;
            }

            using var sourceBuffer = sample.GetBufferByIndex(index);
            var sourcePointer = sourceBuffer.Lock(out _, out var currentLength);

            // MediaFoundation中的SampleTime以及SampleDuration是以100ns为单位的，其实这个单位和TimeSpan.Ticks一致
            // https://learn.microsoft.com/en-us/windows/win32/api/mfobjects/nf-mfobjects-imfsample-getsampletime
            long sampleTime;
            if (firstSampleTime == -1)
            {
                firstSampleTime = sample.SampleTime;
                sampleTime = 0;
            }
            else
            {
                sampleTime = sample.SampleTime - firstSampleTime;
            }

            var frame = new RawVideoFrame(FrameWidth, FrameHeight, currentLength, sourcePointer, OriginalFormat)
            {
                Time = TimeSpan.FromTicks(sampleTime * TimeSpan.TicksPerMillisecond / 10000),
                Duration = TimeSpan.FromTicks(sample.SampleDuration * TimeSpan.TicksPerMillisecond / 10000)
            };

            ProcessFrame(frame, cancellationToken);
        }
    }

    protected override void Closing()
    {
        base.Closing();
        mediaSourceWrapper.Close();
    }
}