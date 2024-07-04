namespace Antelcat.Media.Abstractions;

public interface IModifier<in TDevice, TFormat, TFrame> where TDevice : InputDevice
{
    /// <summary>
    /// 转换之后的Format
    /// </summary>
    /// <remarks>如果为null说明与输入相同</remarks>
    TFormat? TargetFormat { get; }

    /// <summary>
    /// 打开的时候执行
    /// </summary>
    /// <param name="device"></param>
    /// <param name="srcFormat">输入的格式，即前一个Modifier的输出格式</param>
    void Open(TDevice device, TFormat srcFormat);

    /// <summary>
    /// 修改每一帧数据
    /// </summary>
    /// <param name="device"></param>
    /// <param name="frame"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    TFrame ModifyFrame(TDevice device, TFrame frame, CancellationToken cancellationToken);

    /// <summary>
    /// 关闭的时候执行
    /// </summary>
    /// <param name="device"></param>
    void Close(TDevice device);
}