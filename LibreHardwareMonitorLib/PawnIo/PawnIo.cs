﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace LibreHardwareMonitor.PawnIo;

internal class PawnIO
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


    private PawnIO()
    {
        TryLoadDll();
        pawnio_open(out _handle);
    }

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
             Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\PawnIO", "Install_Dir", null) ??
             @"C:\Program Files\PawnIO") is string
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
        pawnio_version(out uint version);
        return version;
    }

    public static void Open()
    {
        TryLoadDll();
    }

    public static void Close()
    {
        foreach (PawnIO module in _loadedModules.Values)
        {
            nint handle = Interlocked.Exchange(ref module._handle, IntPtr.Zero);
            if (handle != IntPtr.Zero)
                pawnio_close(handle);
        }

        _loadedModules.Clear();
    }


    public static PawnIO LoadModule(string name, byte[] bytes)
    {
        if (_loadedModules.TryGetValue(name, out PawnIO pawnIO))
        {
            return pawnIO;
        }

        pawnIO = new PawnIO();
        unsafe
        {
            fixed (byte* bytesPtr = bytes)
            {
                pawnio_load(pawnIO._handle, bytesPtr, (IntPtr)bytes.Length);
            }
        }

        _loadedModules.Add(name, pawnIO);

        return pawnIO;
    }

    public static PawnIO LoadModuleFromResource(Assembly assembly, string resourceName)
    {
        if (_loadedModules.TryGetValue(resourceName, out PawnIO pawnIO))
        {
            return pawnIO;
        }

        using Stream s = assembly.GetManifestResourceStream(resourceName);
        if (s is not UnmanagedMemoryStream ums) throw new InvalidOperationException();

        pawnIO = new PawnIO();
        unsafe
        {
            pawnio_load(pawnIO._handle, ums.PositionPointer, (IntPtr)ums.Length);
        }

        _loadedModules.Add(resourceName, pawnIO);

        return pawnIO;
    }

    public long[] Execute(string name, long[] input, int outLength)
    {
        long[] outArray = new long[outLength];
        pawnio_execute(_handle, name, input, (IntPtr)input.Length, outArray, (IntPtr)outArray.Length,
            out nint returnLength);
        Array.Resize(ref outArray, (int)returnLength);
        return outArray;
    }

    private IntPtr _handle;
    private static readonly Dictionary<string, PawnIO> _loadedModules = [];
}
