namespace Hubcon.Server.Core.Configuration;

using System.Runtime.InteropServices;

public static class FileDescriptorLimit
{
    private const int RLIMIT_NOFILE_LINUX = 7;
    private const int RLIMIT_NOFILE_MACOS = 8;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rlimit
    {
        public ulong rlim_cur; // soft
        public ulong rlim_max; // hard
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int getrlimit(int resource, ref Rlimit rlim);

    [DllImport("libc", SetLastError = true)]
    private static extern int setrlimit(int resource, ref Rlimit rlim);

    public static void RaiseToHardLimit()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        int resource = OperatingSystem.IsLinux()
            ? RLIMIT_NOFILE_LINUX
            : RLIMIT_NOFILE_MACOS;

        var rlim = new Rlimit();
        if (getrlimit(resource, ref rlim) != 0)
            return;

        if (rlim.rlim_cur >= rlim.rlim_max)
            return;

        rlim.rlim_cur = rlim.rlim_max;
        setrlimit(resource, ref rlim);
    }
    
    public static void SetSoftLimit(long desired)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        if (desired <= 0)
            throw new ArgumentOutOfRangeException(nameof(desired), "The desired soft socket limit must be greater than 0.");

        int resource = OperatingSystem.IsLinux()
            ? RLIMIT_NOFILE_LINUX
            : RLIMIT_NOFILE_MACOS;

        var rlim = new Rlimit();
        if (getrlimit(resource, ref rlim) != 0)
            return;

        ulong target = (ulong)desired;

        if (target > rlim.rlim_max)
            target = rlim.rlim_max;

        if (rlim.rlim_cur == target)
            return;

        rlim.rlim_cur = target;
        setrlimit(resource, ref rlim);
    }
}