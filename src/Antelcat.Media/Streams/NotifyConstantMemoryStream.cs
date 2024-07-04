using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Antelcat.Media.Streams;

/// <summary>
/// Read的时候如果不满足大小，会阻塞，直到可读大小 >= 所需大小。
/// Write的时候同理
/// </summary>
public class NotifyConstantMemoryStream(ulong size) : ConstantMemoryStream(size)
{
	private readonly EventWaitHandle readWaitHandle = new AutoResetEvent(false);
	private TaskCompletionSource? writeTaskCompletionSource;
	private readonly object lockObj = new();

	/// <summary>
	/// 阻塞式读取
	/// </summary>
	/// <param name="buffer"></param>
	/// <param name="offset"></param>
	/// <param name="count"></param>
	/// <returns></returns>
	/// <exception cref="IOException"></exception>
	public override int Read(byte[] buffer, int offset, int count) {
		ValidateBufferArguments(buffer, offset, count);

		if (count == 0) {
			return 0;
		}

		if (count > buf.Length) {
			throw new IOException("Read count is more than buffer size.");  // 如果超出，那永远也读不了
		}

        while (true) {
            // 剩余可读多少
            var readableCount = isFull ? buf.LongLength : writePtr >= readPtr ? writePtr - readPtr : writePtr + buf.LongLength - readPtr;
            if (count <= readableCount) {
                break;
            }

            readWaitHandle.WaitOne();
            if (isDisposed) {
	            return 0;
            }
        }

        var reachEndLength = buf.LongLength - readPtr;
		if (count < reachEndLength) {
			Array.Copy(buf, readPtr, buffer, offset, count);
			readPtr += count;
		} else if (count == reachEndLength) {
			Array.Copy(buf, readPtr, buffer, offset, count);
			readPtr = 0;
		} else {
			var firstReadCount = (int)(buf.LongLength - readPtr);
			Array.Copy(buf, readPtr, buffer, offset, firstReadCount);
			Array.Copy(buf, 0, buffer, offset + firstReadCount, count - firstReadCount);
			readPtr = count - firstReadCount;
		}

		isFull = false;
		lock (lockObj) {
			writeTaskCompletionSource?.TrySetResult();
		}

		return count;
	}

	/// <summary>
	/// 不阻塞写入，如果失败直接返回
	/// </summary>
	/// <param name="buffer"></param>
	/// <param name="offset"></param>
	/// <param name="count"></param>
	/// <returns></returns>
	public bool WriteNoBlock(byte[] buffer, int offset, int count) {
		ValidateBufferArguments(buffer, offset, count);

		if (count == 0) {
			return false;
		}

		if (WriteInternal(new ReadOnlyMemory<byte>(buffer, offset, count))) {
			readWaitHandle.Set();
			return true;
		}

		return false;
	}

	public override void Write(byte[] buffer, int offset, int count) {
		ValidateBufferArguments(buffer, offset, count);

		if (count == 0) {
			return;
		}

		while (true) {
			// 如果写入成功，就打破循环，不然一直尝试并阻塞
			if (WriteInternal(new ReadOnlyMemory<byte>(buffer, offset, count))) {
				break;
			}

			lock (lockObj) {
				writeTaskCompletionSource ??= new TaskCompletionSource();
			}

			writeTaskCompletionSource.Task.Wait();
			if (isDisposed) {
				return;
			}
		}

		readWaitHandle.Set();
	}

	public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
		ValidateBufferArguments(buffer, offset, count);

		if (count == 0) {
			return;
		}

		if (count > buf.Length) {
			throw new IOException("Read count is more than buffer size.");  // 如果超出，那永远也读不了
		}

		while (!cancellationToken.IsCancellationRequested) {
			// 如果写入成功，就打破循环，不然一直尝试并阻塞
			if (WriteInternal(new ReadOnlyMemory<byte>(buffer, offset, count))) {
				break;
			}

			lock (lockObj) {
				writeTaskCompletionSource ??= new TaskCompletionSource();
			}
			
			await writeTaskCompletionSource.Task;
			if (isDisposed) {
				return;
			}
		}

		readWaitHandle.Set();
	}

	public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
		if (buffer.Length == 0) {
			return;
		}

		if (buffer.Length > buf.Length) {
			throw new IOException("Write count is more than buffer size.");  // 如果超出，那永远也读不了
		}

		while (!cancellationToken.IsCancellationRequested) {
			// 如果写入成功，就打破循环，不然一直尝试并阻塞
			if (WriteInternal(buffer)) {
				break;
			}

			lock (lockObj) {
				writeTaskCompletionSource ??= new TaskCompletionSource();
			}

			await writeTaskCompletionSource.Task;
			if (isDisposed) {
				return;
			}
		}

		readWaitHandle.Set();
	}

	public override void Flush()
	{
		base.Flush();
		readWaitHandle.Set();
	}

	protected override void Dispose(bool disposing) {
	    isDisposed = true;
        readWaitHandle.Set();
        writeTaskCompletionSource?.TrySetResult();
        base.Dispose(disposing);
    }
}