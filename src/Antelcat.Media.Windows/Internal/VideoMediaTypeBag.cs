using Antelcat.Media.Abstractions.Enums;
using EasyPathology.Abstractions.DataTypes;
using SharpDX.MediaFoundation;

namespace Antelcat.Media.Windows.Internal;

internal record VideoMediaTypeBag(MediaType Type, int Width, int Height, Fraction FrameRate, VideoFrameFormat Format);