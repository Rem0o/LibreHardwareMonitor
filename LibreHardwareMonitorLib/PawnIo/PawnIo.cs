using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace LibreHardwareMonitor.PawnIo;

internal class PawnIo : IDisposable
{
    [DllImport("kernel32", SetLastError = true)]
    static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("PawnIOLib", ExactSpelling = true, PreserveSig = false)]
    private static extern void pawnio_version(out uint version);

    [DllImport("PawnIOLib", ExactSpelling = true, PreserveSig = false)]
    private static extern void pawnio_open(out IntPtr handle);

    [DllImport("PawnIOLib", ExactSpelling = true, PreserveSig = false)]
    private static extern unsafe void pawnio_load(IntPtr handle, byte* blob, IntPtr size);

    [DllImport("PawnIOLib", ExactSpelling = true, PreserveSig = false)]
    private static extern void pawnio_execute(IntPtr handle, [MarshalAs(UnmanagedType.LPStr)] string name,
        long[] inArray, IntPtr inSize, long[] outArray, IntPtr outSize, out IntPtr returnSize);

    [DllImport("PawnIOLib", ExactSpelling = true, PreserveSig = false)]
    private static extern void pawnio_close(IntPtr handle);

    private IntPtr _handle;

    private static void TryLoadDll()
    {
        try
        {
            pawnio_version(out uint _);
            return;
        }
        catch
        {
            // ignored
        }

        // Try getting path from registry
        if ((Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\PawnIO", "Install_Dir", null) ??
             Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\PawnIO", "Install_Dir", null)) is string
            {
                Length: > 0
            } pawnIoPath)
        {
            try
            {
                LoadLibrary(pawnIoPath + Path.DirectorySeparatorChar + "PawnIOLib");
            }
            catch
            {
                // ignored
            }
        }

        // This will throw if we still didn't manage to load it
        pawnio_version(out uint _);
    }

    public static uint Version()
    {
        TryLoadDll();
        pawnio_version(out var version);
        return version;
    }

    private PawnIo()
    {
        TryLoadDll();
        pawnio_open(out _handle);
    }

    ~PawnIo()
    {
        ReleaseUnmanagedResources();
    }

    private void ReleaseUnmanagedResources()
    {
        var handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle != IntPtr.Zero)
            pawnio_close(handle);
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    private void LoadModule(byte[] bytes)
    {
        unsafe
        {
            fixed (byte* bytesPtr = bytes)
            {
                pawnio_load(_handle, bytesPtr, (IntPtr)bytes.Length);
            }
        }
    }

    private void LoadModuleFromResource(Assembly assembly, string resourceName)
    {
        using var s = assembly.GetManifestResourceStream(resourceName);
        if (s is not UnmanagedMemoryStream ums) throw new InvalidOperationException();
        unsafe
        {
            pawnio_load(_handle, ums.PositionPointer, (IntPtr)ums.Length);
        }
    }

    public static PawnIo FromModule(byte[] bytes)
    {
        var pawnIo = new PawnIo();
        pawnIo.LoadModule(bytes);
        return pawnIo;
    }

    public static PawnIo FromModuleResource(Assembly assembly, string resourceName)
    {
        var pawnIo = new PawnIo();
        pawnIo.LoadModuleFromResource(assembly, resourceName);
        return pawnIo;
    }

    public long[] Execute(string name, long[] input, int outLength)
    {
        var outArray = new long[outLength];
        pawnio_execute(_handle, name, input, (IntPtr)input.Length, outArray, (IntPtr)outArray.Length,
            out var returnLength);
        Array.Resize(ref outArray, (int)returnLength);
        return outArray;
    }
}
