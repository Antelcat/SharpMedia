using Antelcat.Media.Abstractions.Enums;

namespace Antelcat.Media.Abstractions;

public interface IVideoModifier : IModifier<VideoInputDevice, VideoFrameFormat, RawVideoFrame> { }