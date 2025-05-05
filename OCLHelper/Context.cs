using Silk.NET.OpenCL;

using static OCLHelper.CLHandle;

namespace OCLHelper;

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

    [Obsolete("Bad implementation")]
    unsafe static public Context FromType(
        DeviceType type)
    {
        var platforms = Platform.GetDiscovered();

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
