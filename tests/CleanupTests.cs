using System;
using System.IO;
using System.Linq;
using SpiderManInstaller;
class CleanupTests
{
    static string root, runtime;
    static string[] rows={"asset_archive/world", "bridge.dll", "SpiderManStartupModule.dll", "SpiderManNativeStartup.exe", "SAVE/profile.dat", "seed.save"};
    static void Put(string relative,string data="test") {string path=Path.Combine(runtime,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,data);}
    static bool Has(string relative)=>File.Exists(Path.Combine(runtime,relative));
    static void Assert(bool yes,string text){if(!yes)throw new Exception(text);}
    static void Reject(Action action){bool rejected=false;try{action();}catch(IOException){rejected=true;}catch(InvalidDataException){rejected=true;}Assert(rejected,"Unsafe operation accepted");}
    static GameContentCleanup Plan()=>new GameContentCleanup(runtime,rows);
    static void Run(string name,Action test)
    {
        root=Path.Combine(Path.GetTempPath(),"SpiderCleanup-"+Guid.NewGuid().ToString("N"));runtime=Path.Combine(root,"runtime");
        Put("runtime-bootstrap.json");Put("preparation-source.txt");Put("prepared-receipt.txt");
        foreach(string row in rows)Put("StartupContent/"+row);
        Put("original-asset-toc");Put("PreparedOverlays/asset_archive/overlay");Put("LocalState/save.dat");
        Directory.CreateDirectory(Path.Combine(root,"other-game"));File.WriteAllText(Path.Combine(root,"other-game","keep"),"keep");
        try{test();Console.WriteLine("PASS "+name);}
        finally
        {
            var full=Path.GetFullPath(root);var temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\')+"\\";
            if(!full.StartsWith(temp,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(full).StartsWith("SpiderCleanup-"))throw new Exception("Invalid fixture cleanup root");
            foreach(var f in Directory.GetFiles(full,"*",SearchOption.AllDirectories))File.SetAttributes(f,FileAttributes.Normal);
            Directory.Delete(full,true);
        }
    }
    static void Main(string[] args)
    {
        if(args.Length==1)
        {
            bool rejected=false;
            try{new GameContentCleanup(args[0],new[]{"asset_archive/world"});}
            catch(IOException){rejected=true;}
            Assert(rejected,"Linked directory was accepted");Console.WriteLine("PASS linked directory refused before cleanup");return;
        }
        Run("Prepared data removed; host, saves, unrelated files preserved",()=>{
            var p=Plan();Assert(p.Count==5,"Unexpected deletion count");Assert(p.Bytes==20,"Wrong byte count");p.Delete(null);
            Assert(!Has("StartupContent/asset_archive/world")&&!Has("StartupContent/bridge.dll"),"Game data left");
            Assert(Has("StartupContent/SpiderManNativeStartup.exe")&&Has("StartupContent/SAVE/profile.dat")&&Has("StartupContent/seed.save")&&Has("LocalState/save.dat"),"Protected data removed");
            Assert(File.Exists(Path.Combine(root,"other-game","keep")),"Other game removed");
            Assert(!Has("prepared-receipt.txt")&&!Has("preparation-source.txt")&&!Has("deletion-pending.txt"),"Metadata left");
            Assert(!Directory.Exists(Path.Combine(runtime,"StartupContent/asset_archive")),"Archive folder blocks reinstall");
        });
        Run("No confirmation means no mutations",()=>{Plan();Assert(Has("prepared-receipt.txt")&&Has("StartupContent/asset_archive/world"),"Planning changed files");});
        Run("Traversal refused",()=>{Reject(()=>new GameContentCleanup(runtime,new[]{"../../other-game/keep"}));Assert(Has("prepared-receipt.txt"),"Receipt lost");});
        Run("Absolute paths refused",()=>Reject(()=>new GameContentCleanup(runtime,new[]{Path.Combine(root,"other-game/keep")})));
        Run("Unknown ownership refused",()=>{File.Delete(Path.Combine(runtime,"preparation-source.txt"));Reject(()=>Plan());Assert(Has("StartupContent/bridge.dll"),"Data removed");});
        Run("Missing bootstrap refused",()=>{File.Delete(Path.Combine(runtime,"runtime-bootstrap.json"));Reject(()=>Plan());});
        Run("In-use file refuses before any deletion",()=>{var p=Plan();using(File.Open(Path.Combine(runtime,"StartupContent/bridge.dll"),FileMode.Open,FileAccess.Read,FileShare.None))Reject(()=>p.Delete(null));Assert(Has("prepared-receipt.txt")&&Has("StartupContent/asset_archive/world"),"Partially deleted locked game");});
        Run("Read-only file refuses before any deletion",()=>{var p=Plan();File.SetAttributes(Path.Combine(runtime,"StartupContent/bridge.dll"),FileAttributes.ReadOnly);Reject(()=>p.Delete(null));Assert(Has("prepared-receipt.txt"),"Receipt lost");});
        Run("Interrupted deletion stays unready and can resume",()=>{var p=Plan();Reject(()=>p.Delete((d,t)=>{if(d==1)throw new IOException("interrupted");}));Assert(!Has("prepared-receipt.txt")&&Has("preparation-source.txt")&&Has("deletion-pending.txt"),"Bad retry state");Plan().Delete(null);Assert(!Has("preparation-source.txt")&&!Has("deletion-pending.txt"),"Retry failed");});
        Run("Unknown files outside asset area preserved",()=>{Put("StartupContent/unrelated.txt");Plan().Delete(null);Assert(Has("StartupContent/unrelated.txt"),"Unknown file removed");});
        Run("New files during removal require retry, not false completion",()=>{var p=Plan();Reject(()=>p.Delete((done,total)=>{if(done==total)Put("StartupContent/asset_archive/new-file");}));Assert(Has("deletion-pending.txt")&&Has("preparation-source.txt"),"Lost retry state");Plan().Delete(null);Assert(!Directory.Exists(Path.Combine(runtime,"StartupContent/asset_archive")),"Retry left data");});
        Console.WriteLine("11 cleanup checks passed.");
    }
}
