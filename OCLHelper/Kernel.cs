using System.Numerics;
using System.Text;
using Silk.NET.OpenCL;

using static OCLHelper.CLHandle;

namespace OCLHelper;

public class Kernel
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

    unsafe private void GetArgInfo<Y>(
        uint arg_index,
        KernelArgInfo param_name,
        nuint param_value_size,
        Y *param_value,
        out nuint param_value_size_ret)
    where Y: unmanaged
    {
        var err = (ErrorCodes)OCL.GetKernelArgInfo(
            Handle,
            arg_index,
            param_name,
            param_value_size,
            param_value,
            out param_value_size_ret);

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
    ) {
        GetArgInfo<byte>(
            arg_index,
            KernelArgInfo.TypeName,
            0, null,
            out nuint size_ret);

        byte[] infoBytes = new byte[size_ret / (nuint)sizeof(byte)];

        fixed (byte *p_infoBytes = infoBytes)
        {
            GetArgInfo(
                arg_index,
                KernelArgInfo.TypeName,
                size_ret, p_infoBytes,
                out var _
            );
        }

        var len = Array.IndexOf(infoBytes, (byte)0);
        return Encoding.UTF8.GetString(infoBytes, 0, len);
    }

    unsafe public KernelArgAddressQualifier GetArgAddressQualifier(
        uint arg_index
    )
    {
        KernelArgAddressQualifier res;

        GetArgInfo(
            arg_index,
            KernelArgInfo.AddressQualifier,
            sizeof(KernelArgAddressQualifier), &res,
            out _
        );

        return res;
    }

    unsafe public void SetArg<T>(
        uint arg_index,
        IMemObject<T> memory)
    where T : unmanaged, INumber<T>
    {
        var binding = memory.Handle;

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
