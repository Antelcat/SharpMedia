using System.Diagnostics;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Windows.Abstractions.Interfaces;
using Antelcat.Media.Windows.Extensions;
using SharpDX;
using SharpDX.MediaFoundation;

namespace Antelcat.Media.Windows.Muxers;

public class MediaFoundationMuxer : IMuxer, IDisposable {
	private ByteStream? byteStream;
	private string? outputUrl;
	private int encoderCount, openedEncoderCount;
	private SinkWriter? sinkWriter;

	static MediaFoundationMuxer() {
		MediaManager.Startup();
	}

	public void Open(string outputUrl) {
		this.outputUrl = outputUrl;
	}

	public void Open(Stream outputStream, string format) {
		byteStream = new ByteStream(outputStream);
	}

	~MediaFoundationMuxer() {
		Dispose();
	}

	public void AddAudioEncoder(IAudioEncoder audioEncoder) {
		if (audioEncoder is IMediaFoundationAudioEncoder) {
			audioEncoder.Opening += AudioEncoder_OnOpening;
			audioEncoder.Closing += (_, _) => Encoder_OnClosing();
			encoderCount++;
		} else {
			throw new NotSupportedException();
		}
	}

	public void AddVideoEncoder(IVideoEncoder videoEncoder) {
		if (videoEncoder is IMediaFoundationVideoEncoder) {
			videoEncoder.Opening += VideoEncoder_OnOpening;
			videoEncoder.Closing += (_, _) => Encoder_OnClosing();
			encoderCount++;
		} else {
			throw new NotSupportedException();
		}
	}

	private void VideoEncoder_OnOpening(VideoInputDevice device, IVideoEncoder encoder) {
		var videoEncoder = (IMediaFoundationVideoEncoder)encoder;
		var streamIndex = AddStream(videoEncoder.InputMediaType, videoEncoder.OutputMediaType);
		videoEncoder.FrameEncoded += packet => Encoder_OnFrameEncoded(streamIndex, (IMediaFoundationPacket)packet);
	}

	private void AudioEncoder_OnOpening(AudioInputDevice device, IAudioEncoder encoder) {
		var audioEncoder = (IMediaFoundationAudioEncoder)encoder;
		var streamIndex = AddStream(audioEncoder.InputMediaType, audioEncoder.OutputMediaType);
		audioEncoder.FrameEncoded += packet => Encoder_OnFrameEncoded(streamIndex, (IMediaFoundationPacket)packet);
	}

	private void Encoder_OnFrameEncoded(int streamIndex, IMediaFoundationPacket packet) {
		sinkWriter?.WriteSample(streamIndex, packet.Sample);
	}

	private void Encoder_OnClosing() {
		if (--openedEncoderCount == 0 && sinkWriter != null) {
			sinkWriter.Finalize();
			sinkWriter = null;
		}
	}

	private int AddStream(MediaType inputMediaType, MediaType outputMediaType) {
		sinkWriter ??= MediaFactory.CreateSinkWriterFromURL(outputUrl, byteStream, null);
#if DEBUG
		Debug.WriteLine("\noutputMediaType:");
		outputMediaType.Dump();
		Debug.WriteLine("\ninputMediaType:");
		inputMediaType.Dump();
#endif
		sinkWriter.AddStream(outputMediaType, out var streamIndex);
		sinkWriter.SetInputMediaType(streamIndex, inputMediaType, null);
		if (++openedEncoderCount == encoderCount) {
			sinkWriter.BeginWriting();
		}

		return streamIndex;
	}

	public void Dispose() {
		byteStream?.Dispose();
		Utilities.Dispose(ref sinkWriter);
		GC.SuppressFinalize(this);
	}
}
