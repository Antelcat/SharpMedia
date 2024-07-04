using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using Antelcat.Media.Windows.Extensions;
using Antelcat.Media.Windows.Internal;
using EasyPathology.Abstractions.DataTypes;
using Lennox.NvEncSharp;
using SharpDX;
using D3D11 = SharpDX.Direct3D11;
using DXGI = SharpDX.DXGI;

namespace Antelcat.Media.Windows.Encoders.Video;

public class NvCodecVideoEncoder(NvCodecVideoEncoder.EncodeOptions encodeOptions) : IVideoEncoder
{
    public abstract record EncodeOptions
    {
        public uint? GopLength { get; init; }
        public bool? EnableLookahead { get; init; }
        public bool Hq { get; init; }
        public uint? VbvBufferSize { get; init; }

        /// <summary>
        /// 阻止外部继承
        /// </summary>
        internal EncodeOptions() { }
    }

    public record VbrEncodeOptions : EncodeOptions
    {
        public required uint MaxBitRate { get; init; }
    }

    public record CbrEncodeOptions : EncodeOptions
    {
        public required uint AverageBitRate { get; init; }
        public bool LowLatency { get; init; }
    }

    public IEnumerable<EncodedVideoFormat> SupportedFormats => new[]
    {
        EncodedVideoFormat.H264, EncodedVideoFormat.Hevc
    };

    public EncodedVideoFormat Format { get; set; }

    public int Bitrate => encodeOptions switch
    {
        VbrEncodeOptions vbr => (int)vbr.MaxBitRate,
        CbrEncodeOptions cbr => (int)cbr.AverageBitRate,
        _ => 0
    };

    public event IEncoder<IVideoEncoder, VideoInputDevice, EncodedVideoFormat, RawVideoFrame, RawVideoPacket>.OpeningHandler? Opening;
    public event IEncoder<IVideoEncoder, VideoInputDevice, EncodedVideoFormat, RawVideoFrame, RawVideoPacket>.FrameEncodedHandler? FrameEncoded;
    public event IEncoder<IVideoEncoder, VideoInputDevice, EncodedVideoFormat, RawVideoFrame, RawVideoPacket>.ClosingHandler? Closing;

    private readonly SharedD3D11Device sharedD3D11Device = SharedD3D11Device.HardwareVideoEncoder;
    
    private NvEncoder encoder;
    private NvEncCreateBitstreamBuffer bitStreamBuffer;
    private D3D11.Texture2D? texture2D;
    private NvEncConfig config;
    private NvEncInitializeParams initializeParams;
    private int encodedFrameCount;

    #region Private

    private static NvEncBufferFormat FrameFormat2NvEncBufferFormat(VideoFrameFormat format)
    {
        return format switch
        {
            VideoFrameFormat.NV12 => NvEncBufferFormat.Nv12,
            VideoFrameFormat.Yv12 => NvEncBufferFormat.Yv12,
            VideoFrameFormat.RGBA32 => NvEncBufferFormat.Argb,
            _ => NvEncBufferFormat.Undefined
        };
    }

    private static uint GetActualHeight(uint frameHeight, VideoFrameFormat format)
    {
        return format switch
        {
            VideoFrameFormat.NV12 => frameHeight * 3 / 2,
            VideoFrameFormat.Yv12 => frameHeight,
            VideoFrameFormat.RGBA32 => frameHeight,
            _ => 0
        };
    }

    private void InitializeEncoder(Guid codec, Fraction frameRate, uint width, uint height)
    {
        encoder = LibNvEnc.OpenEncoderForDirectX(sharedD3D11Device.Device.NativePointer);
        config = encoder.GetEncodePresetConfig(codec, NvEncPresetGuids.Default).PresetCfg;
        if (encodeOptions.GopLength.HasValue) config.GopLength = encodeOptions.GopLength.Value;
        if (encodeOptions.EnableLookahead.HasValue) config.RcParams.EnableLookahead = encodeOptions.EnableLookahead.Value;
        if (encodeOptions.VbvBufferSize.HasValue) config.RcParams.VbvBufferSize = encodeOptions.VbvBufferSize.Value;
        switch (encodeOptions)
        {
            case VbrEncodeOptions vbr:
            {
                config.RcParams.RateControlMode = vbr.Hq ? NvEncParamsRcMode.VbrHq : NvEncParamsRcMode.Vbr;
                config.RcParams.MaxBitRate = vbr.MaxBitRate;
                break;
            }
            case CbrEncodeOptions cbr:
            {
                config.RcParams.RateControlMode =
                    cbr.Hq ? cbr.LowLatency ? NvEncParamsRcMode.CbrLowdelayHq : NvEncParamsRcMode.CbrHq : NvEncParamsRcMode.Cbr;
                config.RcParams.AverageBitRate = cbr.AverageBitRate;
                break;
            }
        }

        unsafe
        {
            fixed (NvEncConfig* p = &config)
            {
                initializeParams = new NvEncInitializeParams
                {
                    Version = LibNvEnc.NV_ENC_INITIALIZE_PARAMS_VER,
                    EncodeGuid = codec,
                    EncodeHeight = height,
                    EncodeWidth = width,
                    MaxEncodeHeight = height,
                    MaxEncodeWidth = width,
                    DarHeight = height,
                    DarWidth = width,
                    FrameRateNum = frameRate.Number,
                    FrameRateDen = frameRate.Denominator,
                    ReportSliceOffsets = false,
                    EnableSubFrameWrite = false,
                    PresetGuid = NvEncPresetGuids.Default,
                    EnableEncodeAsync = 0,
                    EnablePTD = 1,
                    EnableWeightedPrediction = true,
                    EncodeConfig = p
                };

                encoder.InitializeEncoder(ref initializeParams);
            }
        }
    }

    #endregion

    public void Open(VideoInputDevice device)
    {
        if (Format is not EncodedVideoFormat.H264 and not EncodedVideoFormat.Hevc)
        {
            throw new ArgumentException("Only H264 and HEVC are supported");
        }

        if (device.Format.ToDxgiFormat() == DXGI.Format.Unknown)
        {
            throw new NotSupportedException();
        }

        var codec = Format switch
        {
            EncodedVideoFormat.H264 => NvEncCodecGuids.H264,
            EncodedVideoFormat.Hevc => NvEncCodecGuids.Hevc,
            _ => throw new NotSupportedException()
        };

        InitializeEncoder(
            codec,
            device.FrameRate,
            (uint)device.FrameWidth,
            (uint)device.FrameHeight);
        bitStreamBuffer = encoder.CreateBitstreamBuffer();
        encodedFrameCount = 0;

        Opening?.Invoke(device, this);
    }

    public void EncodeFrame(VideoInputDevice device, RawVideoFrame frame)
    {
        if (frame.Length == 0) return;

        var encFormat = FrameFormat2NvEncBufferFormat(frame.Format);
        NvEncRegisterResource reg;
        
        if (frame is D3D11TextureVideoFrame d3D11TextureVideoFrame)
        {
            reg = new NvEncRegisterResource
            {
                Version = LibNvEnc.NV_ENC_REGISTER_RESOURCE_VER,
                BufferFormat = encFormat,
                BufferUsage = NvEncBufferUsage.NvEncInputImage,
                ResourceToRegister = d3D11TextureVideoFrame.Texture2D.NativePointer,
                Width = (uint)frame.Width,
                Height = (uint)frame.Height,
                Pitch = (uint)frame.Pitch
            };
        }
        else
        {
            if (texture2D == null)
            {
                var desc = new D3D11.Texture2DDescription
                {
                    BindFlags = D3D11.BindFlags.None,
                    CpuAccessFlags = D3D11.CpuAccessFlags.Write,
                    Format = frame.Format.ToDxgiFormat(),
                    Width = frame.Width,
                    Height = frame.Height,
                    ArraySize = 1,
                    MipLevels = 1,
                    SampleDescription = new DXGI.SampleDescription(1, 0),
                    OptionFlags = D3D11.ResourceOptionFlags.None,
                    Usage = D3D11.ResourceUsage.Staging,
                };
                texture2D = new D3D11.Texture2D(sharedD3D11Device.Device, desc);
            }
            
            var box = sharedD3D11Device.Device.ImmediateContext.MapSubresource(texture2D, 0, 0, D3D11.MapMode.Write, D3D11.MapFlags.None, out _);
            var actualHeight = GetActualHeight((uint)device.FrameHeight, frame.Format);
            for (var y = 0; y < actualHeight; y++)
            {
                unsafe
                {
                    Buffer.MemoryCopy(
                        (frame.Data + y * box.RowPitch).ToPointer(),
                        (box.DataPointer + y * box.RowPitch).ToPointer(),
                        box.RowPitch,
                        Math.Min(box.RowPitch, frame.Pitch));
                }
            }

            sharedD3D11Device.Device.ImmediateContext.UnmapSubresource(texture2D, 0);
            
            reg = new NvEncRegisterResource
            {
                Version = LibNvEnc.NV_ENC_REGISTER_RESOURCE_VER,
                BufferFormat = encFormat,
                BufferUsage = NvEncBufferUsage.NvEncInputImage,
                ResourceToRegister = texture2D.NativePointer,
                Width = (uint)frame.Width,
                Height = (uint)frame.Height,
                Pitch = (uint)frame.Pitch
            };
        }

        // Registers the hardware texture surface as a resource for
        // NvEnc to use.
        using var resource = encoder.RegisterResource(ref reg);

        var pic = new NvEncPicParams
        {
            Version = LibNvEnc.NV_ENC_PIC_PARAMS_VER,
            PictureStruct = NvEncPicStruct.Frame,
            InputBuffer = reg.AsInputPointer(),
            BufferFmt = encFormat,
            InputWidth = (uint)frame.Width,
            InputHeight = (uint)frame.Height,
            InputPitch = (uint)frame.Pitch,
            OutputBitstream = bitStreamBuffer.BitstreamBuffer,
            InputTimeStamp = (ulong)frame.Time.Ticks,
            InputDuration = (ulong)frame.Duration.Ticks
        };
        if (encodedFrameCount++ % (encodeOptions.GopLength ?? 60) == 0)
        {
            // ReSharper disable BitwiseOperatorOnEnumWithoutFlags
            pic.EncodePicFlags = (uint)((NvEncPicFlags)pic.EncodePicFlags | NvEncPicFlags.FlagOutputSpspps | NvEncPicFlags.FlagForceidr);
            // ReSharper restore BitwiseOperatorOnEnumWithoutFlags
        }

        // Do the actual encoding. With this configuration this is done
        // sync (blocking).
        encoder.EncodePicture(ref pic);

        // The output is written to the bit stream, which is now copied
        // to the output file.
        var stream = encoder.LockBitstream(ref bitStreamBuffer);
        var packet = new RawVideoPacket(stream.BitstreamBufferPtr, (int)stream.BitstreamSizeInBytes, Format)
        {
            Pts = TimeSpan.FromTicks((long)stream.OutputTimeStamp),
            Dts = TimeSpan.FromTicks((long)pic.InputTimeStamp),
            Duration = TimeSpan.FromTicks((long)pic.InputDuration),
            IsKeyFrame = stream.PictureType == NvEncPicType.Idr
        };

        try
        {
            FrameEncoded?.Invoke(packet);
        }
        finally
        {
            encoder.UnlockBitstream(bitStreamBuffer.BitstreamBuffer);
        }
    }

    public void Close(VideoInputDevice device)
    {
        Closing?.Invoke(device, this);

        encoder.DestroyEncoder();
        Utilities.Dispose(ref texture2D);
    }
}