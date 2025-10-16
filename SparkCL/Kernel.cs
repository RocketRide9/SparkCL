using OCLHelper;
using Silk.NET.OpenCL;
using System.Diagnostics;
using System.Numerics;

namespace SparkCL;

public class ArgInfo
{
    public bool IsPointer { get; }
    public KernelArgAddressQualifier Qualifier { get; }
    public Type DataType { get; }
    public string TypeName { get; }

    public ArgInfo(string typeName, KernelArgAddressQualifier qualifier)
    {
        Qualifier = qualifier;
        TypeName = typeName;
        int base_end = typeName.LastIndexOf('*');
        if (base_end == typeName.Length - 1)
        {
            IsPointer = true;
            typeName = typeName[..base_end];
        } else {
            IsPointer = false;
        }

        DataType = typeName switch
        {
            "float" => typeof(float),
            "float4" => typeof(float),
            "double" => typeof(double),
            "int" => typeof(int),
            "uint" => typeof(uint),
            "uchar" => typeof(byte),
            "uchar4" => typeof(byte),
            "long" => typeof(long),
            _ => throw new NotImplementedException(),
        };
    }

    public bool IsEqualTo<T>(T some)
    where T: unmanaged, INumber<T>
    {
        return typeof(T) == DataType;
    }

    public bool IsEqualTo<T>(ComputeBuffer<T> _)
    where T: unmanaged, INumber<T>
    {
        return IsPointer && DataType == typeof(T);
    }
}

public class Kernel
{
    OCLHelper.Kernel Inner;
    public NDRange GlobalWork { get; set; }
    public NDRange LocalWork { get; set; }
    uint lastPushed = 0;

    /// Blocking - Поставить ядро в очередь на выполнение и подождать его
    /// waitList - Список событий, которые должны быть выполнены перед выполнением ядра.
    public Event Execute(
        bool blocking = true,
        Event[]? waitList = null
    )
    {
        Trace.Indent();
        var sw = Stopwatch.StartNew();
        Core.queue!.EnqueueNDRangeKernel(Inner, new NDRange(), GlobalWork, LocalWork, out var ev, waitList);
        // Trace.WriteLine($"Enqueue: {sw.ElapsedMilliseconds}");
#if COLLECT_TIME
        Core.KernEvents.Add(ev);
#endif
        if (blocking)
        {
            sw.Restart();       
            ev.Wait();
            // Trace.WriteLine($"Wait: {sw.ElapsedMilliseconds}");
        }
        Trace.Unindent();
        return ev;
    }

    public uint PushArg<T>(
        SparkCL.ComputeBuffer<T> mem)
    where T: unmanaged, INumber<T>
    {
        SetArg(lastPushed, mem);
        lastPushed++;
        return lastPushed;
    }

    public uint PushArg<T>(
        T arg)
    where T: unmanaged, INumber<T>
    {
        SetArg(lastPushed, arg);
        lastPushed++;
        return lastPushed;
    }

    public void SetArg<T>(
        uint idx,
        T arg)
    where T: unmanaged, INumber<T>
    {
        var info = GetArgInfo(idx);
        if (!info.IsEqualTo(arg))
        {
            throw new ArgumentException($"Expected \"{info.TypeName}\", got \"{typeof(T)}\"");
        }

        Inner.SetArg(idx, arg);
    }

    public void SetArg<T>(
        uint idx,
        SparkCL.ComputeBuffer<T> mem)
    where T: unmanaged, INumber<T>
    {
        var info = GetArgInfo(idx);
        if (!info.IsEqualTo(mem))
        {
            throw new ArgumentException($"Expected \"{info.TypeName}\", got \"{typeof(T)}*\"");
        }

        Inner.SetArg(idx, mem._deviceBuffer!);
    }

    public void SetSize<T>(
        uint idx,
        nuint sz)
    where T: unmanaged
    {
        var info = GetArgInfo(idx);
        if (!info.IsEqualTo(sz))
        {
            throw new ArgumentException($"Expected \"{info.TypeName}\", got \"{typeof(T)}\"");
        }

        Inner.SetSize<T>(idx, sz);
    }

    public ArgInfo GetArgInfo(uint arg_index)
    {
        var name = Inner.GetArgTypeName(arg_index);
        var qual = Inner.GetArgAddressQualifier(arg_index);
        return new ArgInfo(name, qual);
    }

    internal Kernel(OCLHelper.Kernel kernel, NDRange globalWork, NDRange localWork)
    {
        Inner = kernel;
        GlobalWork = globalWork;
        LocalWork = localWork;
    }
}
