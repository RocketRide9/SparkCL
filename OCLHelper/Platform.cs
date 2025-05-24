using System.Text;
using Silk.NET.OpenCL;

using static OCLHelper.CLHandle;

namespace OCLHelper;

public class Platform
{
    public nint Handle { get; }
    unsafe public static Platform[] GetDiscovered()
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

        return ids.Select(id => new Platform(id)).ToArray();
    }

    unsafe public Device[] GetDevicesOfType(DeviceType type)
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

        return ids.Select(id => new Device(id)).ToArray();
    }

    public string GetName()
    {
        return GetStringInfo(PlatformInfo.Name);
    }
    public string GetVersion()
    {
        return GetStringInfo(PlatformInfo.Version);
    }

    unsafe private string GetStringInfo(PlatformInfo platformInfo)
    {
        GetInfo<byte>(
            platformInfo,
            0, null,
            out var size_ret
        );

        byte[] nameBytes = new byte[size_ret / sizeof(byte)];

        fixed (byte *p_infoBytes = nameBytes)
        {
            GetInfo(
                platformInfo,
                size_ret, p_infoBytes,
                out _
            );
        }

        var len = Array.IndexOf(nameBytes, (byte)0);
        return Encoding.UTF8.GetString(nameBytes, 0, len);
    }

    unsafe private void GetInfo<Y>(
        PlatformInfo platform_info,
        nuint info_size,
        Y *info_value,
        out nuint info_size_ret)
    where Y: unmanaged
    {
        var err = (ErrorCodes)OCL.GetPlatformInfo(
            Handle,
            platform_info,
            info_size,
            info_value,
            out info_size_ret
        );

        if (err != ErrorCodes.Success)
        {
            throw new Exception(AppendErrCode(
                $"Failed to get platform info ({platform_info}), code: ",
                err
            ));
        }
    }

    private Platform(nint h)
    {
        Handle = h;
    }
}
