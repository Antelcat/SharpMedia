using System.IO;

namespace Antelcat.Media.Abstractions; 

public interface IMuxer {
    void Open(string outputUrl);

    void Open(Stream outputStream, string format);

    void AddAudioEncoder(IAudioEncoder audioEncoder);

    void AddVideoEncoder(IVideoEncoder videoEncoder);
}