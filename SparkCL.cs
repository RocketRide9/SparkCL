#define DEBUG_TIME

using Silk.NET.OpenCL;
using Quasar.Native;

using System;
using System.Numerics;
using System.IO;
using System.Globalization;
using System.Collections.Generic;

// идея сократить область применения до вычисления на одном устройстве.
// это должно упростить использование OpenCL, абстрагируя понятия контекста,
// очереди команд и устройства.
namespace SparkCL
{
    using System.Collections;
    using SparkOCL;
    static class StarterKit
    {
        // создать объекты на первом попавшемся GPU
        static public void GetStarterKit(
            out SparkOCL.Context context,
            out SparkOCL.Device device,
            out SparkOCL.CommandQueue commandQueue)
        {
            context = Context.FromType(DeviceType.Gpu);

            Platform.Get(out var platforms);
            var platform = platforms[0];

            platform.GetDevices(DeviceType.Gpu, out var devices);
            device = devices[0];

            commandQueue = new CommandQueue(context, device);
        }
    }

    static public class Core
    {
        static internal Context? context;
        static internal CommandQueue? queue;
        static internal SparkOCL.Device? device;

        static public List<Event> IOEvents = new(32);
        static public List<Event> KernEvents = new(32);
        
        static public void Init()
        {
            context = Context.FromType(DeviceType.Gpu);

            Platform.Get(out var platforms);
            var platform = platforms[0];

            platform.GetDevices(DeviceType.Gpu, out var devices);
            device = devices[0];

            queue = new CommandQueue(context, device);
        }
        
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

            return (IO, Kern);
        }

        static public void WaitQueue()
        {
            queue!.Finish();
        }
    }

    public static class EventExt
    {
        public static ulong GetElapsed(this SparkOCL.Event @event)
        {
            var s = @event.GetProfilingInfo(ProfilingInfo.Start);
            var c = @event.GetProfilingInfo(ProfilingInfo.End);

            return c - s;
        }
    }
    
    static class KernelArgExt
    {
        public static void ArgValue(this float val, SparkCL.Kernel kernel, uint index)
        {
            kernel.SetArg(index, val);
        }
        public static void ArgValue(this int val, SparkCL.Kernel kernel, uint index)
        {
            kernel.SetArg(index, val);
        }
        public static void ArgValue(this double val, SparkCL.Kernel kernel, uint index)
        {
            kernel.SetArg(index, val);
        }
        
        public static void ArgLocalSize(this nuint val, SparkCL.Kernel kernel, uint index)
        {
            kernel.SetSize(index, val);
        }
    }

    public class Program
    {
        SparkOCL.Program program;

        public Program(string fileName)
        {
            program = SparkOCL.Program.FromFilename(Core.context!, Core.device!, fileName);
        }

        public SparkCL.Kernel GetKernel(string kernelName, NDRange globalWork, NDRange localWork)
        {
            var oclKernel = new SparkOCL.Kernel(program, kernelName);
            return new Kernel(oclKernel, globalWork, localWork);
        }
    }

    public class ArgInfo
    {
        public bool IsPointer;
        public KernelArgAddressQualifier Qualifier;
        public Type DataType;

        public ArgInfo(string typeName, KernelArgAddressQualifier qualifier)
        {
            Qualifier = qualifier;
            int base_end = typeName.LastIndexOf('*');
            if (base_end == typeName.Length - 1)
            {
                IsPointer = true;
                typeName = typeName.Substring(0, base_end);
            } else {
                IsPointer = false;
            }

            DataType = typeName switch
            {
                "float" => typeof(float),
                "double" => typeof(double),
                "int" => typeof(int),
                _ => throw new NotImplementedException(),
            };
        }
    }
    
    public class Kernel
    {
        SparkOCL.Kernel Inner;
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
            Core.queue!.EnqueueNDRangeKernel(Inner, new NDRange(), GlobalWork, LocalWork, out var ev, waitList);
            #if DEBUG_TIME
                Core.KernEvents.Add(ev);
            #endif
            if (blocking)
            {
                ev.Wait();
            }
            return ev;
        }
        
        public uint PushArg<T>(
            SparkCL.Memory<T> mem)
        where T: unmanaged, INumber<T>
        {
            Inner.SetArg(lastPushed, mem.buffer);
            lastPushed++;
            return lastPushed;
        }

        public uint PushArg<T>(
            T arg)
        where T: unmanaged
        {
            Inner.SetArg(lastPushed, arg);
            lastPushed++;
            return lastPushed;
        }

        public void SetArg<T>(
            uint idx,
            T arg)
        where T: unmanaged
        {
            Inner.SetArg(idx, arg);
        }

        public void SetArg<T>(
            uint idx,
            SparkCL.Memory<T> mem)
        where T: unmanaged, INumber<T>
        {
            Inner.SetArg(idx, mem.buffer);
        }

        public void SetSize(
            uint idx,
            nuint sz)
        {
            Inner.SetSize(idx, sz);
        }
        
        public ArgInfo GetArgInfo(uint arg_index)
        {
            var name = Inner.GetArgTypeName(arg_index);
            var qual = Inner.GetArgAddressQualifier(arg_index);
            return new ArgInfo(name, qual);
        }

        internal Kernel(SparkOCL.Kernel kernel, NDRange globalWork, NDRange localWork)
        {
            Inner = kernel;
            GlobalWork = globalWork;
            LocalWork = localWork;
        }
    }

    public unsafe class Memory<T> : IDisposable, IEnumerable<T>
    where T: unmanaged, INumber<T> 
    {
        internal Buffer<T> buffer;
//        internal void* mappedPtr;
        internal Array<T> array;
        public int Count { get => array.Count; }

        public Memory(ReadOnlySpan<T> in_array, MemFlags flags = MemFlags.ReadWrite)
        {
            this.array = new(in_array);
            buffer = new(Core.context!, flags | MemFlags.UseHostPtr, this.array);
        }

        public Memory(StreamReader file, MemFlags flags = MemFlags.ReadWrite)
        {
            var sizeStr = file.ReadLine();
            var size = int.Parse(sizeStr!);
            this.array = new Array<T>(size);

            for (int i = 0; i < (int)size; i++)
            {
                var row = file.ReadLine();
                T elem;
                try {
                    elem = T.Parse(row!, CultureInfo.InvariantCulture);
                } catch (SystemException) {
                    throw new System.Exception($"i = {i}");
                }
                array[i] = elem;
            }
            buffer = new(Core.context!, flags | MemFlags.UseHostPtr, this.array);
        }

        public Memory (int size, MemFlags flags = MemFlags.ReadWrite)
        {
            array = new(size);
            buffer = new(Core.context!, flags | MemFlags.UseHostPtr, this.array);
        }

        public T this[int i]
        {
            get => array[i];
            set => array[i] = value;
        }

        //public Event Map(
        //    MapFlags flags,
        //    bool blocking = true
        //)
        //{
        //    mappedPtr = Core.queue!.EnqueueMapBuffer(buffer, blocking, flags, 0, array.Count, out var ev);
        //    return ev;
        //}

        //unsafe public Event Unmap()
        //{
        //    Core.queue!.EnqueueUnmapMemObject(buffer, mappedPtr, out var ev);
        //    return ev;
        //}

        public Event Read(
            bool blocking = true,
            Event[]? wait_list = null
        )
        {
            Core.queue!.EnqueueReadBuffer(buffer, blocking, 0, array, out var ev);
            #if DEBUG_TIME
                Core.IOEvents.Add(ev);
            #endif
            return ev;
        }

        public Event Write(
            bool blocking = true
        )
        {
            Core.queue!.EnqueueWriteBuffer(buffer, blocking, 0, array, out var ev);
            #if DEBUG_TIME
                Core.IOEvents.Add(ev);
            #endif
            return ev;
        }

        public Event CopyTo(
            Memory<T> destination,
            bool blocking = true,
            Event[]? waitList = null
        )
        {
            if (Count != destination.Count)
            {
                throw new Exception("Source and destination sizes doesn't match");
            }
            Core.queue!.EnqueueCopyBuffer(buffer, destination.buffer, 0, 0, (nuint) Count, out var ev, waitList);
            #if DEBUG_TIME
                Core.IOEvents.Add(ev);
            #endif
            if (blocking)
            {
                ev.Wait();
            }
            return ev;
        }

        public float DotHost(Memory<float> rhs)
        {
            float res = (float)BLAS.dot(
                (int) this.Count,
                new ReadOnlySpan<float>(    array.Buf, (int)Count),
                new ReadOnlySpan<float>(rhs.array.Buf, (int)Count)
            );
            return res;
        }

        public double DotHost(Memory<double> rhs)
        {
            double res = (double)BLAS.dot(
                (int)this.Count,
                new ReadOnlySpan<double>(array.Buf, (int)Count),
                new ReadOnlySpan<double>(rhs.array.Buf, (int)Count)
            );
            return res;
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
                array.Dispose();
                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        ~Memory()
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
            return array.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
