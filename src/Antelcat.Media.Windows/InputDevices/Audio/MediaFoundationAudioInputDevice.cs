using System.Diagnostics;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Windows.Abstractions.Interfaces;
using Antelcat.Media.Windows.Extensions;
using Antelcat.Media.Windows.Internal;
using SharpDX.MediaFoundation;

namespace Antelcat.Media.Windows.InputDevices.Audio;

internal sealed class MediaFoundationAudioInputDevice : AudioInputDevice, IMediaFoundationInputDevice
{
    public override bool IsReady => Uid != null;
    public override AudioFrameFormat OriginalFormat { get; }
    public MediaType MediaType { get; }
    public MediaSource? MediaSource => mediaSourceWrapper.MediaSource;

    private readonly MediaFoundationMediaSourceWrapper mediaSourceWrapper;

    public MediaFoundationAudioInputDevice(string symbolicLink, AudioMediaTypeBag audioMediaTypeBag)
    {
        mediaSourceWrapper = new MediaFoundationMediaSourceWrapper(symbolicLink, audioMediaTypeBag.Type, MediaDeviceType.Microphone);
        MediaType = new MediaType();
        audioMediaTypeBag.Type.CopyAllItems(MediaType);
        MediaType.Set(MediaTypeAttributeKeys.AudioSamplesPerSecond, audioMediaTypeBag.Format.SampleRate);
        MediaType.Set(MediaTypeAttributeKeys.AudioBitsPerSample, audioMediaTypeBag.Format.BitsPerSample);
        MediaType.Set(MediaTypeAttributeKeys.AudioNumChannels, audioMediaTypeBag.Format.ChannelCount);
        MediaType.Set(MediaTypeAttributeKeys.Subtype, audioMediaTypeBag.Format.IsFloat ? AudioFormatGuids.Float : AudioFormatGuids.Pcm);

        Uid = symbolicLink;
        OriginalFormat = audioMediaTypeBag.Format;
    }

    protected override void Opening()
    {
        mediaSourceWrapper.Open();
        base.Opening();
    }

    protected override void RunLoop(CancellationToken cancellationToken)
    {
        using var reader = new SourceReader(MediaSource);
        reader.SetStreamSelection(SourceReaderIndex.FirstAudioStream, true);
        MediaType.Dump();
        reader.SetCurrentMediaType(SourceReaderIndex.FirstAudioStream, MediaType);
        var firstSampleTime = -1L;

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
            var sampleCount = currentLength * 8 / OriginalFormat.ChannelCount / OriginalFormat.BitsPerSample;
            
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
            
            var frame = new RawAudioFrame(sampleCount, sourcePointer, currentLength, OriginalFormat)
            {
                Time = TimeSpan.FromTicks(sampleTime * TimeSpan.TicksPerMillisecond / 10000),
                Duration = TimeSpan.FromTicks(sample.SampleDuration * TimeSpan.TicksPerMillisecond / 10000)
            };

            try
            {
                if (Modifier != null)
                {
                    frame = Modifier.ModifyFrame(this, frame, cancellationToken);
                }
                
                Encoder?.EncodeFrame(this, frame);
            }
            finally
            {
                sourceBuffer.Unlock();
            }

            frame.Dispose();
        }
    }

    protected override void Closing()
    {
        base.Closing();
        mediaSourceWrapper.Close();
    }
}