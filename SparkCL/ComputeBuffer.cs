using Silk.NET.OpenCL;
using OCLHelper;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SparkCL;

unsafe interface IReadOnlyMemAccessor<T>
where T: unmanaged, INumber<T>
{
    internal T* _ptr { get; }
    int Length{ get; }
    T this[int i] { get; }
}

interface IMemAccessor<T> : IReadOnlyMemAccessor<T>
where T: unmanaged, INumber<T>
{
    new T this[int i] { get; set; }
}

public unsafe class Accessor<T> : IMemAccessor<T>, IDisposable
where T: unmanaged, INumber<T>
{
    internal T* ptr { get; }
    unsafe T* IReadOnlyMemAccessor<T>._ptr => ptr;
    private bool disposedValue;
    ComputeBuffer<T> _master;

    public int Length { get; private set; }


    public T this[int i]
    {
        // TODO: measure performace
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ptr[i];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => ptr[i] = value;
    }

    internal Accessor(ComputeBuffer<T> buffer, T* ptr, int length)
    {
        _master = buffer;
        this.ptr = ptr;
        Length = length;
    }

    public Span<T> AsSpan()
    {
        return new Span<T>(ptr, Length);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: освободить управляемое состояние (управляемые объекты)
                _master.UnmapAccessor(this);
            }

            // TODO: освободить неуправляемые ресурсы (неуправляемые объекты) и переопределить метод завершения
            // TODO: установить значение NULL для больших полей
            disposedValue = true;
        }
    }

    // // TODO: переопределить метод завершения, только если "Dispose(bool disposing)" содержит код для освобождения неуправляемых ресурсов
    // ~Accessor()
    // {
    //     // Не изменяйте этот код. Разместите код очистки в методе "Dispose(bool disposing)".
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Не изменяйте этот код. Разместите код очистки в методе "Dispose(bool disposing)".
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

public unsafe class ComputeBuffer<T>
where T: unmanaged, INumber<T>
{
    internal Buffer<T> _hostBuffer;
    // internal T[] _storage;
    // should be a pointer to _storage
    // internal T* _storage;
    // public Array<T> Array { get; }
    public int Length { get; private set; }

    public ComputeBuffer(ReadOnlySpan<T> in_array, MemFlags flags = MemFlags.ReadWrite)
    {
        Length = in_array.Length;
        _hostBuffer = Buffer<T>.NewCopyHost(Core.context!, MemFlags.ReadWrite, in_array);
    }
    public ComputeBuffer(int length, MemFlags flags = MemFlags.ReadWrite)
    {
        Length = length;
        _hostBuffer = new Buffer<T>(Core.context!, flags, (nuint)length);
    }
    
    /*
    public void SendToDevice(bool blocking = true)
    {
        Core.queue!.EnqueueCopyBuffer(_deviceBuffer, _hostBuffer, 0, 0, Length, out var ev);
        
        #if COLLECT_TIME
            Core.IOEvents.Add(ev);
        #endif
        if (blocking)
        {
            ev.Wait();
        }
    }
    */

    internal void UnmapAccessor(IReadOnlyMemAccessor<T> accessor)
    {
        Core.queue!.EnqueueUnmapMemObject(_hostBuffer, accessor._ptr, out _);
    }

    public Accessor<T> MapAccessor(MapFlags flags)
    {
        var ptr = Map(flags);
        return new Accessor<T>(this, ptr, Length);
    }
/*
    Memory(Buffer<T> buffer, T[] storage) {
        _buffer = buffer;
        _storage = storage;
    }
*/
    /*
    public Span<T> AsSpan()
    {
        return new Span<T>(_storage, Length);
    }
    */

    T* Map(
        MapFlags flags,
        bool blocking = true
    )
    {
        var res = (T*)Core.queue!.EnqueueMapBuffer(_hostBuffer, blocking, flags, 0, (nuint)Length, out var ev);
        #if COLLECT_TIME
            Core.IOEvents.Add(ev);
        #endif
        if (blocking)
        {
            ev.Wait();
        }
        return res;
    }

    //unsafe public Event Unmap()
    //{
    //    Core.queue!.EnqueueUnmapMemObject(buffer, mappedPtr, out var ev);
    //    return ev;
    //}

    /*
    public Event Read(
        bool blocking = true,
        Event[]? wait_list = null
    )
    {
        Core.queue!.EnqueueReadBuffer(_buffer, blocking, 0, AsSpan(), out var ev);
        #if COLLECT_TIME
            Core.IOEvents.Add(ev);
        #endif
        return ev;
    }

    public Event Write(
        bool blocking = true
    )
    {
        Core.queue!.EnqueueWriteBuffer(_buffer, blocking, 0, AsSpan(), out var ev);
        #if COLLECT_TIME
            Core.IOEvents.Add(ev);
        #endif
        return ev;
    }
    */

    public void ReadTo(
        Span<T> destination
    )
    {
        var acc = MapAccessor(MapFlags.Read);
        acc.AsSpan().CopyTo(destination);
        acc.Dispose();
    }

    public Event CopyTo(
        ComputeBuffer<T> destination,
        bool blocking = true,
        Event[]? waitList = null
    )
    {
        if (Length != destination.Length)
        {
            throw new Exception("Source and destination sizes doesn't match");
        }
        Core.queue!.EnqueueCopyBuffer(_hostBuffer, destination._hostBuffer, 0, 0, (nuint) Length, out var ev, waitList);
        #if COLLECT_TIME
            Core.KernEvents.Add(ev);
        #endif
        if (blocking)
        {
            ev.Wait();
        }
        return ev;
    }
/*
    public float DotHost(Memory<float> rhs)
    {
        float res = (float)BLAS.dot(
            (int) this.Count,
            new ReadOnlySpan<float>(    Array.Buf, (int)Count),
            new ReadOnlySpan<float>(rhs.Array.Buf, (int)Count)
        );
        return res;
    }

    public double DotHost(Memory<double> rhs)
    {
        double res = (double)BLAS.dot(
            (int)this.Count,
            new ReadOnlySpan<double>(Array.Buf, (int)Count),
            new ReadOnlySpan<double>(rhs.Array.Buf, (int)Count)
        );
        return res;
    }
*/
}
