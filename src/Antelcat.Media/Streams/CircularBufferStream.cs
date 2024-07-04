using System;
using System.Buffers;
using System.IO;

namespace Antelcat.Media.Streams;

/// <summary>
/// 一个循环缓冲区的流，可以用来做内存缓冲区，写入的时候会自动扩容，保证可以写，但读取之后的数据就不一定可以再读了
/// </summary>
public class CircularBufferStream : Stream
{
    private byte[] buffer;
    private int head;
    private int tail;

    private readonly ArrayPool<byte> arrayPool;

    public CircularBufferStream(int initialCapacity = 1024, ArrayPool<byte>? arrayPool = null)
    {
        this.arrayPool = arrayPool ??= ArrayPool<byte>.Shared;
        buffer = arrayPool.Rent(initialCapacity);
        head = 0;
        tail = 0;
    }

    ~CircularBufferStream()
    {
        arrayPool.Return(buffer);
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => true;
    
    /// <summary>
    /// 剩余可读取的长度
    /// </summary>
    public override long Length => (tail - head + Capacity) % Capacity;
    
    /// <summary>
    /// 累计读取的长度
    /// </summary>
    public override long Position
    {
        get => position;
        set => Seek(value - position, SeekOrigin.Current);
    }

    private long position;

    public int Capacity => buffer.Length;
    
    public int WriteableLength => (int)(Capacity - Length - 1);

    public override void Flush() { }

    public override int Read(byte[] output, int offset, int count)
    {
        ValidateBufferArguments(output, offset, count);

        var bytesRead = 0;
        if (head < tail)
        {
            bytesRead = Math.Min(tail - head, count);
            Array.Copy(buffer, head, output, offset, bytesRead);
            head += bytesRead;
        }
        else if (head > tail)
        {
            bytesRead = Math.Min(Capacity - head, count);
            Array.Copy(buffer, head, output, offset, bytesRead);
            head = (head + bytesRead) % Capacity;
            if (bytesRead >= count) return bytesRead;

            // 头尾分离，还需要再复制一次
            var additionalBytes = Math.Min(tail, count - bytesRead);
            Array.Copy(buffer, head, output, offset + bytesRead, additionalBytes);
            bytesRead += additionalBytes;
            head += additionalBytes;
        }

        position += bytesRead;
        return bytesRead;
    }

    public override void Write(byte[] input, int offset, int count)
    {
        ValidateBufferArguments(input, offset, count);

        EnsureCapacity(count);

        if (tail >= head)
        {
            var firstCopy = Math.Min(Capacity - tail, count);
            Array.Copy(input, offset, buffer, tail, firstCopy);
            tail = (tail + firstCopy) % Capacity;
            if (firstCopy >= count) return;

            // 头尾分离，还需要再复制一次
            var secondCopy = count - firstCopy;
            Array.Copy(input, offset + firstCopy, buffer, tail, secondCopy);
            tail += secondCopy;
        }
        else
        {
            Array.Copy(input, offset, buffer, tail, count);
            tail += count;
        }
    }

    private void EnsureCapacity(int additionalCount)
    {
        var oldCapacity = Capacity;
        var newCapacity = oldCapacity;
        while (newCapacity - Length - 1 < additionalCount)  // 留一个空位，用来区分头尾
        {
            newCapacity *= 2;
        }
        if (newCapacity == oldCapacity) return;

        var newBuffer = arrayPool.Rent(newCapacity);
        
        var (oldTail, oldHead, oldPosition) = (tail, head, position);
#if DEBUG
        var length = (int)Length;
        System.Diagnostics.Debug.Assert(Read(newBuffer, 0, length) == length);
#else
        _ = Read(newBuffer, 0, (int)Length);
#endif
        (tail, head, position) = (oldTail, oldHead, oldPosition);

        arrayPool.Return(buffer);
        buffer = newBuffer;
        if (tail < head)
        {
            // oldCapacity - head + tail = newCapacity - head + newTail;
            tail = (tail + Capacity - oldCapacity) % Capacity;
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:
            case SeekOrigin.End:
            {
                throw new NotSupportedException("Seek operation only supports SeekOrigin.Current in CircularBufferStream.");
            }
            case SeekOrigin.Current:
            {
                switch (offset)
                {
                    case < 0:
                    {
                        throw new NotSupportedException("Seek operation only supports positive offset.");
                    }
                    case > 0:
                    {
                        var newHead = (int)((head + offset + Capacity) % Capacity);
                        if (head <= tail && (newHead > tail || newHead < head) ||
                            head > tail && (newHead > tail && newHead < head) ||
                            offset > Capacity)
                        {
                            throw new InvalidOperationException("Seek operation went out of bounds of the written data.");
                        }
                
                        head = newHead;
                        break;
                    }
                }
                break;
            }
            default:
            {
                throw new ArgumentOutOfRangeException(nameof(origin));
            }
        }

        return position += offset;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public void Clear()
    {
        head = tail = 0;
    }

    public Span<byte> AsSpan()
    {
        if (head == tail) return Span<byte>.Empty;
        if (head < tail) return new Span<byte>(buffer, head, tail - head);
        throw new InvalidOperationException();
    }
}