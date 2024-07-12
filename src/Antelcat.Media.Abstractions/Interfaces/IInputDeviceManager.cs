namespace Antelcat.Media.Abstractions.Interfaces;

public interface IInputDeviceManager
{
    IEnumerable<MediaDeviceInformation> GetMicrophones();

    IEnumerable<MediaDeviceInformation> GetCameras();
    
    IEnumerable<MediaDeviceInformation> GetSpeakers();

    MediaDeviceInformation? GetDefaultMicrophone();
    
    MediaDeviceInformation? GetDefaultSpeaker();

    /// <summary>
    /// 使用所给的参数创建，如果失败会扔异常
    /// </summary>
    /// <param name="information"></param>
    /// <param name="waveFormat"></param>
    /// <returns></returns>
    AudioInputDevice? CreateMicrophone(MediaDeviceInformation information, AudioFrameFormat waveFormat);

	/// <summary>
	/// 使用所给的参数作为下限创建，如果失败返回null
	/// </summary>
	/// <param name="information"></param>
	/// <param name="createPreference"></param>
	/// <returns></returns>
	VideoInputDevice? CreateCamera(MediaDeviceInformation information, VideoInputDevice.CreatePreference createPreference);
}