using System.Diagnostics;

using OCLHelper;
using Silk.NET.OpenCL;

// идея сократить область применения до вычисления на одном устройстве.
// это должно упростить использование OpenCL, абстрагируя понятия контекста,
// очереди команд и устройства.
namespace SparkCL;

public static class EventExt
{
    public static ulong GetElapsed(this OCLHelper.Event @event)
    {
        var s = @event.GetProfilingInfo(ProfilingInfo.Start);
        var c = @event.GetProfilingInfo(ProfilingInfo.End);

        return c - s;
    }
}

static public class StarterKit
{
    // создать объекты на первом попавшемся GPU
    static public void GetStarterKit(
        out OCLHelper.Context context,
        out OCLHelper.Device device,
        out OCLHelper.CommandQueue commandQueue)
    {
        var platforms = Platform.GetDiscovered();

        Platform platform;
        // Avoid Clover if possible
        if (platforms[0].GetName() == "Clover" && platforms.Length > 1)
        {
            platform = platforms[1];
        } else {
            platform = platforms[0];
        }

        Console.WriteLine($"Platform: {platform.GetName()}");
        Console.WriteLine($"Version: {platform.GetVersion()}");

        device = platform.GetDevicesOfType(DeviceType.Gpu).First();

        Console.WriteLine($"Device: {device.GetName()}");

        context = Context.ForDevices([device]);

        QueueProperties[] properties = [
            (QueueProperties)CommandQueueInfo.Properties, (QueueProperties) CommandQueueProperties.ProfilingEnable,
            0
        ];
        commandQueue = new CommandQueue(context, device, properties);
    }
}

static public class Core
{
    public const nuint Prefered1D = 32;

    static public Context? context;
    static public CommandQueue? queue;
    static public OCLHelper.Device? device;
#if COLLECT_TIME
    static public List<Event> IOEvents { get; private set; } = new(32);
    static public List<Event> KernEvents { get; private set; } = new(32);
#endif
    static public void Init()
    {
        Trace.Indent();
        var sw = Stopwatch.StartNew();
        var platforms = Platform.GetDiscovered();
        Trace.WriteLine($"Discover platforms: {sw.ElapsedMilliseconds}");
        Platform platform;
        // Avoid Clover if possible
        if (platforms[0].GetName() == "Clover" && platforms.Length > 1)
        {
            platform = platforms[1];
        } else {
            platform = platforms[0];
        }

        Console.WriteLine($"Platform: {platform.GetName()}");
        Console.WriteLine($"Version: {platform.GetVersion()}");

        sw.Restart();
        device = platform.GetDevicesOfType(DeviceType.Gpu).First();
        Trace.WriteLine($"List devices: {sw.ElapsedMilliseconds}");
        Console.WriteLine($"Device: {device.GetName()}");

        sw.Restart();
        context = Context.ForDevices([device]);
        Trace.WriteLine($"Create context: {sw.ElapsedMilliseconds}");
        
        QueueProperties[] properties = [
            (QueueProperties)CommandQueueInfo.Properties, (QueueProperties) CommandQueueProperties.ProfilingEnable,
            0
        ];
        sw.Restart();
        queue = new CommandQueue(context, device, properties);
        Trace.WriteLine($"Create queue: {sw.ElapsedMilliseconds}");
        
        Trace.Unindent();
    }

    #if COLLECT_TIME
    // Должна быть вызвана после завершения всех операций на устройстве
    static public (ulong IOTime, ulong KernTime) MeasureTime()
    {
        ulong IO = 0;
        ulong Kern = 0;

        foreach (var ev in IOEvents)
        {
            IO += ev.GetElapsed();
        }
        foreach (var ev in KernEvents)
        {
            Kern += ev.GetElapsed();
        }
        KernEvents.Clear();
        IOEvents.Clear();

        return (IO, Kern);
    }
    static public void ResetTime()
    {
        KernEvents.Clear();
        IOEvents.Clear();
    }
    #endif

    static public void WaitQueue()
    {
        queue!.Finish();
    }
}
