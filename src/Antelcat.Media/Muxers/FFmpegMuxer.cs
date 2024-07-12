using System;
using System.Collections.Generic;
using System.IO;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Extensions;
using Antelcat.Media.Abstractions.Interfaces;
using Antelcat.Media.Encoders.Audio;
using Antelcat.Media.Encoders.Video;
using Antelcat.Media.Internal;
using Antelcat.Media.Extensions;
using Sdcb.FFmpeg.Codecs;
using Sdcb.FFmpeg.Common;
using Sdcb.FFmpeg.Formats;
using Sdcb.FFmpeg.Raw;

namespace Antelcat.Media.Muxers;

public sealed class FFmpegMuxer : IMuxer
{
    private readonly List<IDisposable> disposeCollector = [];
    private readonly List<Action> actionCollector = [];

    private OutputFormat? outputFormat;
    private IOContext? ioContext;
    private FormatContext? formatContext;
    private int encoderCount, openedEncoderCount;

    public void Open(string outputUrl)
    {
        ioContext = IOContext.Open(outputUrl, AVIO_FLAG.ReadWrite);
        var format = Path.GetExtension(outputUrl).TrimStart('.');
        outputFormat = OutputFormat.Guess(format).NotNull($"Guess OutputFormat failed: {format}");
    }

    public void Open(Stream outputStream, string format)
    {
        ioContext = IOContext.WriteStream(outputStream);
        outputFormat = OutputFormat.Guess(format).NotNull($"Guess OutputFormat failed: {format}");
    }

    public void AddAudioEncoder(IAudioEncoder audioEncoder)
    {
        audioEncoder.Opening += AudioEncoder_OnOpening;
        audioEncoder.Closing += (_, _) => Encoder_OnClosing();
        encoderCount++;
    }

    public void AddVideoEncoder(IVideoEncoder videoEncoder)
    {
        videoEncoder.Opening += VideoEncoder_OnOpening;
        videoEncoder.Closing += (_, _) => Encoder_OnClosing();
        encoderCount++;
    }

    private void VideoEncoder_OnOpening(VideoInputDevice device, IVideoEncoder encoder)
    {
        if (formatContext == null)
        {
            formatContext = FormatContext.AllocOutput(outputFormat);
            formatContext.Pb = ioContext;
        }

        CodecContext codecContext;

        if (encoder is FFmpegVideoEncoder ffmpegEncoder)
        {
            codecContext = ffmpegEncoder.Encoder.CodecContext.NotNull();
        }
        else
        {
            var codec = Codec.FindEncoderById(encoder.Format.ToCodecId());
            codecContext = new CodecContext(codec);
            disposeCollector.Add(codecContext);
            codecContext.SetByVideoInput(device);
            codecContext.TimeBase = new AVRational(1, (int)TimeSpan.TicksPerSecond);
            codecContext.PixelFormat = AVPixelFormat.Yuv420p;

            codecContext.Open(codec);
        }

        var stream = new MediaStream(formatContext.NotNull());
        var codecPar = stream.Codecpar.NotNull();
        codecPar.CopyFrom(codecContext);
        disposeCollector.Add(codecPar);

        void FrameEncodedHandler(RawVideoPacket packet)
        {
            if (packet is FFmpegVideoPacket ffmpegVideoPacket)
            {
                formatContext.InterleavedWritePacket(ffmpegVideoPacket.Packet);
                return;
            }

            using var pkt = new Packet();
            pkt.Pts = packet.Pts.ToTimestamp(stream.TimeBase);
            pkt.Dts = packet.Dts.ToTimestamp(stream.TimeBase);
            pkt.Duration = packet.Duration.ToTimestamp(stream.TimeBase);
            pkt.Data = new DataPointer(packet.Data, packet.Length);
            pkt.StreamIndex = stream.Index;
            pkt.Position = -1;

            if (packet.IsKeyFrame)
            {
                pkt.Flags |= (int)ffmpeg.AV_PKT_FLAG_KEY;
            }

            formatContext.InterleavedWritePacket(pkt);
        }

        encoder.FrameEncoded += FrameEncodedHandler;
        actionCollector.Add(() => encoder.FrameEncoded -= FrameEncodedHandler);

        if (++openedEncoderCount == encoderCount)
        {
            formatContext.WriteHeader();
        }
    }

    private void AudioEncoder_OnOpening(AudioInputDevice device, IAudioEncoder encoder)
    {
        if (formatContext == null)
        {
            formatContext = FormatContext.AllocOutput(outputFormat);
            formatContext.Pb = ioContext;
        }

        CodecContext codecContext;

        if (encoder is FFmpegAudioEncoder ffmpegEncoder)
        {
            codecContext = ffmpegEncoder.Encoder.CodecContext.NotNull();
        }
        else
        {
            var codec = Codec.FindEncoderById(encoder.Format.ToCodecId());
            codecContext = new CodecContext(codec);
            disposeCollector.Add(codecContext);
            codecContext.SetByWaveFormat(device.Format, encoder.Bitrate);
            codecContext.Open(codec);
        }

        var stream = new MediaStream(formatContext);
        var codecPar = stream.Codecpar.NotNull();
        codecPar.CopyFrom(codecContext);
        disposeCollector.Add(codecPar);

        void FrameEncodedHandler(RawAudioPacket packet)
        {
            if (packet is FFmpegAudioPacket fFmpegAudioPacket)
            {
                formatContext.InterleavedWritePacket(fFmpegAudioPacket.Packet);
                return;
            }

            using var pkt = new Packet();
            pkt.Pts = packet.Pts.ToTimestamp(stream.TimeBase);
            pkt.Dts = packet.Dts.ToTimestamp(stream.TimeBase);
            pkt.Duration = packet.Duration.ToTimestamp(stream.TimeBase);
            pkt.Data = new DataPointer(packet.Data, packet.Length);
            pkt.StreamIndex = stream.Index;
            pkt.Position = -1;

            //pkt.RescaleTimestamp(new AVRational(1, (int)(TimeSpan.TicksPerMillisecond * 1000)), stream.TimeBase);
            formatContext.InterleavedWritePacket(pkt);
        }

        encoder.FrameEncoded += FrameEncodedHandler;
        actionCollector.Add(() => encoder.FrameEncoded -= FrameEncodedHandler);

        if (++openedEncoderCount == encoderCount)
        {
            formatContext.WriteHeader();
        }
    }

    private void Encoder_OnClosing()
    {
        if (--openedEncoderCount == 0 && formatContext != null)
        {
            formatContext.WriteTrailer();

            if (ioContext != null)
            {
                ioContext.Dispose();
                ioContext = null;
            }

            if (formatContext != null)
            {
                formatContext.Dispose();
                formatContext = null;
            }

            foreach (var disposable in disposeCollector)
            {
                disposable.Dispose();
            }
            disposeCollector.Clear();
            
            foreach (var action in actionCollector)
            {
                action();
            }
            actionCollector.Clear();
        }
    }
}