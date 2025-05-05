using Silk.NET.OpenCL;

// обёртка над Silk.NET.OpenCL для удобного использования в csharp
namespace OCLHelper;

internal static class CLHandle
{
    static public CL OCL = CL.GetApi();
    static public string AppendErrCode(string description, ErrorCodes code)
    {
        return description + $"{code}({(int)code})";
    }
}

interface IMemObject<T>
{
    public nint Handle { get; }
}

public class NDRange
{
    public uint Dimensions { get; }
    public nuint[] Sizes { get; } = [1, 1, 1];

    public NDRange()
    {
        Dimensions = 0;
        Sizes[0] = 0;
        Sizes[1] = 0;
        Sizes[2] = 0;
    }
    public NDRange(nuint size0)
    {
        Dimensions = 1;
        Sizes[0] = size0;
        Sizes[1] = 1;
        Sizes[2] = 1;
    }
    public NDRange(
        nuint size0,
        nuint size1)
    {
        Dimensions = 2;
        Sizes[0] = size0;
        Sizes[1] = size1;
        Sizes[2] = 1;
    }
    public NDRange(
        nuint size0,
        nuint size1,
        nuint size2)
    {
        Dimensions = 1;
        Sizes[0] = size0;
        Sizes[1] = size1;
        Sizes[2] = size2;
    }

    nuint this[int i]
    {
        get => Sizes[i];
    }
}
