using Antelcat.Media.Abstractions.Enums;

namespace Antelcat.Media.Abstractions.Interfaces;

public interface IVideoDecoder : IDecoder<IVideoDecoder, VideoFrameFormat, RawVideoFrame>;