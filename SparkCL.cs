#define DEBUG_TIME

using Silk.NET.OpenCL;
using Quasar.Native;

using System.Numerics;
using System.Globalization;

// идея сократить область применения до вычисления на одном устройстве.
// это должно упростить использование OpenCL, абстрагируя понятия контекста,
// очереди команд и устройства.
namespace SparkCL
{
    using System.Collections;
    using System.Runtime.CompilerServices;
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
            
            var platform = Platform.GetDiscovered().First();

            device = platform.GetDevicesOfType(DeviceType.Gpu).First();

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
        static internal SparkOCL.Device? device;

        static public List<Event> IOEvents { get; private set; } = new(32);
        static public List<Event> KernEvents { get; private set; } = new(32);
        
        static public void Init()
        {
            context = Context.FromType(DeviceType.Gpu);

            var platform = Platform.GetDiscovered().First();

            device = platform.GetDevicesOfType(DeviceType.Gpu).First();

            QueueProperties[] properties = [
                (QueueProperties)CommandQueueInfo.Properties, (QueueProperties) CommandQueueProperties.ProfilingEnable,
                0
            ];
            queue = new CommandQueue(context, device, properties);
        }
        
        #if DEBUG_TIME
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
        #endif

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

        public bool IsEqualTo<T>(Memory<T> _)
        where T: unmanaged, INumber<T>
        {
            return IsPointer && DataType == typeof(T);
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
            SparkCL.Memory<T> mem)
        where T: unmanaged, INumber<T>
        {
            var info = GetArgInfo(idx);
            if (!info.IsEqualTo(mem))
            {
                throw new ArgumentException($"Expected \"{info.TypeName}\", got \"{typeof(T)}*\"");
            }
            
            Inner.SetArg(idx, mem._buffer);
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

        internal Kernel(SparkOCL.Kernel kernel, NDRange globalWork, NDRange localWork)
        {
            Inner = kernel;
            GlobalWork = globalWork;
            LocalWork = localWork;
        }
    }

    unsafe interface IReadOnlyMemAccessor<T>
    where T: unmanaged, INumber<T>
    {
        internal T* _ptr { get; }
        int Length{ get; }
        T this[int i] { get; }
    }

    interface IMemAccessor<T> : IReadOnlyMemAccessor<T>
    where T: unmanaged, INumber<T>
    {
        new T this[int i] { get; set; }
    }

    public unsafe class Accessor<T> : IMemAccessor<T>, IDisposable
    where T: unmanaged, INumber<T>
    {
        internal T* ptr { get; }
        unsafe T* IReadOnlyMemAccessor<T>._ptr => ptr;
        private bool disposedValue;
        Memory<T> _master;

        public int Length { get; private set; }


        public T this[int i]
        {
            // TODO: measure performace
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ptr[i];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => ptr[i] = value;
        }

        internal Accessor(Memory<T> buffer, T* ptr, int length)
        {
            _master = buffer;
            this.ptr = ptr;
            Length = length;
        }

        public Span<T> AsSpan()
        {
            return new Span<T>(ptr, Length);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: освободить управляемое состояние (управляемые объекты)
                    _master.UnmapAccessor(this);
                }

                // TODO: освободить неуправляемые ресурсы (неуправляемые объекты) и переопределить метод завершения
                // TODO: установить значение NULL для больших полей
                disposedValue = true;
            }
        }

        // // TODO: переопределить метод завершения, только если "Dispose(bool disposing)" содержит код для освобождения неуправляемых ресурсов
        // ~Accessor()
        // {
        //     // Не изменяйте этот код. Разместите код очистки в методе "Dispose(bool disposing)".
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Не изменяйте этот код. Разместите код очистки в методе "Dispose(bool disposing)".
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public unsafe class Memory<T>
    where T: unmanaged, INumber<T> 
    {
        internal Buffer<T> _buffer;
        // internal T[] _storage;
        // should be a pointer to _storage
        // internal T* _storage;
        // public Array<T> Array { get; }
        public int Length { get; private set; }

        public Memory(ReadOnlySpan<T> in_array, MemFlags flags = MemFlags.ReadWrite)
        {
            Length = in_array.Length;
            _buffer = new(Core.context!, flags | MemFlags.CopyHostPtr, in_array);
            _buffer = Buffer<T>.NewCopyHost(Core.context!, MemFlags.ReadWrite, in_array);
        }

        internal void UnmapAccessor(IReadOnlyMemAccessor<T> accessor)
        {
            Core.queue!.EnqueueUnmapMemObject(_buffer, accessor._ptr, out _);
        }

        public Accessor<T> GetAccessor()
        {
            var ptr = Map(MapFlags.Write);
            return new Accessor<T>(this, ptr, Length);
        }
/*
        public Memory (int size, MemFlags flags = MemFlags.ReadWrite)
        {

            _storage = new T[size];
            _buffer = new(Core.context!, flags | MemFlags.UseHostPtr, _storage);
        }

        Memory(Buffer<T> buffer, T[] storage) {
            _buffer = buffer;
            _storage = storage;
        }
*/
        /*
        public Span<T> AsSpan()
        {
            return new Span<T>(_storage, Length);
        }
        */

        T* Map(
           MapFlags flags,
           bool blocking = true
        )
        {
            var res = (T*)Core.queue!.EnqueueMapBuffer(_buffer, blocking, flags, 0, (nuint)Length, out var ev);
            #if DEBUG_TIME
                Core.IOEvents.Add(ev);
            #endif
            if (blocking)
            {
                ev.Wait();
            }
            return res;
        }

        //unsafe public Event Unmap()
        //{
        //    Core.queue!.EnqueueUnmapMemObject(buffer, mappedPtr, out var ev);
        //    return ev;
        //}

        /*
        public Event Read(
            bool blocking = true,
            Event[]? wait_list = null
        )
        {
            Core.queue!.EnqueueReadBuffer(_buffer, blocking, 0, AsSpan(), out var ev);
            #if DEBUG_TIME
                Core.IOEvents.Add(ev);
            #endif
            return ev;
        }

        public Event Write(
            bool blocking = true
        )
        {
            Core.queue!.EnqueueWriteBuffer(_buffer, blocking, 0, AsSpan(), out var ev);
            #if DEBUG_TIME
                Core.IOEvents.Add(ev);
            #endif
            return ev;
        }
        */

        public Event CopyTo(
            Memory<T> destination,
            bool blocking = true,
            Event[]? waitList = null
        )
        {
            if (Length != destination.Length)
            {
                throw new Exception("Source and destination sizes doesn't match");
            }
            Core.queue!.EnqueueCopyBuffer(_buffer, destination._buffer, 0, 0, (nuint) Length, out var ev, waitList);
            #if DEBUG_TIME
                Core.KernEvents.Add(ev);
            #endif
            if (blocking)
            {
                ev.Wait();
            }
            return ev;
        }
/*
        public float DotHost(Memory<float> rhs)
        {
            float res = (float)BLAS.dot(
                (int) this.Count,
                new ReadOnlySpan<float>(    Array.Buf, (int)Count),
                new ReadOnlySpan<float>(rhs.Array.Buf, (int)Count)
            );
            return res;
        }

        public double DotHost(Memory<double> rhs)
        {
            double res = (double)BLAS.dot(
                (int)this.Count,
                new ReadOnlySpan<double>(Array.Buf, (int)Count),
                new ReadOnlySpan<double>(rhs.Array.Buf, (int)Count)
            );
            return res;
        }
*/
    }
}
