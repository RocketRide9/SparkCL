using System.Text;
using Silk.NET.OpenCL;

using static OCLHelper.CLHandle;

namespace OCLHelper;

class Device
{
    DeviceType Type { get; }
    public nint Handle { get; }

    [Obsolete]
    unsafe public bool IsHostUnifiedMemory {
        get {
            GetInfo<bool>(
                DeviceInfo.HostUnifiedMemory,
                0, null,
                out var size_ret
            );

            bool res;
            GetInfo(
                DeviceInfo.HostUnifiedMemory,
                size_ret, &res,
                out _
            );

            return res;
        }
    }

    unsafe public string GetName()
    {
        return GetStringInfo(DeviceInfo.Name);
    }

    unsafe private string GetStringInfo(DeviceInfo deviceInfo)
    {
        GetInfo<byte>(
            deviceInfo,
            0, null,
            out var size_ret
        );

        byte[] bytes = new byte[size_ret / sizeof(byte)];

        fixed (byte *p_infoBytes = bytes)
        {
            GetInfo(
                deviceInfo,
                size_ret, p_infoBytes,
                out _
            );
        }

        var len = Array.IndexOf(bytes, (byte)0);
        return Encoding.UTF8.GetString(bytes, 0, len);
    }

    unsafe private void GetInfo<Y>(
        DeviceInfo param_name,
        nuint info_size,
        Y *info_value,
        out nuint info_size_ret)
    where Y: unmanaged
    {
        var err = (ErrorCodes)OCL.GetDeviceInfo(
            Handle,
            param_name,
            info_size,
            info_value,
            out info_size_ret
        );

        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode(
                $"Failed to get device info ({param_name}), code: ",
                err
            ));
        }
    }

    internal Device(nint h)
    {
        Handle = h;
    }

    ~Device()
    {
        OCL.ReleaseDevice(Handle);
    }
}
