using Silk.NET.OpenCL;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using static SparkOCL.CLHandle;
using System.Linq;
using System.Collections;

// обёртка над Silk.NET.OpenCL для удобного использования в csharp
namespace SparkOCL
{
    internal static class CLHandle
    {
        static public CL OCL = CL.GetApi();
        static public string AppendErrCode(string description, ErrorCodes code)
        {
            return description + $"{code}({(int)code})";
        }
    }

    public class Event: IDisposable
    {
        private bool disposedValue;

        public nint Handle { get; }

        unsafe public ulong GetProfilingInfo(
            ProfilingInfo info
        )
        {
            ulong time;
            var err = (ErrorCodes)OCL.GetEventProfilingInfo(Handle, info, 8, &time, null);

            if (err != ErrorCodes.Success)
            {
                throw new Exception(AppendErrCode("Couldn't get profiling info, code: ", err));
            }

            return time;
        }
        
        unsafe public void Wait()
        {

            var handle = Handle;
            var err = (ErrorCodes)OCL.WaitForEvents(1, &handle);
            
            if (err != ErrorCodes.Success)
            {
                throw new Exception(AppendErrCode("Couldn't wait for event, code: ", err));
            }
        }

        internal Event(nint h)
        {
            Handle = h;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: освободить управляемое состояние (управляемые объекты)
                }

                // TODO: освободить неуправляемые ресурсы (неуправляемые объекты) и переопределить метод завершения
                // TODO: установить значение NULL для больших полей
                OCL.ReleaseEvent(Handle);
                disposedValue = true;
            }
        }

        // TODO: переопределить метод завершения, только если "Dispose(bool disposing)" содержит код для освобождения неуправляемых ресурсов
        ~Event()
        {
            // Не изменяйте этот код. Разместите код очистки в методе "Dispose(bool disposing)".
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Не изменяйте этот код. Разместите код очистки в методе "Dispose(bool disposing)".
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    class Context
    {
        public nint Handle { get; }

        unsafe static public Context FromDevice(
            Device device)
        {
            var device_h = device.Handle;
            ErrorCodes err;
            var h = OCL.CreateContext(null, 1, &device_h, null, null, (int *)&err);

            if (err != ErrorCodes.Success)
            {
                throw new Exception(AppendErrCode("Couldn't create context on requested device, code: ", err));
            }

            var res = new Context(h);
            return res;
        }

        unsafe static public Context FromType(
            DeviceType type)
        {

            var platforms = new List<Platform>();
            Platform.Get(out platforms);
            
            nint[] contextProperties =
            [
                (nint)ContextProperties.Platform,
                platforms[0].Handle,
                0
            ];

            fixed (nint* p = contextProperties)
            {
                ErrorCodes err;
                var context_handle = OCL.CreateContextFromType(p, DeviceType.Gpu, null, null,  (int *)&err);
                if (err != ErrorCodes.Success)
                {
                    throw new Exception(AppendErrCode("Couldn't create context on requested device type, code: {errNum}", err));
                }

                return new Context(context_handle);
            }
        }

        private Context(nint h)
        {
            Handle = h;
        }

        ~Context()
        {
            OCL.ReleaseContext(Handle);
        }
    }

    class Platform
    {
        public nint Handle { get; }
        unsafe public static void Get(out List<Platform> platforms)
        {
            uint n = 0;
            var err = (ErrorCodes)OCL.GetPlatformIDs(0, null, &n);
            if (err != ErrorCodes.Success)
            {
                throw new Exception(AppendErrCode("Couldn't get platform ids, code: ", err));
            }

            var ids = new nint[n];
            err = (ErrorCodes)OCL.GetPlatformIDs(n, ids, (uint *)null);
            if (err != ErrorCodes.Success)
            {
                throw new Exception(AppendErrCode("Couldn't get platform ids, code: ", err));
            }

            platforms = new()
            {
                Capacity = (int)n
            };
            for (int i = 0; i < n; i++)
            {
                var p = new Platform(ids[i]);
                platforms.Add(p);
            }
        }

        unsafe public void GetDevices(DeviceType type, out List<Device> devices)
        {
            uint n = 0;
            var err = (ErrorCodes)OCL.GetDeviceIDs(Handle, type, 0, null, &n);
            if (err != ErrorCodes.Success)
            {
                throw new Exception(AppendErrCode("Couldn't get devices ID, code: ", err));
            }

            var ids = new nint[n];
            err = (ErrorCodes)OCL.GetDeviceIDs(Handle, type, n, ids, (uint *)null);
            if (err != ErrorCodes.Success)
            {
                throw new Exception(AppendErrCode("Couldn't get devices ID, code: ", err));
            }

            devices = new((int) n);
            for (int i = 0; i < n; i++)
            {
                var p = new Device(ids[i]);
                devices.Add(p);
            }
        }

        private Platform(nint h)
        {
            Handle = h;
        }
    }

    class Device
    {
        DeviceType Type { get; }
        public nint Handle { get; }

        internal Device(nint h)
        {
            Handle = h;
        }

        ~Device()
        {
            OCL.ReleaseDevice(Handle);
        }
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

    class CommandQueue
    {
        public nint Handle { get; }

        public unsafe CommandQueue(Context context, Device device)
        {

            ErrorCodes err;
            QueueProperties[] props = [
                (QueueProperties)CommandQueueInfo.Properties, (QueueProperties) CommandQueueProperties.ProfilingEnable,
                0
            ];
            fixed (QueueProperties *p = props)
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
        where T : unmanaged
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

        public unsafe void EnqueueReadBuffer<T>(
            Buffer<T> buffer,
            bool blocking,
            nuint offset,
            Array<T> array,
            out Event @event,
            Event[]? wait_list = null)
        where T : unmanaged
        {

            nint event_h;
            ErrorCodes err;
            fixed (nint *wait_list_p = Nintize(wait_list))
            {
                err = (ErrorCodes)OCL.EnqueueReadBuffer(
                    Handle,
                    buffer.Handle,
                    blocking,
                    offset,
                    (nuint) array.Count * (nuint) sizeof(T),
                    array.Buf,
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
            Array<T> array,
            out Event @event)
        where T : unmanaged
        {

            nint event_h;
            var err = (ErrorCodes)OCL.EnqueueWriteBuffer(
                Handle,
                buffer.Handle,
                blocking,
                offset,
                (nuint) array.Count * (nuint) sizeof(T),
                array.Buf,
                0,
                null,
                out event_h);

            if (err != ErrorCodes.Success)
            {
                throw new Exception(AppendErrCode("Couldn't enqueue buffer read, code: ", err));
            }
            @event = new Event(event_h);
        }

        public unsafe void EnqueueUnmapMemObject<T>(
            Buffer<T> buffer,
            void *ptr,
            out Event @event)
        where T : unmanaged
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

        public unsafe void EnqueueCopyBuffer<T>(
            Buffer<T> src,
            Buffer<T> dst,
            nuint src_offset,
            nuint dst_offset,
            nuint count,
            out Event @event,
            Event[]? wait_list = null)
        where T : unmanaged
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

    class Kernel
    {
        public nint Handle { get; }

        unsafe public Kernel(
            Program program,
            string name)
        {
            ErrorCodes err;
            Handle = OCL.CreateKernel(program.Handle, Encoding.ASCII.GetBytes(name), (int *)&err);

            if (err != ErrorCodes.Success)
            {
                throw new Exception(AppendErrCode("Failed to create kernel, code: ", err));
            }
        }

        ~Kernel()
        {
            OCL.ReleaseKernel(Handle);
        }
        
        unsafe private void GetKernelInfo<Y>(
            uint arg_index,
            KernelArgInfo param_name,
            nuint param_value_size,
            Y *param_value,
            nuint *param_value_size_ret)
        where Y: unmanaged
        {
            var err = (ErrorCodes)OCL.GetKernelArgInfo(
                Handle,
                arg_index,
                param_name,
                param_value_size,
                param_value,
                param_value_size_ret);
                
            if (err != ErrorCodes.Success)
            {
                throw new Exception(AppendErrCode(
                    $"Failed to get kernel argument info (index = {arg_index}), code: ",
                    err
                ));
            }
        }
        
        unsafe public string GetArgTypeName(
            uint arg_index
        )
        {
            nuint size_ret;
            GetKernelInfo<byte>(
                arg_index,
                KernelArgInfo.TypeName, 
                0, null,
                &size_ret);

            byte[] infoBytes = new byte[size_ret / (nuint)sizeof(byte)];
            
            fixed (byte *p_infoBytes = infoBytes)
            {
                GetKernelInfo(
                    arg_index,
                    KernelArgInfo.TypeName, 
                    size_ret, p_infoBytes,
                    null);
            }

            var len = Array.IndexOf(infoBytes, (byte)0);
            return Encoding.UTF8.GetString(infoBytes, 0, len);
        }
        
        unsafe public KernelArgAddressQualifier GetArgAddressQualifier(
            uint arg_index
        )
        {
            KernelArgAddressQualifier res;

            GetKernelInfo(
                arg_index,
                KernelArgInfo.AddressQualifier, 
                sizeof(KernelArgAddressQualifier), &res,
                null);

            return res;
        }

        unsafe public void SetArg<T>(
            uint arg_index,
            SparkOCL.Buffer<T> buffer)
        where T : unmanaged, INumber<T>
        {
            var binding = buffer.Handle;

            var err = (ErrorCodes)OCL.SetKernelArg(Handle, arg_index, (nuint)sizeof(nint), ref binding);
            if (err != ErrorCodes.Success)
            {
                throw new Exception(AppendErrCode("Failed to set kernel argument (index = {arg_index}), code: ", err));
            }
        }

        unsafe public void SetArg<T>(
            uint arg_index,
            T arg)
        where T: unmanaged
        {
            var err = (ErrorCodes)OCL.SetKernelArg(Handle, arg_index, (nuint)sizeof(T), ref arg);
            if (err != ErrorCodes.Success)
            {
                throw new Exception(AppendErrCode($"Failed to set kernel argument, arg size: {(nuint)sizeof(T)}, code: ", err));
            }
        }

        /// <summary>
        /// Set size of kernel local array.
        /// </summary>
        /// <typeparam name="T">Type of array</typeparam>
        /// <param name="arg_index"></param>
        /// <param name="Length">Array Length</param>
        /// <exception cref="Exception"></exception>
        unsafe public void SetSize<T>(
            uint arg_index,
            nuint Length)
        where T: unmanaged
        {
            var err = (ErrorCodes)OCL.SetKernelArg(Handle, arg_index, (nuint)sizeof(T) * Length, null);
            if (err != ErrorCodes.Success)
            {
                throw new Exception(AppendErrCode("Failed to set kernel argument, code: ", err));
            }
        }
    }

    class Buffer<T>
    where T : unmanaged
    {
        public nint Handle { get; }

        unsafe public Buffer(Context context, MemFlags flags, SparkOCL.Array<T> array)
        {
            ErrorCodes err;
            Handle = OCL.CreateBuffer(context.Handle, flags, (nuint) sizeof(T) * (nuint)array.Count, array.Buf, (int *)&err);
            if (err != ErrorCodes.Success)
            {
                throw new Exception(AppendErrCode("Failed to create buffer, code: ", err));
            }
        }

        ~Buffer()
        {
            OCL.ReleaseMemObject(Handle);
        }
    }

    unsafe class Array<T> : IDisposable, IEnumerable<T>
    where T: unmanaged
    {
        public T* Buf { get; internal set; }
        public int Count { get; }
        public nuint ElementSize { get; }

        public Array (ReadOnlySpan<T> array)
        {
            ElementSize = (nuint)sizeof(T);
            Buf = (T*)NativeMemory.AlignedAlloc((nuint)array.Length * ElementSize, 4096);
            Count = array.Length;
            array.CopyTo(new Span<T>(Buf, array.Length));
        }

        public Array (int size)
        {
            ElementSize = (nuint)sizeof(T);
            Buf = (T*)NativeMemory.AlignedAlloc((nuint)size * ElementSize, 4096);
            Count = size;
        }

        public T this[int i]
        {
            get
            {
                return Buf[i];
            }
            set
            {
                Buf[i] = value;
            }
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
        ~Array()
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
    }

    class Program
    {
        internal nint Handle { get; }

        unsafe static public Program FromFilename(
            Context context,
            Device device,
            string fileName)
        {
            using var sr = new StreamReader(fileName);
            string clStr = sr.ReadToEnd();

            ErrorCodes err;
            var program = OCL.CreateProgramWithSource(context.Handle, 1, [clStr], null, (int *)&err);
            if (program == IntPtr.Zero || err != ErrorCodes.Success)
            {
                throw new Exception(AppendErrCode("Failed to create CL program from source, code: ", err));
            }

            var errNum = (ErrorCodes)OCL.BuildProgram(program, 0, null, (byte*)null, null, null);

            if (errNum != ErrorCodes.Success)
            {
                _ = OCL.GetProgramBuildInfo(program, device.Handle, ProgramBuildInfo.BuildLog, 0, null, out nuint buildLogSize);
                byte[] log = new byte[buildLogSize / (nuint)sizeof(byte)];
                fixed (void* pValue = log)
                {
                    OCL.GetProgramBuildInfo(program, device.Handle, ProgramBuildInfo.BuildLog, buildLogSize, pValue, null);
                }
                string? build_log = Encoding.UTF8.GetString(log);

                //Console.WriteLine("Error in kernel: ");
                Console.WriteLine("=============== OpenCL Program Build Info ================");
                Console.WriteLine(build_log);
                Console.WriteLine("==========================================================");

                OCL.ReleaseProgram(program);
                throw new Exception(AppendErrCode("OpenCL build failed.", err));
            }

            return new Program(program);
        }

        Program(nint h)
        {
            Handle = h;
        }

        ~Program()
        {
            OCL.ReleaseProgram(Handle);
        }
    }
}
