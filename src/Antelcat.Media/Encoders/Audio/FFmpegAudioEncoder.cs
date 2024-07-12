using System.Collections.Generic;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using Antelcat.Media.Abstractions.Interfaces;
using Antelcat.Media.Internal;
using Antelcat.Media.Extensions;
using IEncoder = Antelcat.Media.Abstractions.Interfaces.IEncoder<
    Antelcat.Media.Abstractions.Interfaces.IAudioEncoder, 
    Antelcat.Media.Abstractions.AudioInputDevice, 
    Antelcat.Media.Abstractions.Enums.EncodedAudioFormat, 
    Antelcat.Media.Abstractions.RawAudioFrame, 
    Antelcat.Media.Abstractions.RawAudioPacket>;

namespace Antelcat.Media.Encoders.Audio;

public class FFmpegAudioEncoder : IAudioEncoder
{
    public IEnumerable<EncodedAudioFormat> SupportedFormats => new[] {
        EncodedAudioFormat.Aac, EncodedAudioFormat.Mp3, EncodedAudioFormat.Opus,
        EncodedAudioFormat.Pcma, EncodedAudioFormat.Pcmu, EncodedAudioFormat.Wma,
        EncodedAudioFormat.G729, EncodedAudioFormat.PcmS16Le
    };
    
    public EncodedAudioFormat Format { get; set; }

    public int Bitrate { get; set; } = 128000;

    public event IEncoder.OpeningHandler? Opening;
    public event IEncoder.FrameEncodedHandler? FrameEncoded;
    public event IEncoder.ClosingHandler? Closing;

    internal FFmpegEncoder Encoder { get; } = new();
    
    public void Open(AudioInputDevice device)
    {
        Encoder.Open(device, Format.ToCodecId(), Bitrate);
        Opening?.Invoke(device, this);
    }

    public void EncodeFrame(AudioInputDevice device, RawAudioFrame frame) 
    {
        foreach (var packet in Encoder.EncodeFrame(frame))
        {
            FrameEncoded?.Invoke(new FFmpegAudioPacket(packet, Format));
        }
    }

    public void Close(AudioInputDevice device)
    {
        Closing?.Invoke(device, this);
        Encoder.Close();
    }
}