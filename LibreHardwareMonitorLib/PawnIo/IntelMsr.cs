using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.PawnIo;

public class IntelMsr
{
    private readonly PawnIo _pawnIo;

    public IntelMsr()
    {
        _pawnIo = PawnIo.FromModuleResource(typeof(IntelMsr).Assembly, $"{nameof(LibreHardwareMonitor)}.Resources.PawnIo.IntelMSR.bin");
    }

    public bool ReadMsr(uint index, out ulong value)
    {
        var inArray = new long[1];
        inArray[0] = (long)index;
        value = 0;
        try
        {
            var outArray = _pawnIo.Execute("ioctl_read_msr", inArray, 1);
            value = (ulong)outArray[0];
        }
        catch
        {
            return false;
        }
        return true;
    }

    public bool ReadMsr(uint index, out uint eax, out uint edx)
    {
        var inArray = new long[1];
        inArray[0] = (long)index;
        eax = 0;
        edx = 0;
        try
        {
            var outArray = _pawnIo.Execute("ioctl_read_msr", inArray, 1);
            eax = (uint)outArray[0];
            edx = (uint)(outArray[0] >> 32);
        }
        catch
        {
            return false;
        }
        return true;
    }

    public bool ReadMsr(uint index, out uint eax, out uint edx, GroupAffinity affinity)
    {
        GroupAffinity previousAffinity = ThreadAffinity.Set(affinity);
        bool result = ReadMsr(index, out eax, out edx);
        ThreadAffinity.Set(previousAffinity);
        return result;
    }
}
