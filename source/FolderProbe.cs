using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
namespace SpiderManInstaller
{
    static class FolderProbe
    {
        internal static async Task RunAsync()
        {
            var log=new StringBuilder();
            string parent=Path.Combine(ApplicationData.Current.TemporaryFolder.Path,"FolderProbe-"+Guid.NewGuid().ToString("N"));
            try
            {
                await FolderChecks.RunAsync(parent, async path=>new UsbFolderSource(await StorageFolder.GetFolderFromPathAsync(path)),line=>log.AppendLine(line));
                string archive=Path.Combine(parent,"source","asset_archive","g00s000");
                using(var native=UsbFolderSource.OpenNativeRead(archive))
                using(var reference=File.OpenRead(archive))
                    if(ImportEngine.Digest(native)!=ImportEngine.Digest(reference))throw new Exception("FromApp read mismatch");
                log.AppendLine("PASS CreateFile2FromAppW read of extensionless fixture");
                log.AppendLine("PASS native UWP folder importer (app-private fixture; USB hardware remains untested)");
            }
            catch(Exception ex){log.AppendLine("FAIL "+ex);}
            finally
            {
                File.WriteAllText(Path.Combine(ApplicationData.Current.LocalFolder.Path,"folder-probe.txt"),log.ToString());
                string root=Path.GetFullPath(ApplicationData.Current.TemporaryFolder.Path).TrimEnd('\\')+"\\";
                if(Path.GetFullPath(parent).StartsWith(root,StringComparison.OrdinalIgnoreCase) && Directory.Exists(parent))Directory.Delete(parent,true);
            }
        }
    }
}
