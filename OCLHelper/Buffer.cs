using System.Numerics;
using Silk.NET.OpenCL;

using static OCLHelper.CLHandle;

namespace OCLHelper;

class Buffer<T> : IMemObject<T>
where T : unmanaged, INumber<T>
{
    public nint Handle { get; }
    public bool IsOnHost { get; }

    unsafe public static Buffer<T> NewCopyHost(Context context, MemFlags flags, ReadOnlySpan<T> initial)
    {
        nint handle;
        ErrorCodes err;
        fixed(T* array_p = initial)
        {
            handle = OCL.CreateBuffer(
                context.Handle,
                MemFlags.CopyHostPtr | flags,
                (nuint) sizeof(T) * (nuint)initial.Length,
                array_p,
                (int *)&err
            );
        }
        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode("Failed to create buffer, code: ", err));
        }

        var isOnHost = flags.HasFlag(MemFlags.AllocHostPtr) || flags.HasFlag(MemFlags.UseHostPtr);
        return new Buffer<T>(handle, isOnHost);
    }

    unsafe public static Buffer<T> NewAllocHost(Context context, MemFlags flags, nuint length)
    {
        ErrorCodes err;
        nint handle;
        handle = OCL.CreateBuffer(
            context.Handle,
            MemFlags.AllocHostPtr | flags,
            (nuint) sizeof(T) * length,
            null,
            (int *)&err
        );

        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode("Failed to create buffer, code: ", err));
        }

        return new Buffer<T>(handle, true);
    }

    unsafe public Buffer(Context context, MemFlags flags, nuint length)
    {
        ErrorCodes err;
        Handle = OCL.CreateBuffer(
            context.Handle,
            flags,
            (nuint) sizeof(T) * (nuint)length,
            null,
            (int *)&err
        );
        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode("Failed to create buffer, code: ", err));
        }
        IsOnHost = flags.HasFlag(MemFlags.AllocHostPtr) || flags.HasFlag(MemFlags.UseHostPtr);
    }

    Buffer(nint handle, bool isOnHost)
    {
        IsOnHost = isOnHost;
        Handle = handle;
    }

    ~Buffer()
    {
        OCL.ReleaseMemObject(Handle);
    }
}
