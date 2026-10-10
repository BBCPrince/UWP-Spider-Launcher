using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
namespace SpiderManInstaller
{
    // Tiny, opt-in access check using ordinary UWP APIs. No permission changes.
    static class StorageProbe
    {
        internal static async Task RunIfRequestedAsync()
        {
            var local = ApplicationData.Current.LocalFolder;
            var request = await local.TryGetItemAsync("storage-probe.request");
            if (request == null) return;
            await request.DeleteAsync();
            var log = new StringBuilder(DateTime.UtcNow.ToString("o") + "\r\n");
            foreach (string path in new[] { local.Path, @"D:\DevelopmentFiles", @"D:\DevelopmentFiles\SpiderManInstallerStorageCheck-2c50a20aa9c8" })
            {
                log.AppendLine("PATH " + path);
                try
                {
                    var folder = await StorageFolder.GetFolderFromPathAsync(path);
                    log.AppendLine("StorageFolder open OK");
                    try { log.AppendLine("Free bytes " + await StorageSpace.FreeBytes(folder)); }
                    catch(Exception ex) { log.AppendLine("Free space unavailable: " + ex.Message); }
                    // Parent is opened read-only. Only write in our private fixture.
                    if (path == @"D:\DevelopmentFiles") continue;
                    string name = "SpiderManInstallerProbe-" + Guid.NewGuid().ToString("N");
                    var probe = await folder.CreateFolderAsync(name, CreationCollisionOption.FailIfExists);
                    try
                    {
                        string file = Path.Combine(probe.Path,"probe.bin");
                        using (var output = new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                            output.Write(new byte[] {1,2,3,4},0,4);
                        var bytes = File.ReadAllBytes(file);
                        if(bytes.Length != 4 || bytes[3] != 4) throw new IOException("Readback mismatch");
                        File.Delete(file);
                        log.AppendLine("PASS broker create + FileStream write/read/delete");
                    }
                    finally { await probe.DeleteAsync(StorageDeleteOption.PermanentDelete); }
                }
                catch(Exception ex) { log.AppendLine("FAIL " + ex.GetType().Name + " 0x" + ex.HResult.ToString("X8") + " " + ex.Message); }
            }
            // Check the normal .NET UWP file APIs independently of the broker.
            // The fixture was created through the authenticated development share;
            // its permissions are inherited unchanged. No platform settings change.
            const string fixture = @"D:\DevelopmentFiles\SpiderManInstallerStorageCheck-2c50a20aa9c8";
            try
            {
                string seed = File.ReadAllText(Path.Combine(fixture,"read-check.txt"));
                if(seed.Replace("\r\n","\n") != "SpiderManInstaller storage check\n") throw new IOException("Seed mismatch");
                log.AppendLine("PASS FileStream existing fixture read");
            }
            catch(Exception ex) { log.AppendLine("FAIL FileStream read 0x"+ex.HResult.ToString("X8")+" "+ex.Message); }
            string created = Path.Combine(fixture,"write-check-"+Guid.NewGuid().ToString("N")+".bin");
            bool owned = false;
            try
            {
                using(var stream = new FileStream(created,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                { owned=true; stream.WriteByte(42); }
                var data = File.ReadAllBytes(created);
                if(data.Length != 1 || data[0] != 42) throw new IOException("Write readback mismatch");
                log.AppendLine("PASS FileStream fixture create/write/read");
            }
            catch(Exception ex) { log.AppendLine("FAIL FileStream write 0x"+ex.HResult.ToString("X8")+" "+ex.Message); }
            finally
            {
                if(owned)
                {
                    try { File.Delete(created); log.AppendLine("PASS FileStream fixture delete"); }
                    catch(Exception ex) { log.AppendLine("FAIL fixture cleanup "+ex.Message); }
                }
            }
            string fixtureRoot = null;
            try
            {
                string dataRoot = GameStorage.OpenRoot();
                log.AppendLine("DESTINATION " + dataRoot);
                long free = GameStorage.FreeBytes(dataRoot);
                log.AppendLine("DESTINATION_FREE_BYTES " + free);
                if(free < 71883934383L) throw new IOException("Not enough capacity for this game plus margin");
                fixtureRoot = Path.Combine(dataRoot,"StorageSelfTest-"+Guid.NewGuid().ToString("N"));
                await FolderChecks.RunAsync(fixtureRoot, path=>Task.FromResult<IFolderImportSource>(new NativeSource(path)),line=>log.AppendLine(line));
                log.AppendLine("PASS destination directory creation, free-space query, and full import fixture checks");
            }
            catch(Exception ex) { log.AppendLine("FAIL destination test "+ex); }
            finally
            {
                if(fixtureRoot != null && Directory.Exists(fixtureRoot))
                {
                    string root = Path.GetFullPath(GameStorage.OpenRoot()).TrimEnd('\\')+"\\";
                    if(!Path.GetFullPath(fixtureRoot).StartsWith(root,StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(fixtureRoot).StartsWith("StorageSelfTest-"))
                        throw new IOException("Unexpected fixture cleanup path");
                    Directory.Delete(fixtureRoot,true);
                }
            }
            var result = await local.CreateFileAsync("storage-probe.txt",CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(result, log.ToString());
        }
        sealed class NativeSource : IFolderImportSource
        {
            readonly string root;
            internal NativeSource(string root) { this.root=root; }
            public Task<Stream> OpenReadAsync(string relative,System.Threading.CancellationToken cancel)
            {
                cancel.ThrowIfCancellationRequested();
                return Task.FromResult<Stream>(File.OpenRead(Path.Combine(root,ImportEngine.SafePath(relative))));
            }
        }
    }
}
