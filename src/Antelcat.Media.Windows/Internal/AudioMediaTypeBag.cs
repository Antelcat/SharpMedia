using Antelcat.Media.Abstractions;
using SharpDX.MediaFoundation;

namespace Antelcat.Media.Windows.Internal;

internal record AudioMediaTypeBag(MediaType Type, AudioFrameFormat Format);