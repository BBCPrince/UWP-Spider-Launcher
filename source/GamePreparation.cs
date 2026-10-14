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
        static string selectedProfile;
        internal static string Profile=>selectedProfile??GameVersionSupport.ReadPreparedProfile(RuntimeRoot);
        static string SupportBase=>CombinedRuntime.Enabled?Path.Combine(Bundle,"PreparationSupport"):Path.Combine(RuntimeRoot,"PreparationSupport");
        static string Support=>Profile==GameVersionSupport.Newer?Path.Combine(SupportBase,"Version40630"):SupportBase;
        internal static string PatchPath=>Path.Combine(Support,"ModulePatch.json");
        internal static string PatchHash=>Profile==GameVersionSupport.Newer?"9e8fbc31970d59df82b8c114000c9b9ea0879781d7df5ec484e93255a5e3bf6a":ExpectedPatch;
        static string PreparationHash=>Profile==GameVersionSupport.Newer?"19dffdd7d9a1fb0196c7a076f9aba8e712b11274c48147500ccba40a7780e3d0":ExpectedRecipe;
        internal static string PipelineMap=>Path.Combine(Bundle,"PipelinePreparation",Profile==GameVersionSupport.Newer?"pipeline-map-40630.tsv":"pipeline-map.tsv");
        internal static string ComputeMap=>Path.Combine(Bundle,"PipelinePreparation",Profile==GameVersionSupport.Newer?"compute-map-40630.tsv":"compute-map.tsv");
        const string PriorVersionSupportRecipe="bc1c6bc5ee864032d9231ec21a87222158acc731a6c257e46a4fb0226bf6d49c";
        static string PriorVersionSupportGeneratedHash=>AssetPreparation.Bytes.Hash(System.Text.Encoding.UTF8.GetBytes("on-device-assets-v1\n"+PriorVersionSupportRecipe+"\n"+AssetPreparation.Bytes.FileHash(Path.Combine(Bundle,"AssetRecipes","batch.tsv"))));
        const string PriorWakeObservationRecipe = "734fdf4eb728c389e690014d377df199c257efbdaac7e38272847bc9720c7909";
        const string PriorBindingBaselineRecipe = "5d7a0d758f0b4f21b1cb6f8e40ac179ce7ac9295794083cf8aad34f385fb8ea7";
        const string PriorMemoryProbeRecipe = "8cbcf6dd62566759c37d67686ad0171a08ffaf3d729853fb66e62a5a309699b0";
        const string PriorPriorityProbeRecipe = "ad59141f1b0fe4a521af304caea8fefd3b6da5b9876f40d53bae579f2f7b247e";
        const string PriorPriorityPairProbeRecipe = "71129628a7b884dd6fd9a7713e376a42c9e66ec8b2c68ee5e1fcb072647c2580";
        const string PriorBundledDataPreparationRecipe = "6b691ee2f274f28e799220068f16f603d9f0c067b3293f00c527d4f1db3171f8";
        const string ExpectedRecipe = "8350ad41bf9cb10ce1b5f06b14837b555c6d99dc9f3f9ade3eb0978e6133673a";
        const string PriorRecordingRecipe = "05f2b5ab81d5d6276ccc160b97bef5a4f082976691a791ccecfb57cc77265444";
        const string PriorWaitTotalsRecipe = "e03aa8908ba1909d4af9dc5b281c144f89b29d105d780c78a927c1a0a209a68d";
        const string PriorCombinedRecipe = "0f2f38fd9a840b4ded43208e57cc0fabfebc4dcafea48739cba578e3c8a629ac";
        const string PreviousRecipe = "80902a7aee284e4e72c0368c1ae0865f5bdeec8f8b3b620e00267da5d0bd6e69";
        const string OlderRecipe = "9903cdd6f2a39646e2d27fc37e5845d1de35bc4d711d1f09a421dc33661e5e14";
        const string ExpectedPatch = "01c967d9f6fcc203b3c9763d4ecacd9066ac2f77f20dd94b69a694f722d12e9d";
        static void RequireSupport()
        {
            foreach(var entry in new[]{new{ Name="PreparationRecipe.json",Hash=PreparationHash },new{ Name="ModulePatch.json",Hash=PatchHash }})
            {
                string path=Path.Combine(Support,entry.Name);
                if(!File.Exists(path))throw new IOException("Install the compatible separate runtime preparation support first. Your game files are preserved.");
                if(AssetPreparation.Bytes.FileHash(path)!=entry.Hash)throw new IOException("Runtime preparation support version mismatch. Update the separate runtime; your game files are preserved.");
            }
        }
        // Only the packaged host changed; user-derived content is identical.
        static string PriorLoaderFixRecipe=>Profile==GameVersionSupport.Newer?"8ce847973cc46dcf48ef651a6bd70418264890ab74d2f6be7843b7e35ba2af10":"4bd83513309769bb0f1c43af88cae76c5673bda75f9ccb9a3d41b434771e61d8";
        static string PriorLoaderFixGeneratedHash=>AssetPreparation.Bytes.Hash(System.Text.Encoding.UTF8.GetBytes("on-device-assets-v1\n"+PriorLoaderFixRecipe+"\n"+AssetPreparation.Bytes.FileHash(Path.Combine(Bundle,"AssetRecipes","batch.tsv"))));
        internal static string RecipeHash { get { return AssetPreparation.Bytes.Hash(System.Text.Encoding.UTF8.GetBytes("on-device-assets-v1\n"+PreparationHash+"\n"+AssetPreparation.Bytes.FileHash(Path.Combine(Bundle,"AssetRecipes","batch.tsv")))); } }
        static string PriorBackFixGeneratedHash => AssetPreparation.Bytes.Hash(System.Text.Encoding.UTF8.GetBytes("on-device-assets-v1\n80a1a4bb7e7475e812262ee93cefd4f5d904e9e05426af339d78fddf2666a96a\n"+AssetPreparation.Bytes.FileHash(Path.Combine(Bundle,"AssetRecipes","batch.tsv"))));
        static string PriorPriorityPairProbeGeneratedHash => AssetPreparation.Bytes.Hash(System.Text.Encoding.UTF8.GetBytes("on-device-assets-v1\n"+PriorPriorityPairProbeRecipe+"\n"+AssetPreparation.Bytes.FileHash(Path.Combine(Bundle,"AssetRecipes","batch.tsv"))));
        static string PriorBundledDataPreparationGeneratedHash => AssetPreparation.Bytes.Hash(System.Text.Encoding.UTF8.GetBytes("on-device-assets-v1\n"+PriorBundledDataPreparationRecipe+"\n"+AssetPreparation.Bytes.FileHash(Path.Combine(Bundle,"AssetRecipes","batch.tsv"))));
        static string PriorPriorityProbeGeneratedHash => AssetPreparation.Bytes.Hash(System.Text.Encoding.UTF8.GetBytes("on-device-assets-v1\n"+PriorPriorityProbeRecipe+"\n"+AssetPreparation.Bytes.FileHash(Path.Combine(Bundle,"AssetRecipes","batch.tsv"))));
        static string PriorMemoryProbeGeneratedHash => AssetPreparation.Bytes.Hash(System.Text.Encoding.UTF8.GetBytes("on-device-assets-v1\n"+PriorMemoryProbeRecipe+"\n"+AssetPreparation.Bytes.FileHash(Path.Combine(Bundle,"AssetRecipes","batch.tsv"))));
        static string PriorBindingBaselineGeneratedHash => AssetPreparation.Bytes.Hash(System.Text.Encoding.UTF8.GetBytes("on-device-assets-v1\n"+PriorBindingBaselineRecipe+"\n"+AssetPreparation.Bytes.FileHash(Path.Combine(Bundle,"AssetRecipes","batch.tsv"))));
        static string PriorWakeObservationGeneratedHash => AssetPreparation.Bytes.Hash(System.Text.Encoding.UTF8.GetBytes("on-device-assets-v1\n"+PriorWakeObservationRecipe+"\n"+AssetPreparation.Bytes.FileHash(Path.Combine(Bundle,"AssetRecipes","batch.tsv"))));
        static string PriorRecordingGeneratedHash => AssetPreparation.Bytes.Hash(System.Text.Encoding.UTF8.GetBytes("on-device-assets-v1\n"+PriorRecordingRecipe+"\n"+AssetPreparation.Bytes.FileHash(Path.Combine(Bundle,"AssetRecipes","batch.tsv"))));
        static string PriorWaitTotalsGeneratedHash => AssetPreparation.Bytes.Hash(System.Text.Encoding.UTF8.GetBytes("on-device-assets-v1\n"+PriorWaitTotalsRecipe+"\n"+AssetPreparation.Bytes.FileHash(Path.Combine(Bundle,"AssetRecipes","batch.tsv"))));
        static string PriorCombinedGeneratedHash => AssetPreparation.Bytes.Hash(System.Text.Encoding.UTF8.GetBytes("on-device-assets-v1\n"+PriorCombinedRecipe+"\n"+AssetPreparation.Bytes.FileHash(Path.Combine(Bundle,"AssetRecipes","batch.tsv"))));
        static string LegacyRecipeHash => PreviousRecipe;
        static string PreviousGeneratedRecipeHash => AssetPreparation.Bytes.Hash(System.Text.Encoding.UTF8.GetBytes("on-device-assets-v1\n"+PreviousRecipe+"\n"+AssetPreparation.Bytes.FileHash(Path.Combine(Bundle,"AssetRecipes","batch.tsv"))));
        static string OlderGeneratedRecipeHash => AssetPreparation.Bytes.Hash(System.Text.Encoding.UTF8.GetBytes("on-device-assets-v1\n"+OlderRecipe+"\n"+AssetPreparation.Bytes.FileHash(Path.Combine(Bundle,"AssetRecipes","batch.tsv"))));
        static string PipelineOutput => Path.Combine(RuntimeRoot,"StartupContent","first-launch-recipes-v1");
        internal static void GeneratePipelines(string import,Action<string> progress,CancellationToken cancel)
        {
            string archives=Path.Combine(import,"asset_archive");
            if(!Directory.Exists(archives))archives=Path.Combine(RuntimeRoot,"StartupContent","asset_archive");
            AssetPreparation.PipelineGenerator.Run(import,archives,PipelineMap,PipelineOutput,progress,cancel);
        }
        internal static void EnsurePipelines(Action<string> progress,CancellationToken cancel)
        {
            if(CombinedRuntime.Enabled){CombinedRuntime.VerifyPayload(progress,cancel);CombinedRuntime.EnsureOwnedFiles(progress,cancel);return;}
            RuntimeGraphicsRepair.Ensure(Path.Combine(RuntimeRoot,"StartupContent"),PreparationEngine.SharedPayloadRoot(SupportBase),progress);
            if(AssetPreparation.PipelineGenerator.IsReady(PipelineOutput))return;
            if(AssetPreparation.PipelineGenerator.VerifyExisting(PipelineOutput,progress,cancel))return;
            string journal=Path.Combine(RuntimeRoot,"preparation-source.txt");
            string import=File.Exists(journal)?File.ReadLines(journal).Skip(1).FirstOrDefault():null;
            if(String.IsNullOrEmpty(import)||!Imports().Contains(import,StringComparer.OrdinalIgnoreCase))
                throw new IOException("The original verified import is needed to build shader preparation. Import your supported game files again; your existing game and saves are preserved.");
            GeneratePipelines(import,progress,cancel);
        }
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
            if(Profile==GameVersionSupport.Newer&&receipt!=RecipeHash&&receipt!=PriorLoaderFixGeneratedHash)return false;
            return !File.Exists(Path.Combine(RuntimeRoot,"deletion-pending.txt")) && (receipt==RecipeHash || (CombinedRuntime.Enabled && receipt==PriorLoaderFixGeneratedHash) || (CombinedRuntime.Enabled && receipt==PriorVersionSupportGeneratedHash) || (CombinedRuntime.Enabled && receipt==PriorBundledDataPreparationGeneratedHash) || (CombinedRuntime.Enabled && receipt==PriorPriorityPairProbeGeneratedHash) || (CombinedRuntime.Enabled && receipt==PriorPriorityProbeGeneratedHash) || (CombinedRuntime.Enabled && receipt==PriorMemoryProbeGeneratedHash) || (CombinedRuntime.Enabled && receipt==PriorBindingBaselineGeneratedHash) || (CombinedRuntime.Enabled && (receipt==PriorWakeObservationGeneratedHash || receipt==PriorRecordingGeneratedHash)) || (CombinedRuntime.Enabled && receipt==PriorWaitTotalsGeneratedHash) || (CombinedRuntime.Enabled && (receipt==PriorCombinedGeneratedHash || receipt==PriorBackFixGeneratedHash)) || receipt==LegacyRecipeHash || receipt==PreviousGeneratedRecipeHash || receipt==OlderRecipe || receipt==OlderGeneratedRecipeHash) &&
                new[]{"Defaults/engine-pool-capacities.txt","Defaults/spider-graphics-settings.bin","StartupContent/SpiderManNativeStartup.exe","StartupContent/SpiderManStartupModule.dll","StartupContent/asset_archive/toc"}.All(n=>File.Exists(Path.Combine(CombinedRuntime.Enabled&&n!="StartupContent/asset_archive/toc"&&n!="StartupContent/SpiderManStartupModule.dll"?Bundle:RuntimeRoot,n)));
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
            selectedProfile=GameVersionSupport.Inspect(Path.Combine(import,"Spider-Man.exe"));
            try {PrepareCore(import,progress,cancel);File.WriteAllText(Path.Combine(RuntimeRoot,"game-version.txt"),selectedProfile);}
            finally {selectedProfile=null;}
        }
        static void PrepareCore(string import,Action<string> progress,CancellationToken cancel)
        {
            if(Ready())throw new IOException("This runtime is already prepared. Choose Play.");
            RequireSupport();
            if(File.Exists(Path.Combine(RuntimeRoot,"deletion-pending.txt")))throw new IOException("Finish deleting game files in Storage before preparing again.");
            if(AnalyticsInfo.VersionInfo.DeviceFamily!="Windows.Xbox")throw new IOException("Game preparation currently requires Xbox Developer Mode. Importing is also available on PC.");
            if(!Imports().Contains(import,StringComparer.OrdinalIgnoreCase))throw new IOException("Select a verified import from this launcher");
            if(CombinedRuntime.Enabled)CombinedRuntime.EnsurePreparationDataRoot(progress,cancel);
            var marker=JsonObject.Parse(File.ReadAllText(Path.Combine(RuntimeRoot,"runtime-bootstrap.json")));
            if(marker.GetNamedString("family")!=RuntimeFamily)throw new IOException("Different runtime installed");
            foreach(string n in new[]{"engine-pool-capacities.txt","spider-graphics-settings.bin"})
                if(!File.Exists(Path.Combine(RuntimeRoot,"Defaults",n)))throw new IOException("Runtime defaults missing; finish runtime installation first");
            if(!CombinedRuntime.Enabled) {
                using(var host=File.OpenRead(Path.Combine(RuntimeRoot,"StartupContent","SpiderManNativeStartup.exe")))
                    if(ImportEngine.Digest(host)!=marker.GetNamedString("host_sha256"))throw new IOException("Runtime host failed verification");
            }
            var rows=new List<PreparationFile>();
            foreach(var v in JsonArray.Parse(File.ReadAllText(Path.Combine(Support,"PreparationRecipe.json"))))
            {
                var o=v.GetObject();rows.Add(new PreparationFile { Destination=o.GetNamedString("destination"),Origin=o.GetNamedString("origin"),Source=o.GetNamedString("source"),Size=(long)o.GetNamedNumber("size"),Hash=o.GetNamedString("sha256") });
            }
            if(CombinedRuntime.Enabled)rows=CombinedRuntime.DataPreparationFiles(rows,cancel);
            string archives=Path.Combine(import,"asset_archive");
            if(!Directory.Exists(archives))archives=Path.Combine(RuntimeRoot,"StartupContent","asset_archive");
            LanguageImports.ApplyToPreparation(archives,rows,LoadLanguagePacks());
            var p=JsonObject.Parse(File.ReadAllText(Path.Combine(Support,"ModulePatch.json")));
            var patch=new ModulePatch { SourceSize=(long)p.GetNamedNumber("source_size"),ResultSize=(long)p.GetNamedNumber("result_size"),SourceHash=p.GetNamedString("source_sha256"),ResultHash=p.GetNamedString("result_sha256"),Append=Hex(p.GetNamedString("append_hex")),Replacements=new List<ModuleReplacement>() };
            foreach(var v in p.GetNamedArray("replacements"))
            { var o=v.GetObject();patch.Replacements.Add(new ModuleReplacement { Offset=(long)o.GetNamedNumber("offset"),Before=Hex(o.GetNamedString("before")),After=Hex(o.GetNamedString("after")) }); }
            long extra=rows.Where(r=>r.Origin!="import" || !r.Source.StartsWith("asset_archive/",StringComparison.OrdinalIgnoreCase)).Sum(r=>r.Size);
            long generatedUpperBound=File.ReadLines(Path.Combine(Bundle,"AssetRecipes","batch.tsv")).Skip(1).Sum(line=>long.Parse(line.Split('\t')[3],System.Globalization.CultureInfo.InvariantCulture));
            long needed=checked(extra+generatedUpperBound+640L*1024*1024);
            if(GameStorage.FreeBytes(RuntimeRoot)<needed)throw new IOException("Preparation needs "+(needed/1073741824.0).ToString("F1")+" GiB free, including generated assets and staging. It reuses the imported archives.");
            // Check the original index even on a resumed operation after archive relocation.
            var tocRow=JsonArray.Parse(File.ReadAllText(Path.Combine(Bundle,"SteamFiles.json"))).Select(v=>v.GetObject()).Single(o=>o.GetNamedString("path")=="asset_archive/toc");
            string toc=Path.Combine(import,"asset_archive","toc");
            if(!File.Exists(toc))toc=Path.Combine(RuntimeRoot,"original-asset-toc");
            if(!File.Exists(toc))toc=Path.Combine(RuntimeRoot,"StartupContent","asset_archive","toc");
            using(var f=File.OpenRead(toc))if(f.Length!=(long)tocRow.GetNamedNumber("size") || ImportEngine.Digest(f)!=tocRow.GetNamedString("sha256"))throw new IOException("Original asset index failed verification");
            // Generate in a separate persistent directory before moving any
            // imported archive. Each complete output is verified on resume.
            GeneratePipelines(import,progress,cancel);
            string generatedRoot=Path.Combine(RuntimeRoot,"GeneratedPreparation");
            var generated=AssetPreparation.AssetBatch.Run(archives,toc,Path.Combine(Bundle,"AssetRecipes"),Path.Combine(generatedRoot,"asset_archive"),progress,cancel);
            foreach(var file in generated)
            {
                string destination=file.Name=="runtime-toc"?"toc":"asset_archive/"+file.Name;
                var row=rows.Single(r=>r.Destination==destination);
                row.Origin="generated";row.Source="asset_archive/"+file.Name;row.Size=file.Size;row.Hash=file.Hash;
            }
            PreparationEngine.Run(import,RuntimeRoot,PreparationEngine.SharedPayloadRoot(SupportBase),rows,patch,RecipeHash,progress,cancel,generatedRoot);
        }
        internal static async Task<bool> Play()
        {
            if(!Ready())throw new IOException("Prepare and verify the game first");
            if(CombinedRuntime.Enabled)return await CombinedRuntime.Launch();
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
