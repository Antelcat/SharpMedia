namespace Antelcat.Media.Abstractions;

public interface IAudioEncoder : IEncoder<IAudioEncoder, AudioInputDevice, EncodedAudioFormat, RawAudioFrame, RawAudioPacket>;