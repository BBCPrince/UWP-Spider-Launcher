using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Windows.Storage;

namespace SpiderManInstaller
{
    // Read-only access through UWP's normal broker, or its documented FromApp
    // API. Neither changes permissions nor requests broadFileSystemAccess.
    sealed class UsbFolderSource : IFolderImportSource
    {
        readonly StorageFolder folder;
        public UsbFolderSource(StorageFolder folder) { this.folder = folder; }

        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile2FromAppW(string name, uint access, uint share, uint disposition, IntPtr parameters);

        internal static Stream OpenNativeRead(string path)
        {
            var handle = CreateFile2FromAppW(path, 0x80000000, 1, 3, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error(); handle.Dispose();
                if (error == 2) throw new FileNotFoundException("USB file not found", path);
                if (error == 3) throw new DirectoryNotFoundException("USB directory not found: " + path);
                throw new IOException("File access error " + error);
            }
            try { return new FileStream(handle, FileAccess.Read); }
            catch { handle.Dispose(); throw; }
        }

        public async Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            string relative = ImportEngine.SafePath(relativePath).Replace('/', '\\');
            try
            {
                var file = await folder.GetFileAsync(relative);
                cancel.ThrowIfCancellationRequested();
                return await file.OpenStreamForReadAsync();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception brokerError)
            {
                if (String.IsNullOrEmpty(folder.Path)) throw new IOException("USB cannot expose " + relativePath + ". Use ZIP or Wi-Fi import.", brokerError);
                string root = Path.GetFullPath(folder.Path).TrimEnd('\\') + "\\";
                string path = Path.GetFullPath(Path.Combine(root, relative));
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Path escaped selected USB folder.");
                try
                {
                    // OPEN_EXISTING + GENERIC_READ + FILE_SHARE_READ: never writes USB.
                    cancel.ThrowIfCancellationRequested();
                    return OpenNativeRead(path);
                }
                catch (FileNotFoundException) { throw; }
                catch (DirectoryNotFoundException) { throw; }
                catch (OperationCanceledException) { throw; }
                catch (Exception error)
                {
                    throw new IOException("Cannot read USB file " + relativePath + ". Check that the complete Steam folder is on a readable USB drive. If Xbox restricts this file type, use ZIP or Wi-Fi import. " + error.Message, error);
                }
            }
        }
    }
}
