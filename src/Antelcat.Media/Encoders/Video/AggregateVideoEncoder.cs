// using System;
// using System.Collections.Generic;
// using System.Collections.Immutable;
// using System.Threading;
// using System.Threading.Tasks;
// using Antelcat.Media.Definitions;
// using IEncoder = Antelcat.Media.Definitions.IEncoder<
//     Antelcat.Media.Definitions.IVideoEncoder,
//     Antelcat.Media.Definitions.VideoInputDevice,
//     Antelcat.Media.Definitions.EncodedVideoFormat,
//     Antelcat.Media.Definitions.RawVideoFrame,
//     Antelcat.Media.Definitions.RawVideoPacket>;
//
// namespace Antelcat.Media.Encoders.Video;
//
// /// <summary>
// /// 多线程调用多个视频编码器协同工作
// /// </summary>
// public class AggregateVideoEncoder : IVideoEncoder {
//     [Obsolete($"这个无效，用不同编码器的{nameof(SupportedFormats)}代替")]
//     public IEnumerable<EncodedVideoFormat> SupportedFormats => throw new NotSupportedException();
//
//     [Obsolete($"这个无效，用不同编码器的{nameof(Format)}代替")]
//     public EncodedVideoFormat Format {
//         get => throw new NotSupportedException();
//         set => throw new NotSupportedException();
//     }
//
//     public long BitRate { get; set; }
//
//     public IReadOnlyCollection<IVideoEncoder> Encoders => encoders;
//
//     public event IEncoder.OpeningHandler? Opening;
//     public event IEncoder.FrameEncodedHandler? FrameEncoded;
//     public event IEncoder.ClosingHandler? Closing;
//
//     private List<IVideoEncoder> encoders = new();
//     private AutoResetEvent? encodeEvent;
//     private CancellationTokenSource? encodeCts;
//
//     public void Open(VideoInputDevice device) {
//         var immutableEncoders = encoders.ToImmutableList();
//         foreach (var encoder in immutableEncoders) {
//             encoder.Open(device);
//         }
//
//         encodeEvent = new AutoResetEvent(false);
//         encodeCts = new CancellationTokenSource();
//         Task.Factory.StartNew(
//             () => EncodeTask(immutableEncoders, encodeEvent, encodeCts.Token), 
//             TaskCreationOptions.LongRunning);
//     }
//
//     public void EncodeFrame(VideoInputDevice device, RawVideoFrame frame, TimeSpan time, TimeSpan duration) {
//         throw new NotImplementedException();
//     }
//
//     public void Close(VideoInputDevice device) {
//         encodeCts?.Cancel();
//         encodeEvent?.Set();
//     }
//
//     private void EncodeTask(
//         IVideoEncoder encoder, 
//         AutoResetEvent encodeEvent,
//         CancellationToken cancellationToken) {
//         
//         while (!cancellationToken.IsCancellationRequested) {
//             encodeEvent.WaitOne();
//             if (cancellationToken.IsCancellationRequested) {
//                 break;
//             }
//
//             encoder.EncodeFrame();
//         }
//         
//         encoder.Close();
//     }
// }