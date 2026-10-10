using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Storage;
using Windows.System;
using Windows.System.Profile;

namespace SpiderManInstaller
{
    static class GamePreparation
    {
        internal const string RuntimeFamily="Local.SpiderManRuntime_tw4nvkrr1gm8e";
        internal const string RuntimeRoot=@"D:\DevelopmentFiles\LooseApps\SpiderManRuntime-20261008";
        static string Bundle => Package.Current.InstalledLocation.Path;
        // Compatibility data is supplied by the separately installed runtime.
        static string Support => Path.Combine(RuntimeRoot,"PreparationSupport");
        const string ExpectedRecipe = "9903cdd6f2a39646e2d27fc37e5845d1de35bc4d711d1f09a421dc33661e5e14";
        const string ExpectedPatch = "01c967d9f6fcc203b3c9763d4ecacd9066ac2f77f20dd94b69a694f722d12e9d";
        static void RequireSupport()
        {
            foreach(var entry in new[]{new{ Name="PreparationRecipe.json",Hash=ExpectedRecipe },new{ Name="ModulePatch.json",Hash=ExpectedPatch }})
            {
                string path=Path.Combine(Support,entry.Name);
                if(!File.Exists(path))throw new IOException("Install the compatible separate runtime preparation support first. Your game files are preserved.");
                if(AssetPreparation.Bytes.FileHash(path)!=entry.Hash)throw new IOException("Runtime preparation support version mismatch. Update the separate runtime; your game files are preserved.");
            }
        }
        internal static string RecipeHash { get { return AssetPreparation.Bytes.Hash(System.Text.Encoding.UTF8.GetBytes("on-device-assets-v1\n"+ExpectedRecipe+"\n"+AssetPreparation.Bytes.FileHash(Path.Combine(Bundle,"AssetRecipes","batch.tsv")))); } }
        static string LegacyRecipeHash => ExpectedRecipe;
        internal static List<string> Imports()
        {
            string journal=Path.Combine(RuntimeRoot,"preparation-source.txt");
            string resumable=File.Exists(journal)?File.ReadAllLines(journal).Skip(1).FirstOrDefault():null;
            return Directory.GetDirectories(GameStorage.OpenRoot()).Where(p=>
                File.Exists(Path.Combine(p,"import-receipt.txt")) &&
                (Directory.Exists(Path.Combine(p,"asset_archive")) ||
                 (String.Equals(p,resumable,StringComparison.OrdinalIgnoreCase) && Directory.Exists(Path.Combine(RuntimeRoot,"StartupContent","asset_archive")))))
                .OrderBy(p=>p).ToList();
        }
        internal static bool Ready()
        {
            string p=Path.Combine(RuntimeRoot,"prepared-receipt.txt");
            string receipt=File.Exists(p)?File.ReadLines(p).FirstOrDefault():null;
            return !File.Exists(Path.Combine(RuntimeRoot,"deletion-pending.txt")) && (receipt==RecipeHash || receipt==LegacyRecipeHash) &&
                new[]{"Defaults/engine-pool-capacities.txt","Defaults/spider-graphics-settings.bin","StartupContent/SpiderManNativeStartup.exe","StartupContent/SpiderManStartupModule.dll","StartupContent/asset_archive/toc"}.All(n=>File.Exists(Path.Combine(RuntimeRoot,n)));
        }
        static byte[] Hex(string s)
        {
            if(s.Length%2!=0)throw new IOException("Invalid preparation bytes");
            var b=new byte[s.Length/2];for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(s.Substring(i*2,2),16);return b;
        }
        internal static List<LanguagePack> LoadLanguagePacks()
        {
            var packs=JsonArray.Parse(File.ReadAllText(Path.Combine(Bundle,"LanguagePacks.json"))).Select(v=>
            {
                var o=v.GetObject();return new LanguagePack { Language=o.GetNamedString("language"), Label=o.GetNamedString("label"),
                    File=new ExpectedFile { Path=o.GetNamedString("path"), Size=(long)o.GetNamedNumber("size"), Hash=o.GetNamedString("sha256") } };
            }).ToList();
            LanguageImports.Validate(packs);return packs;
        }
        internal static void Prepare(string import,Action<string> progress,CancellationToken cancel)
        {
            if(Ready())throw new IOException("This runtime is already prepared. Choose Play.");
            RequireSupport();
            if(File.Exists(Path.Combine(RuntimeRoot,"deletion-pending.txt")))throw new IOException("Finish deleting game files in Storage before preparing again.");
            if(AnalyticsInfo.VersionInfo.DeviceFamily!="Windows.Xbox")throw new IOException("Game preparation currently requires Xbox Developer Mode. Importing is also available on PC.");
            if(!Imports().Contains(import,StringComparer.OrdinalIgnoreCase))throw new IOException("Select a verified import from this launcher");
            var marker=JsonObject.Parse(File.ReadAllText(Path.Combine(RuntimeRoot,"runtime-bootstrap.json")));
            if(marker.GetNamedString("family")!=RuntimeFamily)throw new IOException("Different runtime installed");
            foreach(string n in new[]{"engine-pool-capacities.txt","spider-graphics-settings.bin"})
                if(!File.Exists(Path.Combine(RuntimeRoot,"Defaults",n)))throw new IOException("Runtime defaults missing; finish runtime installation first");
            using(var host=File.OpenRead(Path.Combine(RuntimeRoot,"StartupContent","SpiderManNativeStartup.exe")))
                if(ImportEngine.Digest(host)!=marker.GetNamedString("host_sha256"))throw new IOException("Runtime host failed verification");
            var rows=new List<PreparationFile>();
            foreach(var v in JsonArray.Parse(File.ReadAllText(Path.Combine(Support,"PreparationRecipe.json"))))
            {
                var o=v.GetObject();rows.Add(new PreparationFile { Destination=o.GetNamedString("destination"),Origin=o.GetNamedString("origin"),Source=o.GetNamedString("source"),Size=(long)o.GetNamedNumber("size"),Hash=o.GetNamedString("sha256") });
            }
            string archives=Path.Combine(import,"asset_archive");
            if(!Directory.Exists(archives))archives=Path.Combine(RuntimeRoot,"StartupContent","asset_archive");
            LanguageImports.ApplyToPreparation(archives,rows,LoadLanguagePacks());
            var p=JsonObject.Parse(File.ReadAllText(Path.Combine(Support,"ModulePatch.json")));
            var patch=new ModulePatch { SourceSize=(long)p.GetNamedNumber("source_size"),ResultSize=(long)p.GetNamedNumber("result_size"),SourceHash=p.GetNamedString("source_sha256"),ResultHash=p.GetNamedString("result_sha256"),Append=Hex(p.GetNamedString("append_hex")),Replacements=new List<ModuleReplacement>() };
            foreach(var v in p.GetNamedArray("replacements"))
            { var o=v.GetObject();patch.Replacements.Add(new ModuleReplacement { Offset=(long)o.GetNamedNumber("offset"),Before=Hex(o.GetNamedString("before")),After=Hex(o.GetNamedString("after")) }); }
            long extra=rows.Where(r=>r.Origin!="import" || !r.Source.StartsWith("asset_archive/",StringComparison.OrdinalIgnoreCase)).Sum(r=>r.Size);
            long generatedUpperBound=File.ReadLines(Path.Combine(Bundle,"AssetRecipes","batch.tsv")).Skip(1).Sum(line=>long.Parse(line.Split('\t')[3],System.Globalization.CultureInfo.InvariantCulture));
            long needed=checked(extra+generatedUpperBound+256L*1024*1024);
            if(GameStorage.FreeBytes(RuntimeRoot)<needed)throw new IOException("Preparation needs "+(needed/1073741824.0).ToString("F1")+" GiB free, including generated assets and staging. It reuses the imported archives.");
            // Check the original index even on a resumed operation after archive relocation.
            var tocRow=JsonArray.Parse(File.ReadAllText(Path.Combine(Bundle,"SteamFiles.json"))).Select(v=>v.GetObject()).Single(o=>o.GetNamedString("path")=="asset_archive/toc");
            string toc=Path.Combine(import,"asset_archive","toc");
            if(!File.Exists(toc))toc=Path.Combine(RuntimeRoot,"original-asset-toc");
            if(!File.Exists(toc))toc=Path.Combine(RuntimeRoot,"StartupContent","asset_archive","toc");
            using(var f=File.OpenRead(toc))if(f.Length!=(long)tocRow.GetNamedNumber("size") || ImportEngine.Digest(f)!=tocRow.GetNamedString("sha256"))throw new IOException("Original asset index failed verification");
            // Generate in a separate persistent directory before moving any
            // imported archive. Each complete output is verified on resume.
            string generatedRoot=Path.Combine(RuntimeRoot,"GeneratedPreparation");
            var generated=AssetPreparation.AssetBatch.Run(archives,toc,Path.Combine(Bundle,"AssetRecipes"),Path.Combine(generatedRoot,"asset_archive"),progress,cancel);
            foreach(var file in generated)
            {
                string destination=file.Name=="runtime-toc"?"toc":"asset_archive/"+file.Name;
                var row=rows.Single(r=>r.Destination==destination);
                row.Origin="generated";row.Source="asset_archive/"+file.Name;row.Size=file.Size;row.Hash=file.Hash;
            }
            PreparationEngine.Run(import,RuntimeRoot,Path.Combine(Support,"PreparationPayload"),rows,patch,RecipeHash,progress,cancel,generatedRoot);
        }
        internal static async Task<bool> Play()
        {
            if(!Ready())throw new IOException("Prepare and verify the game first");
            return await Launcher.LaunchUriAsync(new Uri("spiderman-runtime:play"),new LauncherOptions { TargetApplicationPackageFamilyName=RuntimeFamily });
        }
        internal static GameContentCleanup CleanupPlan()
        {
            RequireSupport();
            if(AnalyticsInfo.VersionInfo.DeviceFamily!="Windows.Xbox")throw new IOException("Game cleanup is available on Xbox only.");
            var marker=JsonObject.Parse(File.ReadAllText(Path.Combine(RuntimeRoot,"runtime-bootstrap.json")));
            if(marker.GetNamedString("family")!=RuntimeFamily)throw new IOException("The runtime does not belong to this launcher.");
            var destinations=JsonArray.Parse(File.ReadAllText(Path.Combine(Support,"PreparationRecipe.json")))
                .Select(v=>v.GetObject().GetNamedString("destination"));
            return new GameContentCleanup(RuntimeRoot,destinations);
        }
    }
}
