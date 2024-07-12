using Antelcat.Media.Abstractions.Enums;

namespace Antelcat.Media.Abstractions.Interfaces;

public interface IAudioEncoder : IEncoder<IAudioEncoder, AudioInputDevice, EncodedAudioFormat, RawAudioFrame, RawAudioPacket>;