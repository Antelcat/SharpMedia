using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Antelcat.Media.Abstractions.Extensions;
using Antelcat.Media.Abstractions.Interfaces;
using Promise = System.Threading.Tasks.TaskCompletionSource;

namespace Antelcat.Media.Abstractions;

/// <summary>
/// 录制设备
/// </summary>
public abstract class InputDevice : IStateMachine<InputDevice.State>
{
    public enum State
    {
        /// <summary>
        /// 设备可以OpenAsync
        /// </summary>
        Closed,

        /// <summary>
        /// 调用了OpenAsync，正在打开
        /// </summary>
        Opening,

        /// <summary>
        /// 已暂停，可随时Start录制
        /// </summary>
        Paused,

        /// <summary>
        /// 正在录制，不断输出数据
        /// </summary>
        Running,

        /// <summary>
        /// 调用了Closing，正在关闭。关闭后回到Closed状态
        /// </summary>
        Closing
    }

    /// <summary>
    /// 当录制发生错误时触发
    /// </summary>
    /// <param name="device"></param>
    /// <param name="exception"></param>
    public delegate void RecordErrorHandler(InputDevice device, Exception exception);

    /// <summary>
    /// 唯一标识id
    /// </summary>
    public string? Uid { get; protected init; }

    /// <summary>
    /// 当前设备是否可以调用OpenAsync
    /// </summary>
    public abstract bool IsReady { get; }

    /// <summary>
    /// 平均1s多少字节
    /// </summary>
    public abstract long AverageBytesPerSecond { get; }

    /// <summary>
    /// 自从Open之后录制了多长时间，Close清零
    /// </summary>
    public TimeSpan CurrentTime => sw.Elapsed;

    /// <summary>
    /// 处理错误
    /// </summary>
    public event RecordErrorHandler? ErrorOccurred;

    protected readonly ManualResetEvent waitHandle = new(false);
    private CancellationTokenSource? cts;
    private readonly Stopwatch sw = new();
    private Promise? closingPromise;

    /// <summary>
    /// <see cref="RunLoop"/>之前调用，进行准备
    /// </summary>
    protected abstract void Opening();

    /// <summary>
    /// Open之后在Task里面运行，重载之后应该使用无限循环，得到的帧需要调用ProcessFrame，直到token被取消
    /// </summary>
    /// <param name="cancellationToken"></param>
    protected abstract void RunLoop(CancellationToken cancellationToken);

    protected abstract void Closing();

#if DEBUG
    protected InputDevice()
    {
        StateChanging += (oldState, newState) =>
        {
            Debug.WriteLine($"{Uid} Changing from {oldState} to {newState}");
        };
    }
#endif

    /// <summary>
    /// 打开录制设备，这个时候设备就需要开启，如果有线程也需要开始工作，但是还不会开始编码，直到<see cref="Start"/>被调用。
    /// 也就是说，准备工作会在这里完成，<see cref="Start"/>应该在瞬间完成。
    /// </summary>
    public virtual async Task OpenAsync()
    {
        if (!IsReady)
        {
            throw new InvalidOperationException($"Call OpenAsync but Device {Uid} is not ready.");
        }

        ThrowAndClear();

        if (CurrentState != State.Closed)
        {
            return;
        }

        CurrentState = State.Opening;

        waitHandle.Reset();
        cts?.Cancel();
        cts = new CancellationTokenSource();

        await Task.Run(Opening); // 确保Opening执行完毕
        CurrentState = State.Paused;

        Task.Factory.StartNew(() =>
            {
                try
                {
                    RunLoop(cts.Token);
                }
                catch (Exception e)
                {
                    HandleRecordError(e);
                }

                CurrentState = State.Closing;
                try
                {
                    Closing();
                }
                catch (Exception e)
                {
                    HandleRecordError(e);
                }

                sw.Reset();
                CurrentState = State.Closed;

                if (closingPromise != null)
                {
                    closingPromise.TrySetResult();
                    closingPromise = null;
                }
            },
            TaskCreationOptions.LongRunning).Detach(HandleRecordError);
    }

    protected abstract void ProcessFrame(RawFrame frame, CancellationToken token);

    public virtual void Start()
    {
        ThrowAndClear();

        if (CurrentState != State.Paused)
        {
            return;
        }

        waitHandle.Set();
        sw.Start();
    }

    public virtual void Pause()
    {
        ThrowAndClear();

        if (CurrentState != State.Running)
        {
            return;
        }

        sw.Stop();
        waitHandle.Reset();
        CurrentState = State.Paused;
    }

    public virtual async Task CloseAsync()
    {
        ThrowAndClear();

        if (CurrentState is not State.Running and not State.Paused)
        {
            return;
        }

        CurrentState = State.Closing;
        closingPromise = new Promise();

        if (cts != null)
        {
            cts.Cancel();
            cts = null;
        }

        waitHandle.Set();

        await closingPromise.Task;
    }

    /// <summary>
    /// <see cref="HandleRecordError"/>未处理的异常存储在这里，Close之后抛出
    /// </summary>
    private ExceptionDispatchInfo? unhandledException;

    /// <summary>
    /// 处理错误
    /// </summary>
    /// <param name="exception"></param>
    /// <returns></returns>
    private void HandleRecordError(Exception exception)
    {
        if (ErrorOccurred == null)
        {
            unhandledException = ExceptionDispatchInfo.Capture(exception);
            return;
        }

        try
        {
            ErrorOccurred.Invoke(this, exception);
        }
        catch
        {
            unhandledException = ExceptionDispatchInfo.Capture(exception);
        }
    }

    private void ThrowAndClear()
    {
        if (unhandledException == null)
        {
            return;
        }

        try
        {
            unhandledException.Throw();
        }
        finally
        {
            unhandledException = null;
        }
    }

    public override string ToString()
    {
        return Uid ?? "Unknown";
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(obj, this))
        {
            return true;
        }

        return Uid != null && obj is InputDevice { Uid: not null } id && id.Uid == Uid;
    }

    public static bool operator ==(InputDevice? left, InputDevice? right)
    {
        return left?.Equals(right) ?? right is null;
    }

    public static bool operator !=(InputDevice? left, InputDevice? right)
    {
        return !(left == right);
    }

    public override int GetHashCode()
    {
        return Uid?.GetHashCode() ?? 0;
    }

    public State CurrentState
    {
        get => current;
        protected set
        {
            if (current == value)
            {
                return;
            }

            StateChanging?.Invoke(current, value);
            current = value;
        }
    }

    private State current;

    public event IStateMachine<State>.StateChangeHandler? StateChanging;
}