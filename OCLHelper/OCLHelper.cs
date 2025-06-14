using System.Net.Http.Headers;
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

public interface IMemObject<T>
{
    public nint Handle { get; }
}

public class NDRange
{
    public static nuint PaddedTo(int initial, int multiplier)
    {
        if (initial % multiplier == 0)
        {
            return (nuint)initial;
        } else {
            return ((nuint)(initial / multiplier) + 1 ) * (nuint)multiplier;
        }
    }

    public NDRange PadTo(int multiplier)
    {
        for (int i = 0; i < 3; i++)
        {
            Sizes[i] = (Sizes[i] / (nuint)multiplier + 1 ) * (nuint)multiplier;
        }

        return this;
    }
    
    public NDRange PadTo(nuint multiplier)
    {
        for (int i = 0; i < 3; i++)
        {
            Sizes[i] = (Sizes[i] / multiplier + 1 ) * multiplier;
        }

        return this;
    }
    
    public uint Dimensions { get; }
    public nuint[] Sizes { get; } = new nuint[3];

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
        Dimensions = 3;
        Sizes[0] = size0;
        Sizes[1] = size1;
        Sizes[2] = size2;
    }

    nuint this[int i]
    {
        get => Sizes[i];
    }
}
