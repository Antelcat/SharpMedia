using Antelcat.Media.Abstractions.Enums;

namespace Antelcat.Media.Abstractions;

public interface IVideoDecoder : IDecoder<IVideoDecoder, VideoFrameFormat, RawVideoFrame>;