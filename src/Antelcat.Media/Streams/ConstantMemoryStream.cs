using System;
using System.IO;

namespace Antelcat.Media.Streams;

/// <summary>
/// 固定大小的Stream，如果写满会扔异常
/// </summary>
public class ConstantMemoryStream : Stream
{
    public ulong Capacity { get; }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => true;

    /// <summary>
    /// 可读取的长度，也就是数据的长度
    /// </summary>
    public override long Length => isFull ? buf.LongLength :
        writePtr >= readPtr ? writePtr - readPtr : writePtr + buf.LongLength - readPtr;

    public override long Position
    {
        get => writePtr;
        set => throw new NotSupportedException();
    }

    protected readonly byte[] buf;

    protected long writePtr, readPtr;

    protected bool isFull, isDisposed;

    public ConstantMemoryStream(ulong size)
    {
        buf = new byte[Capacity = size];
    }

    /// <summary>
    /// 清空数据
    /// </summary>
    public override void Flush()
    {
        readPtr = writePtr;
        isFull = false;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);

        // 剩余可读多少
        var readableCount = Length;
        if (readableCount == 0)
        {
            return 0;
        }

        if (count > readableCount)
        {
            count = (int)readableCount;
        }

        var reachEndLength = buf.LongLength - readPtr;
        if (count < reachEndLength)
        {
            Array.Copy(buf, readPtr, buffer, offset, count);
            readPtr += count;
        }
        else if (count == reachEndLength)
        {
            Array.Copy(buf, readPtr, buffer, offset, count);
            readPtr = 0;
        }
        else
        {
            var firstReadCount = (int)(buf.LongLength - readPtr);
            Array.Copy(buf, readPtr, buffer, offset, firstReadCount);
            Array.Copy(buf, 0, buffer, offset + firstReadCount, count - firstReadCount);
            readPtr = count - firstReadCount;
        }

        isFull = false;

        return count;
    }

    public virtual int Read(IntPtr buffer, int offset, int count)
    {
        // 剩余可读多少
        var readableCount = Length;
        if (readableCount == 0)
        {
            return 0;
        }

        if (count > readableCount)
        {
            count = (int)readableCount;
        }

        var reachEndLength = buf.LongLength - readPtr;
        unsafe
        {
            fixed (byte* bufPtr = buf)
            {
                if (count < reachEndLength)
                {
                    Buffer.MemoryCopy(bufPtr + readPtr, (buffer + offset).ToPointer(), count, count);
                    readPtr += count;
                }
                else if (count == reachEndLength)
                {
                    Buffer.MemoryCopy(bufPtr + readPtr, (buffer + offset).ToPointer(), count, count);
                    readPtr = 0;
                }
                else
                {
                    var firstReadCount = (int)(buf.LongLength - readPtr);
                    Buffer.MemoryCopy(bufPtr + readPtr, (buffer + offset).ToPointer(), count, firstReadCount);
                    Buffer.MemoryCopy(bufPtr, (buffer + offset + firstReadCount).ToPointer(), count - firstReadCount, count - firstReadCount);
                    readPtr = count - firstReadCount;
                }
            }
        }

        isFull = false;

        return count;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (origin != SeekOrigin.Current)
        {
            throw new NotSupportedException();
        }

        if (offset < 0)
        {
            if (-offset > Length)
            {
                throw new IOException();
            }

            writePtr += offset;
            if (writePtr < 0)
            {
                writePtr += buf.LongLength;
            }
        }

        return Position;
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);

        if (count == 0)
        {
            return;
        }

        if (!WriteInternal(new ReadOnlyMemory<byte>(buffer, offset, count)))
        {
            throw new BufferFullException(buf.LongLength, Length);
        }
    }

    public void Write(IntPtr buffer, int offset, int count)
    {
        if (count == 0)
        {
            return;
        }

        if (!WriteInternal(buffer + offset, count))
        {
            throw new BufferFullException(buf.LongLength, Length);
        }
    }

    protected unsafe bool WriteInternal(ReadOnlyMemory<byte> memory)
    {
        using var pin = memory.Pin();
        return WriteInternal(new IntPtr(pin.Pointer), memory.Length);
    }

    protected unsafe bool WriteInternal(IntPtr buffer, int length)
    {
        var remainingLength = buf.LongLength - Length;
        if (isFull || length > remainingLength)
        {
            return false;
        }

        var reachEndLength = buf.LongLength - writePtr;
        fixed (byte* bufPtr = buf)
        {
            if (length < reachEndLength)
            {
                Buffer.MemoryCopy(buffer.ToPointer(), bufPtr + writePtr, reachEndLength, length);
                writePtr += length;
            }
            else if (length == reachEndLength)
            {
                Buffer.MemoryCopy(buffer.ToPointer(), bufPtr + writePtr, reachEndLength, length);
                writePtr = 0;
            }
            else
            {
                var firstWriteCount = (int)(buf.LongLength - writePtr);
                Buffer.MemoryCopy(buffer.ToPointer(), bufPtr + writePtr, firstWriteCount, firstWriteCount);
                Buffer.MemoryCopy((byte*)buffer.ToPointer() + firstWriteCount, bufPtr, length - firstWriteCount, length - firstWriteCount);
                writePtr = length - firstWriteCount;
            }
        }

        if (length == remainingLength)
        {
            isFull = true;
        }

        return true;
    }
}

public class BufferFullException(double? maximum, double? current) : IOException
{
    public override string Message => $"This buffer is full by Maximum:{maximum} when Current:{current}";
}