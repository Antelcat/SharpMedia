using System;
using System.Threading;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Interfaces;
using Antelcat.Media.Extensions;
using Sdcb.FFmpeg.Raw;
using Sdcb.FFmpeg.Swresamples;
using Sdcb.FFmpeg.Utils;
using static Sdcb.FFmpeg.Raw.ffmpeg;

namespace Antelcat.Media.Modifiers;

/// <summary>
/// 音频重采样
/// </summary>
public class AudioResampleModifier(AudioFrameFormat targetFormat) : IAudioModifier
{
    public AudioFrameFormat TargetFormat { get; } = targetFormat;

    private readonly SampleConverter sampleConverter = new();
    private Frame? srcFrame, dstFrame;
    private int_array8 dstLineSize;
    private int srcSampleCount, dstSampleCount;

    public void Open(AudioInputDevice device, AudioFrameFormat srcFormat)
    {
        sampleConverter.Reset(
            TargetFormat.ToAvChannelLayout(),
            TargetFormat.ToAvSampleFormat(),
            TargetFormat.SampleRate,
            srcFormat.ToAvChannelLayout(),
            srcFormat.ToAvSampleFormat(),
            srcFormat.SampleRate);
        sampleConverter.Initialize();
    }

    public unsafe RawAudioFrame ModifyFrame(AudioInputDevice device, RawAudioFrame frame, CancellationToken cancellationToken)
    {
        if (frame.SampleCount == 0)
        {
            return new RawAudioFrame(0, 0, TargetFormat);
        }
        
        if (srcFrame == null || srcSampleCount < frame.SampleCount)
        {
            srcFrame?.Dispose();
            srcFrame = Frame.CreateAudio(
                frame.Format.ToAvSampleFormat(),
                frame.Format.ToAvChannelLayout(),
                frame.Format.SampleRate,
                frame.SampleCount);

            srcSampleCount = frame.SampleCount;
        }

        frame.CopyToAvFrame(srcFrame);

        int desiredDstSampleCount;
        // Buffer is going to be directly written to a raw audio file, no alignment
        if (dstFrame == null)
        {
            /* Compute the number of converted samples: buffering is avoided
             * ensuring that the output buffer will contain at least all the
             * converted input samples */
            dstSampleCount = desiredDstSampleCount = (int)av_rescale_rnd(
                frame.SampleCount,
                TargetFormat.SampleRate,
                frame.Format.SampleRate,
                AVRounding.Up);

            dstFrame = Frame.CreateAudio(
                TargetFormat.ToAvSampleFormat(),
                TargetFormat.ToAvChannelLayout(),
                TargetFormat.SampleRate,
                desiredDstSampleCount);

            dstLineSize = dstFrame.Linesize;
        }

        // Compute destination number of samples
        desiredDstSampleCount = (int)av_rescale_rnd(
            sampleConverter.GetDelay(frame.Format.SampleRate) +
            frame.SampleCount,
            TargetFormat.SampleRate,
            frame.Format.SampleRate,
            AVRounding.Up);
        if (desiredDstSampleCount > dstSampleCount)
        {
            dstFrame.Dispose();
            dstFrame = Frame.CreateAudio(
                TargetFormat.ToAvSampleFormat(),
                TargetFormat.ToAvChannelLayout(),
                TargetFormat.SampleRate,
                desiredDstSampleCount);

            dstLineSize = dstFrame.Linesize;
            dstSampleCount = desiredDstSampleCount;
        }

        var samples = sampleConverter.Convert(
            dstFrame.Data,
            desiredDstSampleCount,
            srcFrame.Data,
            frame.SampleCount);
        if (samples == 0)
        {
            throw new InvalidOperationException();
        }

        var lineSize = dstLineSize;
        var destinationBufferSize = av_samples_get_buffer_size(
            lineSize._,
            TargetFormat.ChannelCount,
            samples,
            TargetFormat.ToAvSampleFormat(),
            4);
        dstLineSize = lineSize;

        if (destinationBufferSize == 0)
        {
            throw new InsufficientMemoryException();
        }

        return new RawAudioFrame(desiredDstSampleCount, dstFrame.Data._0, destinationBufferSize, TargetFormat)
        {
            Time = frame.Time,
            Duration = frame.Duration
        };
    }

    public void Close(AudioInputDevice device)
    {
        sampleConverter.Close();
    }
}