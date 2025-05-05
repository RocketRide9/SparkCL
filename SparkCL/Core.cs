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
        var s = @event.GetProfilingInfo(ProfilingInfo.Queued);
        var c = @event.GetProfilingInfo(ProfilingInfo.Complete);

        return c - s;
    }
}

static class StarterKit
{
    // создать объекты на первом попавшемся GPU
    static public void GetStarterKit(
        out OCLHelper.Context context,
        out OCLHelper.Device device,
        out OCLHelper.CommandQueue commandQueue)
    {
        var platform = Platform.GetDiscovered().First();

        device = platform.GetDevicesOfType(DeviceType.Gpu).First();
        context = Context.FromDevice(device);

        QueueProperties[] properties = [
            (QueueProperties)CommandQueueInfo.Properties, (QueueProperties) CommandQueueProperties.ProfilingEnable,
            0
        ];
        commandQueue = new CommandQueue(context, device, properties);
    }
}

static public class Core
{
    static internal Context? context;
    static internal CommandQueue? queue;
    static internal OCLHelper.Device? device;

    static public List<Event> IOEvents { get; private set; } = new(32);
    static public List<Event> KernEvents { get; private set; } = new(32);

    static public void Init()
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

        context = Context.FromDevice(device);

        QueueProperties[] properties = [
            (QueueProperties)CommandQueueInfo.Properties, (QueueProperties) CommandQueueProperties.ProfilingEnable,
            0
        ];
        queue = new CommandQueue(context, device, properties);
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
