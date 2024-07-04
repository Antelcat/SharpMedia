using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Antelcat.Media.Windows;

public class DisposeCollector<T> : IReadOnlyList<T>, IDisposable where T : IDisposable
{
    protected readonly List<T> disposables = [];
    protected bool isDisposed;

    ~DisposeCollector()
    {
        Dispose();
    }

    public T Add(T disposable) 
    {
        if (isDisposed) throw new ObjectDisposedException(nameof(DisposeCollector<T>));
        disposables.Add(disposable);
        return disposable;
    }
    
    public T Add(Func<T> factory) 
    {
        if (isDisposed) throw new ObjectDisposedException(nameof(DisposeCollector<T>));
        var disposable = factory();
        disposables.Add(disposable);
        return disposable;
    }

    public void RemoveAndDispose(ref T? disposable)
    {
        if (isDisposed) throw new ObjectDisposedException(nameof(DisposeCollector<T>));
        if (disposable == null) return;
        disposable.Dispose();
        if (!disposables.Remove(disposable)) return;
        disposable = default;
    }
    
    /// <summary>
    /// 清空后还能继续使用
    /// </summary>
    public void DisposeAndClear()
    {
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }
        disposables.Clear();
    }
    
    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;
        DisposeAndClear();
        GC.SuppressFinalize(this);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DisposeToDefault(ref T? disposable)
    {
        if (disposable is null) return;
        disposable.Dispose();
        disposable = default;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return disposables.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)disposables).GetEnumerator();
    }

    public int Count => disposables.Count;
    public T this[int index] => disposables[index];
}

public class DisposeCollector : DisposeCollector<IDisposable>
{
    public void Add(Action disposer) => Add(new AnonymousDisposable(disposer));
    
    public T Add<T>(T disposable) where T : IDisposable
    {
        base.Add(disposable);
        return disposable;
    }

    public void RemoveAndDispose<T>(ref T? disposable) where T : IDisposable
    {
        if (isDisposed) throw new ObjectDisposedException(nameof(DisposeCollector<T>));
        if (disposable == null) return;
        disposable.Dispose();
        if (!disposables.Remove(disposable)) return;
        disposable = default;
    }

    public void Replace<T>([NotNullIfNotNull(nameof(newDisposable))] ref T? oldDisposable, T? newDisposable) where T : IDisposable
    {
        if (isDisposed) throw new ObjectDisposedException(nameof(DisposeCollector<T>));
        if (oldDisposable != null)
        {
            oldDisposable.Dispose();
            disposables.Remove(oldDisposable);
        }
        
        oldDisposable = newDisposable;
        if (newDisposable == null) return;
        disposables.Add(newDisposable);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DisposeToDefault<T>(ref T? disposable) where T : IDisposable
    {
        if (disposable is null) return;
        disposable.Dispose();
        disposable = default;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FreeHGlobalToNull(ref IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return;
        Marshal.FreeHGlobal(ptr);
        ptr = IntPtr.Zero;
    }
}

public readonly struct AnonymousDisposable(Action disposer) : IDisposable
{
    public void Dispose()
    {
        disposer.Invoke();
    }
}

public readonly struct AnonymousDisposable<T>(T obj, Action<T> disposer) : IDisposable
{
    public void Dispose()
    {
        disposer.Invoke(obj);
    }
}