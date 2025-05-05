using System.Numerics;
using Silk.NET.OpenCL;

using static OCLHelper.CLHandle;

namespace OCLHelper;

class CommandQueue
{
    public nint Handle { get; }

    public unsafe CommandQueue(Context context, Device device, QueueProperties[] properties)
    {
        // properties example:
        // QueueProperties[] properties = [
        //     (QueueProperties)CommandQueueInfo.Properties, (QueueProperties) CommandQueueProperties.ProfilingEnable,
        //     0
        // ];

        ErrorCodes err;
        fixed (QueueProperties *p = properties)
        {
            Handle = OCL.CreateCommandQueueWithProperties(context.Handle, device.Handle, p, (int *)&err);
        }

        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode("Couldn't create command queue, code: ", err));
        }
    }

    public void Finish()
    {
        var err = (ErrorCodes)OCL.Finish(Handle);

        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode("Couldn't finish command queue, code: ", err));
        }
    }

    private static nint[]? Nintize(Event[]? evs)
    {
        return evs?.Select((ev, i) => ev.Handle).ToArray();
    }

    public unsafe void EnqueueNDRangeKernel(
        Kernel kernel,
        NDRange offset,
        NDRange global,
        NDRange local,
        out Event @event,
        Event[]? wait_list = null)
    {

        ErrorCodes err;
        nint event_h;
        fixed (nuint *g = global.Sizes)
        fixed (nuint *o = offset.Sizes)
        fixed (nuint *l = local.Sizes)
        fixed (nint *wait_list_p = Nintize(wait_list))
        {
            err = (ErrorCodes)OCL.EnqueueNdrangeKernel(
                Handle,
                kernel.Handle,
                global.Dimensions,
                offset.Dimensions != 0 ? o : null,
                g,
                l,
                wait_list == null ? 0 : (uint) wait_list.Length,
                wait_list == null ? null : wait_list_p,
                &event_h);
        }
        @event = new Event(event_h);

        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode("Couldn't enqueue kernel, code: ", err));
        }
    }

    public unsafe void* EnqueueMapBuffer<T>(
        Buffer<T> buffer,
        bool blocking,
        MapFlags flags,
        nuint offset,
        nuint count,
        out Event @event)
    where T : unmanaged, INumber<T>
    {
        nint event_h;
        ErrorCodes err;
        var ptr = OCL.EnqueueMapBuffer(
            Handle,
            buffer.Handle,
            blocking,
            flags,
            offset,
            count * (nuint) sizeof(T),
            0,
            null,
            out event_h,
            (int *)&err);

        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode("Couldn't enqueue buffer map, code: ", err));
        }
        @event = new Event(event_h);

        return ptr;
    }

    public unsafe void EnqueueUnmapMemObject<T>(
        IMemObject<T> buffer,
        void *ptr,
        out Event @event)
    where T : unmanaged, INumber<T>
    {

        nint event_h;
        var err = (ErrorCodes)OCL.EnqueueUnmapMemObject(
            Handle,
            buffer.Handle,
            ptr,
            0,
            null,
            out event_h);

        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode("Couldn't enqueue memory object unmap, code: ", err));
        }
        @event = new Event(event_h);
    }

    public unsafe void EnqueueReadBuffer<T>(
        Buffer<T> buffer,
        bool blocking,
        nuint offset,
        Span<T> array,
        out Event @event,
        Event[]? wait_list = null)
    where T : unmanaged, INumber<T>
    {
        nint event_h;
        ErrorCodes err;
        fixed (nint *wait_list_p = Nintize(wait_list))
        fixed (T *array_p = array)
        {
            err = (ErrorCodes)OCL.EnqueueReadBuffer(
                Handle,
                buffer.Handle,
                blocking,
                offset,
                (nuint) array.Length * (nuint) sizeof(T),
                array_p,
                wait_list == null ? 0 : (uint) wait_list.Length,
                wait_list == null ? null : wait_list_p,
                out event_h);
        }
        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode("Couldn't enqueue buffer read, code: ", err));
        }
        @event = new Event(event_h);
    }

    public unsafe void EnqueueWriteBuffer<T>(
        Buffer<T> buffer,
        bool blocking,
        nuint offset,
        ReadOnlySpan<T> array,
        out Event @event)
    where T : unmanaged, INumber<T>
    {

        nint event_h;
        ErrorCodes err;
        fixed (T* array_p = array)
        {
            err = (ErrorCodes)OCL.EnqueueWriteBuffer(
                Handle,
                buffer.Handle,
                blocking,
                offset,
                (nuint) array.Length * (nuint) sizeof(T),
                array_p,
                0,
                null,
                out event_h);
        }

        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode("Couldn't enqueue buffer read, code: ", err));
        }
        @event = new Event(event_h);
    }

    public unsafe void EnqueueCopyBuffer<T>(
        IMemObject<T> src,
        IMemObject<T> dst,
        nuint src_offset,
        nuint dst_offset,
        nuint count,
        out Event @event,
        Event[]? wait_list = null)
    where T : unmanaged, INumber<T>
    {
        nint event_h;
        ErrorCodes err;
        fixed (nint *wait_list_p = Nintize(wait_list))
        {
            err = (ErrorCodes)OCL.EnqueueCopyBuffer(
                Handle,
                src.Handle,
                dst.Handle,
                src_offset,
                dst_offset,
                count * (nuint) sizeof(T),
                wait_list == null ? 0 : (uint) wait_list.Length,
                wait_list == null ? null : wait_list_p,
                out event_h);
        }

        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode("Couldn't enqueue memory object unmap, code: ", err));
        }
        @event = new Event(event_h);
    }

    private CommandQueue(nint h)
    {
        Handle = h;
    }

    ~CommandQueue()
    {
        OCL.ReleaseCommandQueue(Handle);
    }
}
