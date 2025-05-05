using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OCLHelper;

/// <summary>
/// Should be used as a replacement for csharp arrays, that
/// allows zero-copy when plugged into OpenCL buffer.
/// </summary>
/// <typeparam name="T"></typeparam>
[Obsolete("Allocate memory using OpenCL buffers with ALLOC_HOST flag instead")]
public unsafe class DeprecatedArray<T> : IDisposable, IEnumerable<T>
where T: unmanaged, INumber<T>
{
    public T* Buf { get; internal set; }
    public int Count { get; }
    public nuint ElementSize { get; }

    public DeprecatedArray (ReadOnlySpan<T> array)
    {
        ElementSize = (nuint)sizeof(T);
        Buf = (T*)NativeMemory.AlignedAlloc((nuint)array.Length * ElementSize, 4096);
        Count = array.Length;
        array.CopyTo(new Span<T>(Buf, array.Length));
    }

    public DeprecatedArray (int size)
    {
        ElementSize = (nuint)sizeof(T);
        Buf = (T*)NativeMemory.AlignedAlloc((nuint)size * ElementSize, 4096);
        Count = size;
    }

    public DeprecatedArray (StreamReader file)
    {
        var sizeStr = file.ReadLine();

        ElementSize = (nuint)sizeof(T);
        Count = int.Parse(sizeStr!);
        Buf = (T*)NativeMemory.AlignedAlloc((nuint)Count * ElementSize, 4096);

        for (int i = 0; i < (int)Count; i++)
        {
            var row = file.ReadLine();
            T elem;
            try
            {
                elem = T.Parse(row!, CultureInfo.InvariantCulture);
            }
            catch (SystemException)
            {
                throw new System.Exception($"i = {i}");
            }
            this[i] = elem;
        }
    }

    public T this[int i]
    {
        // TODO: measure performace
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Buf[i];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Buf[i] = value;
    }


    private bool disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            NativeMemory.AlignedFree(Buf);
            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    ~DeprecatedArray()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void CopyTo(DeprecatedArray<T> destination)
    {
        var sp = new Span<T>(Buf, this.Count);
        var sp2 = new Span<T>(destination.Buf, destination.Count);
        sp.CopyTo(sp2);
    }

    public Span<T> AsSpan()
    {
        return new Span<T>(Buf, this.Count);
    }
}
