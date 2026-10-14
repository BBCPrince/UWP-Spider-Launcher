using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Storage;
using Windows.System;

namespace SpiderManInstaller
{
    // One installed package supplies the host; game-derived files stay in the
    // verified user's data directory and are generated from an owned import.
    internal static class CombinedRuntime
    {
        internal static bool Enabled => Package.Current.Id.Name=="Local.UwpSpiderCombinedPrototype";
        internal static string Bundle => Package.Current.InstalledLocation.Path;
        internal static string Family => Package.Current.Id.FamilyName;
        static string Data => Path.Combine(GamePreparation.RuntimeRoot,"StartupContent");
        static string Pipelines => Path.Combine(Data,"first-launch-recipes-v1");
        static readonly Uri PlayUri=new Uri("uwp-spider-combined-game:play");
        internal static void Log(string text) => File.AppendAllText(Path.Combine(ApplicationData.Current.LocalFolder.Path,"combined-launcher.log"),DateTime.UtcNow.ToString("o")+" "+text+"\n");
        static void Verify(string file,long size,string hash)
        {
            if(!File.Exists(file)||new FileInfo(file).Length!=size||AssetPreparation.Bytes.FileHash(file)!=hash)
                throw new IOException("Preparation verification failed: "+Path.GetFileName(file));
        }
        static void VerifyInventory(string file,string expected)
        {
            if(AssetPreparation.Bytes.FileHash(file)!=expected)throw new IOException("Included preparation metadata failed verification.");
        }
        internal static void VerifyPayload(Action<string> progress,CancellationToken cancel)
        {
            progress?.Invoke("Checking the included runtime components…");
            string inventory=Path.Combine(Bundle,"RuntimeInventory.json");
            VerifyInventory(inventory,"26e9b4ee1c4b547d958462108b7c2679887cb912b812594bcefa3bef77581dea");
            foreach(var value in JsonArray.Parse(File.ReadAllText(inventory))) {
                cancel.ThrowIfCancellationRequested();var row=value.GetObject();string name=row.GetNamedString("path");
                if(String.IsNullOrEmpty(name)||ImportEngine.SafePath(name)!=name||name.EndsWith("/"))throw new IOException("Invalid runtime inventory path.");
                Verify(Path.Combine(Bundle,"StartupContent",name),(long)row.GetNamedNumber("size"),row.GetNamedString("sha256"));
            }
        }
        internal static System.Collections.Generic.List<PreparationFile> DataPreparationFiles(System.Collections.Generic.IList<PreparationFile> files,CancellationToken cancel)
        {
            VerifyPayload(null,cancel);
            var included=new System.Collections.Generic.List<PreparationFile>();
            foreach(var value in JsonArray.Parse(File.ReadAllText(Path.Combine(Bundle,"RuntimeInventory.json")))) {
                var row=value.GetObject();
                included.Add(new PreparationFile {Destination=row.GetNamedString("path"),Size=(long)row.GetNamedNumber("size"),Hash=row.GetNamedString("sha256")});
            }
            var result=PreparationEngine.SelectDataFiles(files,included);
            Log("combined_data_preparation included_runtime_verified=1 obsolete_runtime_copies_ignored="+(files.Count-result.Count));
            return result;
        }
        static string SourceImport()
        {
            string journal=Path.Combine(GamePreparation.RuntimeRoot,"preparation-source.txt");
            string source=File.Exists(journal)?File.ReadLines(journal).Skip(1).FirstOrDefault():null;
            if(String.IsNullOrEmpty(source)||!GamePreparation.Imports().Contains(source,StringComparer.OrdinalIgnoreCase))
                throw new IOException("Import your supported game files so this Xbox can generate the runtime. Existing game data is preserved.");
            return source;
        }
        static void EnsureSteamCompanion(CancellationToken cancel)
        {
            const long size=1958912;
            const string hash="0cfe547ea82071953cf99daffa3bd11bb468eec0e400961e7e33e4dc36674ea8";
            string source=Path.Combine(Bundle,"PreparationSupport","PreparationPayload","steam_api64_real.dll");
            string target=Path.Combine(Data,"steam_api64_real.dll");
            cancel.ThrowIfCancellationRequested();Verify(source,size,hash);
            if(File.Exists(target)&&new FileInfo(target).Length==size&&AssetPreparation.Bytes.FileHash(target)==hash)return;
            Directory.CreateDirectory(Data);
            string temporary=target+"."+Guid.NewGuid().ToString("N")+".partial";
            try {
                File.Copy(source,temporary,false);Verify(temporary,size,hash);cancel.ThrowIfCancellationRequested();
                if(File.Exists(target))File.Replace(temporary,target,null);else File.Move(temporary,target);
                Verify(target,size,hash);
                Log("private_steam_companion_installed verified=1");
            } finally {if(File.Exists(temporary))File.Delete(temporary);}
        }
        internal static void EnsureOwnedFiles(Action<string> progress,CancellationToken cancel)
        {
            EnsureSteamCompanion(cancel);
            string recipe=GamePreparation.PatchPath;
            VerifyInventory(recipe,GamePreparation.PatchHash);
            var p=JsonObject.Parse(File.ReadAllText(recipe));string module=Path.Combine(Data,"SpiderManStartupModule.dll");
            if(!File.Exists(module)) {
                progress?.Invoke("Building the game module from your own installation…");
                var patch=new ModulePatch { SourceSize=(long)p.GetNamedNumber("source_size"),ResultSize=(long)p.GetNamedNumber("result_size"),SourceHash=p.GetNamedString("source_sha256"),ResultHash=p.GetNamedString("result_sha256"),Append=Hex(p.GetNamedString("append_hex")),Replacements=new System.Collections.Generic.List<ModuleReplacement>() };
                foreach(var v in p.GetNamedArray("replacements")) { var row=v.GetObject();patch.Replacements.Add(new ModuleReplacement {Offset=(long)row.GetNamedNumber("offset"),Before=Hex(row.GetNamedString("before")),After=Hex(row.GetNamedString("after"))}); }
                Directory.CreateDirectory(Data);
                PreparationEngine.Derive(Path.Combine(SourceImport(),"Spider-Man.exe"),module,patch,cancel);
            }
            cancel.ThrowIfCancellationRequested();Verify(module,(long)p.GetNamedNumber("result_size"),p.GetNamedString("result_sha256"));
            CrashReporterPreparation.Ensure(Path.Combine(Data,"crs-client.dll"),progress,cancel);
            if(!AssetPreparation.PipelineGenerator.IsReady(Pipelines)&&!AssetPreparation.PipelineGenerator.VerifyExisting(Pipelines,progress,cancel))
                GamePreparation.GeneratePipelines(SourceImport(),progress,cancel);
            if(!AssetPreparation.PipelineGenerator.IsReady(Pipelines))throw new IOException("Shader preparation is incomplete; original files are preserved.");
            string original=Path.Combine(SourceImport(),"Spider-Man.exe");
            AssetPreparation.ComputeGenerator.Run(File.Exists(original)?original:module,GamePreparation.ComputeMap,Path.Combine(Data,"first-launch-compute-v1"),progress,cancel);
            AssetPreparation.OwnedCompressionPreparation.Run(File.Exists(original)?original:module,Path.Combine(Bundle,"CompilerTools"),Path.Combine(ApplicationData.Current.LocalFolder.Path,"owned-compression-shaders-v1"),progress,cancel);
            Log("owned_runtime_verified bundled_game_module=0 bundled_shader_recipes=0 generated_recipes="+AssetPreparation.PipelineGenerator.Count);
        }
        static byte[] Hex(string value)
        {
            if(value.Length%2!=0||value.Length>1024*1024||value.Any(c=>!Uri.IsHexDigit(c)))throw new IOException("Invalid module recipe bytes.");
            var bytes=new byte[value.Length/2];for(int i=0;i<bytes.Length;i++)bytes[i]=Convert.ToByte(value.Substring(i*2,2),16);return bytes;
        }
        internal static void SeedMissingSettings()
        {
            string source=Path.Combine(Bundle,"InitialSettings"),destination=ApplicationData.Current.LocalFolder.Path;
            VerifyInventory(Path.Combine(source,"RuntimeDefaults.json"),"91d56b8a369b2706a611acb6d150390aa6349b825a85cb5d423d7e86824f1e98");
            var rows=JsonArray.Parse(File.ReadAllText(Path.Combine(source,"RuntimeDefaults.json")));
            foreach(var value in rows) {
                var row=value.GetObject();string relative=row.GetNamedString("path");
                if(String.IsNullOrEmpty(relative)||Path.IsPathRooted(relative)||relative.Contains("..")||relative.Contains(":")||relative.Contains("\\"))throw new IOException("Invalid startup settings path.");
                Verify(Path.Combine(source,"Files",relative),(long)row.GetNamedNumber("size"),row.GetNamedString("sha256"));
            }
            foreach(var value in rows) {
                string relative=value.GetObject().GetNamedString("path"),target=Path.Combine(destination,relative);
                if(File.Exists(target))continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(Path.Combine(source,"Files",relative),target,false);
            }
            VerifyInventory(Path.Combine(source,"DerivedRecipeDefaults.json"),"38bed09b36919398b79026a30887aa52afc8c019194c3511ed6777834c1f6c1b");
            foreach(var value in JsonArray.Parse(File.ReadAllText(Path.Combine(source,"DerivedRecipeDefaults.json")))) {
                var row=value.GetObject();string relative=row.GetNamedString("path");
                if(String.IsNullOrEmpty(relative)||relative.Contains("..")||relative.Contains(":")||relative.Contains("\\")||Path.IsPathRooted(relative))throw new IOException("Invalid derived recipe path.");
                string target=Path.Combine(destination,relative);if(File.Exists(target))continue;
                string name=Path.GetFileName(relative),key=Path.GetFileNameWithoutExtension(name);
                if(key.Length!=64||key.Any(c=>!Uri.IsHexDigit(c)))throw new IOException("Invalid derived recipe identity.");
                byte[] body=File.ReadAllBytes(Path.Combine(Pipelines,name));
                if(body.Length!=(long)row.GetNamedNumber("size")||body.Length<48)throw new IOException("Derived recipe size differs.");
                // Costs are scheduling metadata, not shader contents. Preserve
                // the accepted default while sourcing every shader from the import.
                byte[] cost=BitConverter.GetBytes((uint)row.GetNamedNumber("cost_ms"));Array.Copy(cost,0,body,8,4);
                if(AssetPreparation.Bytes.Hash(body)!=row.GetNamedString("sha256"))throw new IOException("Derived recipe checksum differs.");
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                using(var file=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(body,0,body.Length);file.Flush(true);}
            }
            Log("standalone_defaults_seeded accepted_fixes=1 original_settings_preserved=1");
        }
        internal static void EnsurePreparationDataRoot(Action<string> progress,CancellationToken cancel)
        {
            VerifyPayload(progress,cancel);string root=GamePreparation.RuntimeRoot,marker=Path.Combine(root,"runtime-bootstrap.json");
            Directory.CreateDirectory(root);
            if(File.Exists(marker)) {
                if(JsonObject.Parse(File.ReadAllText(marker)).GetNamedString("family")!=GamePreparation.RuntimeFamily)throw new IOException("The game-data directory belongs to a different runtime.");
            } else {
                var data=new JsonObject();data["family"]=JsonValue.CreateStringValue(GamePreparation.RuntimeFamily);data["combined_package_family"]=JsonValue.CreateStringValue(Family);
                using(var writer=new StreamWriter(new FileStream(marker,FileMode.CreateNew,FileAccess.Write,FileShare.None)))writer.Write(data.Stringify());
            }
            Directory.CreateDirectory(Path.Combine(root,"Defaults"));
            foreach(string name in new[]{"engine-pool-capacities.txt","spider-graphics-settings.bin"}) {
                string target=Path.Combine(root,"Defaults",name);if(!File.Exists(target))File.Copy(Path.Combine(Bundle,"Defaults",name),target,false);
            }
        }
        internal static async Task<bool> Launch()
        {
            await Task.Run(()=>{VerifyPayload(null,CancellationToken.None);EnsureOwnedFiles(null,CancellationToken.None);SeedMissingSettings();});
            var support=await Launcher.QueryUriSupportAsync(PlayUri,LaunchQuerySupportType.Uri,Family);
            if(support!=LaunchQuerySupportStatus.Available)throw new IOException("The included game entry is unavailable. Reinstall this launcher; imported files are preserved.");
            return await Launcher.LaunchUriAsync(PlayUri,new LauncherOptions {TargetApplicationPackageFamilyName=Family});
        }
    }
}
