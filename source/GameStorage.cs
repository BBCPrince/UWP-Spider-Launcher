using System;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Storage;
using Windows.System.Profile;
namespace SpiderManInstaller
{
    static class GameStorage
    {
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetDiskFreeSpaceExW(string directory, out ulong available, out ulong total, out ulong free);

        internal static string OpenRoot()
        {
            string root = AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Xbox"
                ? @"D:\DevelopmentFiles\SpiderManInstaller-Imports"
                : Path.Combine(ApplicationData.Current.LocalFolder.Path,"GameImports");
            // Ordinary UWP FileStream/Directory access. If denied, fail rather than
            // silently writing a 66 GiB installation into private LocalState.
            Directory.CreateDirectory(root);
            return root;
        }
        internal static long FreeBytes(string directory)
        {
            ulong available,total,free;
            if(!GetDiskFreeSpaceExW(directory,out available,out total,out free))
                throw new IOException("Cannot measure destination free space (Windows error " + Marshal.GetLastWin32Error() + "): " + directory);
            return checked((long)available);
        }
    }
}
