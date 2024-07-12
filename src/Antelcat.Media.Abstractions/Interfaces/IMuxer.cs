namespace Antelcat.Media.Abstractions.Interfaces; 

public interface IMuxer {
    void Open(string outputUrl);

    void Open(Stream outputStream, string format);

    void AddAudioEncoder(IAudioEncoder audioEncoder);

    void AddVideoEncoder(IVideoEncoder videoEncoder);
}