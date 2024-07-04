using System;
using System.Collections.Generic;
using System.Linq;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Extensions;
using Antelcat.Media.Streams;
using Sdcb.FFmpeg.Codecs;
using Sdcb.FFmpeg.Raw;
using Sdcb.FFmpeg.Swscales;
using Sdcb.FFmpeg.Utils;

namespace Antelcat.Media.Internal;

internal class FFmpegEncoder
{
    public CodecContext? CodecContext { get; private set; }
    public Codec Codec { get; private set; }

    private PixelConverter? pixelConverter;
    private Frame? avFrame;
    private Packet? packet;
    private ConstantAudioBuffer? audioBuffer;

    public void Open(VideoInputDevice device, AVCodecID codecId, long bitRate)
    {
        Codec = Codec.FindEncoderById(codecId);
        CodecContext = new CodecContext(Codec);

        // 获取设备的像素格式
        var devicePixelFormat = device.Format.ToAvPixelFormat();
        if (!Codec.PixelFormats.Contains(devicePixelFormat))
        {
            // 如果不支持，那就需要转换
            var destPixelFormat = Codec.PixelFormats.First();
            CodecContext.SetByVideoInput(device, destPixelFormat, bitRate);
            pixelConverter = new PixelConverter(
                CodecContext.Width,
                CodecContext.Height,
                devicePixelFormat,
                CodecContext.Width,
                CodecContext.Height,
                destPixelFormat);
        }
        else
        {
            CodecContext.SetByVideoInput(device, devicePixelFormat, bitRate);
        }

        CodecContext.Open(Codec);

        avFrame = Frame.CreateVideo(
            CodecContext.Width,
            CodecContext.Height,
            devicePixelFormat);
        avFrame.TimeBase = CodecContext.TimeBase;

        packet = new Packet();
    }

    public void Open(AudioInputDevice device, AVCodecID codecId, long bitRate)
    {
        Codec = Codec.FindEncoderById(codecId);
        CodecContext = new CodecContext(Codec);

        // 与视频编码器不同，音频编码中不同的采样方式会影响音质，所以不能选取默认值
        // 如果不支持，就抛出异常，调用方需要解决
        var deviceSampleFormat = device.Format.ToAvSampleFormat();
        if (!Codec.SampleFormats.Contains(deviceSampleFormat))
        {
            throw new NotSupportedException(
                $"当前编码器{codecId}不支持给定的采样方式：{deviceSampleFormat}\n支持的采样方式：{string.Join(", ", Codec.SampleFormats)}");
        }
        if (!Codec.SupportedSamplerates.Contains(device.Format.SampleRate))
        {
            throw new NotSupportedException(
                $"当前编码器{codecId}不支持给定的采样率：{device.Format.SampleRate}\n支持的采样率：{string.Join(", ", Codec.SupportedSamplerates)}");
        }

        CodecContext.SetByWaveFormat(device.Format, bitRate);
        CodecContext.Open(Codec);

        avFrame = Frame.CreateAudio(
            CodecContext.SampleFormat,
            CodecContext.ChLayout,
            CodecContext.SampleRate,
            CodecContext.FrameSize);
        avFrame.TimeBase = CodecContext.TimeBase;

        packet = new Packet();

        // 3s缓冲
        audioBuffer = new ConstantAudioBuffer(device.Format, TimeSpan.FromSeconds(3));
    }

    public IEnumerable<Packet> EncodeFrame(RawVideoFrame frame)
    {
        if (CodecContext == null)
        {
            throw new NullReferenceException(nameof(CodecContext));
        }
        if (avFrame == null)
        {
            throw new NullReferenceException(nameof(avFrame));
        }
        if (packet == null)
        {
            throw new NullReferenceException(nameof(packet));
        }

        avFrame.MakeWritable();

        if (pixelConverter != null)
        {
            pixelConverter.Convert(frame.Data, avFrame, (AVPixelFormat)avFrame.Format, CodecContext);
        }
        else
        {
            frame.CopyToAvFrame(avFrame);
        }

        avFrame.Pts = frame.Time.ToTimestamp(avFrame.TimeBase);
        avFrame.Duration = frame.Duration.ToTimestamp(avFrame.TimeBase);

        CodecContext.SendFrame(avFrame);

        while (true)
        {
            var ret = CodecContext.ReceivePacket(packet);
            if (ret is CodecResult.Again or CodecResult.EOF)
            {
                break;
            }

            if (ret < 0)
            {
                throw new Exception($"CodecContext.ReceivePacket: {ret}");
            }

            packet.TimeBase = CodecContext.TimeBase;
            yield return packet;
            packet.Unref();
        }
    }

    public IEnumerable<Packet> EncodeFrame(RawAudioFrame frame)
    {
        if (CodecContext == null)
        {
            throw new NullReferenceException(nameof(CodecContext));
        }
        if (avFrame == null)
        {
            throw new NullReferenceException(nameof(avFrame));
        }
        if (packet == null)
        {
            throw new NullReferenceException(nameof(packet));
        }
        if (audioBuffer == null)
        {
            throw new NullReferenceException(nameof(audioBuffer));
        }

        audioBuffer.Write(frame.Data, 0, frame.Length);

        var frameBytesLength = CodecContext.FrameSize * frame.Format.BitsPerSample / 8 * frame.Format.ChannelCount;
        while (audioBuffer.Length >= frameBytesLength)
        {
            avFrame.MakeWritable();
            var pts = audioBuffer.SamplesReadCount;
            audioBuffer.Read(avFrame.Data._0, 0, frameBytesLength);
            var duration = audioBuffer.SamplesReadCount - pts;

            avFrame.Pts = pts;
            avFrame.Duration = duration;
            CodecContext.SendFrame(avFrame);

            while (true)
            {
                var ret = CodecContext.ReceivePacket(packet);
                if (ret is CodecResult.Again or CodecResult.EOF)
                {
                    break;
                }

                if (ret < 0)
                {
                    throw new Exception($"CodecContext.ReceivePacket: {ret}");
                }

                yield return packet;
                packet.Unref();
            }
        }
    }

    public void Close()
    {
        if (avFrame != null)
        {
            avFrame.Dispose();
            avFrame = null;
        }

        if (packet != null)
        {
            packet.Dispose();
            packet = null;
        }

        if (pixelConverter != null)
        {
            pixelConverter.Dispose();
            pixelConverter = null;
        }

        if (audioBuffer != null)
        {
            audioBuffer.Dispose();
            audioBuffer = null;
        }
    }
}