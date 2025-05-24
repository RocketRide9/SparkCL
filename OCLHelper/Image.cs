using System.Numerics;
using Silk.NET.OpenCL;

namespace OCLHelper;

public class Image<T> : IMemObject<T>
where T : unmanaged, INumber<T>
{
    public nint Handle { get; }

    unsafe Image (Context context, MemFlags flags, ImageFormat imageFormat)
    {

    }
}
