using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace SpiderManInstaller
{
    public sealed class PreparationFile
    {
        public string Destination, Origin, Source, Hash;
        public long Size;
    }
    public sealed class ModuleReplacement { public long Offset; public byte[] Before, After; }
    public sealed class ModulePatch
    {
        public long SourceSize, ResultSize;
        public string SourceHash, ResultHash;
        public byte[] Append;
        public List<ModuleReplacement> Replacements;
    }
    public static class PreparationEngine
    {
        static string Full(string root,string relative)
        {
            string safe=ImportEngine.SafePath(relative);
            string prefix=Path.GetFullPath(root).TrimEnd('\\','/')+Path.DirectorySeparatorChar;
            string result=Path.GetFullPath(Path.Combine(root,safe.Replace('/',Path.DirectorySeparatorChar)));
            if(!result.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) throw new IOException("Unsafe preparation path");
            return result;
        }
        static string Hash(string path,CancellationToken cancel)
        {
            using(var input=File.OpenRead(path)) using(var hash=SHA256.Create())
            {
                var b=new byte[1024*1024];int n;
                while((n=input.Read(b,0,b.Length))!=0) { cancel.ThrowIfCancellationRequested();hash.TransformBlock(b,0,n,b,0); }
                hash.TransformFinalBlock(new byte[0],0,0);
                return BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant();
            }
        }
        static void Verify(string path,long size,string hash,CancellationToken cancel)
        {
            if(!File.Exists(path) || new FileInfo(path).Length!=size || !Hash(path,cancel).Equals(hash,StringComparison.OrdinalIgnoreCase))
                throw new IOException("Preparation verification failed: "+Path.GetFileName(path));
        }
        static void Copy(string source,string target,long size,string hash,CancellationToken cancel)
        {
            if(File.Exists(target)) { Verify(target,size,hash,cancel); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            string partial=target+".prepare-partial";
            // Only this operation's generated partial name is disposable.
            if(File.Exists(partial)) File.Delete(partial);
            using(var input=File.OpenRead(source)) using(var output=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {
                var b=new byte[1024*1024];int n;
                while((n=input.Read(b,0,b.Length))!=0) { cancel.ThrowIfCancellationRequested();output.Write(b,0,n); }
                output.Flush();
            }
            Verify(partial,size,hash,cancel); File.Move(partial,target);
        }
        static void Derive(string source,string target,ModulePatch patch,CancellationToken cancel)
        {
            if(File.Exists(target)) { Verify(target,patch.ResultSize,patch.ResultHash,cancel);return; }
            Verify(source,patch.SourceSize,patch.SourceHash,cancel);
            if(patch.Append==null || patch.SourceSize+patch.Append.Length!=patch.ResultSize) throw new IOException("Invalid module recipe");
            long end=0;
            foreach(var change in patch.Replacements)
            {
                if(change.Offset<end || change.Before.Length!=change.After.Length || change.Offset+change.Before.Length>patch.SourceSize)
                    throw new IOException("Invalid module replacement range");
                end=change.Offset+change.Before.Length;
            }
            string partial=target+".prepare-partial";
            if(File.Exists(partial)) File.Delete(partial);
            File.Copy(source,partial,false);
            using(var output=new FileStream(partial,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            {
                foreach(var change in patch.Replacements)
                {
                    cancel.ThrowIfCancellationRequested();output.Position=change.Offset;
                    foreach(byte value in change.Before) if(output.ReadByte()!=value) throw new IOException("Module input mismatch");
                    output.Position=change.Offset;output.Write(change.After,0,change.After.Length);
                }
                output.Position=patch.SourceSize;output.Write(patch.Append,0,patch.Append.Length);output.Flush();
            }
            Verify(partial,patch.ResultSize,patch.ResultHash,cancel);File.Move(partial,target);
        }
        public static void Run(string importRoot,string runtimeRoot,string payloadRoot,
            IList<PreparationFile> files,ModulePatch patch,string recipeHash,Action<string> report,CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            importRoot=Path.GetFullPath(importRoot);runtimeRoot=Path.GetFullPath(runtimeRoot);
            if(importRoot.Equals(runtimeRoot,StringComparison.OrdinalIgnoreCase) || runtimeRoot.StartsWith(importRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || importRoot.StartsWith(runtimeRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new IOException("Runtime must be separate from the import");
            if(files.Count==0 || recipeHash==null || recipeHash.Length!=64 || recipeHash.Any(c=>!Uri.IsHexDigit(c))) throw new IOException("Invalid preparation identity");
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var row in files)
            {
                Full(runtimeRoot,row.Destination);Full(payloadRoot,row.Source);
                if(!names.Add(ImportEngine.SafePath(row.Destination)) || row.Size<0 || row.Hash==null || row.Hash.Length!=64 || row.Hash.Any(c=>!Uri.IsHexDigit(c))) throw new IOException("Invalid preparation recipe");
                if(row.Origin!="import" && row.Origin!="payload" && row.Origin!="derived")throw new IOException("Unknown preparation input");
                if(row.Origin=="import" && row.Source.StartsWith("asset_archive/",StringComparison.OrdinalIgnoreCase) && row.Source!=row.Destination) throw new IOException("Archive paths must match");
                if(row.Origin=="derived" && (row.Size!=patch.ResultSize || row.Hash!=patch.ResultHash))throw new IOException("Module recipe identity mismatch");
            }
            string stage=Path.Combine(runtimeRoot,"StartupContent");
            string journal=Path.Combine(runtimeRoot,"preparation-source.txt");
            string binding=recipeHash+"\n"+importRoot;
            if(File.Exists(journal) && File.ReadAllText(journal)!=binding)throw new IOException("Runtime belongs to a different preparation. Keep it and choose another runtime.");
            if(!File.Exists(Path.Combine(runtimeRoot,"runtime-bootstrap.json"))) throw new IOException("Install the separate game runtime first");
            if(!File.Exists(Path.Combine(importRoot,"import-receipt.txt"))) throw new IOException("Choose a completed, verified import");
            string ready=Path.Combine(runtimeRoot,"prepared-receipt.txt");
            if(File.Exists(ready))throw new IOException("This runtime is already prepared");
            Directory.CreateDirectory(stage);
            if(!File.Exists(journal)) File.WriteAllText(journal,binding);
            string sourceAssets=Path.Combine(importRoot,"asset_archive");
            string targetAssets=Path.Combine(stage,"asset_archive");
            bool moved=!Directory.Exists(sourceAssets) && Directory.Exists(targetAssets);
            if(Directory.Exists(sourceAssets) && Directory.Exists(targetAssets)) throw new IOException("Both source and runtime archives exist; refusing to overwrite either");
            if(!moved && !Directory.Exists(sourceAssets))throw new IOException("Imported assets are missing");
            Verify(Full(importRoot,"Spider-Man.exe"),patch.SourceSize,patch.SourceHash,cancel);
            var archiveInputs=files.Where(r=>r.Origin=="import" && r.Source.StartsWith("asset_archive/",StringComparison.OrdinalIgnoreCase)).ToList();
            int index=0;
            foreach(var row in archiveInputs)
            {
                report?.Invoke("Verifying game archives "+(++index)+" / "+archiveInputs.Count+": "+Path.GetFileName(row.Source));
                Verify(Full(moved?stage:importRoot,row.Source),row.Size,row.Hash,cancel);
            }
            string pending=Path.Combine(runtimeRoot,"PreparedOverlays");
            index=0;
            foreach(var row in files)
            {
                cancel.ThrowIfCancellationRequested();
                if(row.Origin=="import" && row.Source.StartsWith("asset_archive/",StringComparison.OrdinalIgnoreCase)) continue;
                report?.Invoke("Preparing Xbox files "+(++index)+": "+row.Destination);
                bool overlay=row.Destination.StartsWith("asset_archive/",StringComparison.OrdinalIgnoreCase);
                string target=Full(overlay?pending:stage,row.Destination);
                if(row.Origin=="derived") Derive(Full(importRoot,row.Source),target,patch,cancel);
                else Copy(Full(row.Origin=="import"?importRoot:payloadRoot,row.Source),target,row.Size,row.Hash,cancel);
            }
            // All slow work is verified before this short, resumable commit.
            cancel.ThrowIfCancellationRequested();report?.Invoke("Finishing preparation—keeping game archives on D:");
            if(!moved)Directory.Move(sourceAssets,targetAssets);
            string savedToc=Path.Combine(runtimeRoot,"original-asset-toc");
            string toc=Path.Combine(targetAssets,"toc");
            if(!File.Exists(savedToc))File.Move(toc,savedToc);
            foreach(var row in files.Where(r=>r.Origin=="payload" && r.Destination.StartsWith("asset_archive/",StringComparison.OrdinalIgnoreCase)))
            {
                string target=Full(stage,row.Destination),from=Full(pending,row.Destination);
                if(File.Exists(target))Verify(target,row.Size,row.Hash,CancellationToken.None);
                else File.Move(from,target);
            }
            foreach(var row in files)
            {
                string target=Full(stage,row.Destination);
                if(!File.Exists(target)||new FileInfo(target).Length!=row.Size)throw new IOException("Incomplete prepared runtime: "+row.Destination);
            }
            File.WriteAllText(ready,recipeHash+"\nPrepared from owned verified import: "+importRoot+"\nUTC: "+DateTime.UtcNow.ToString("o"));
            report?.Invoke("Prepared and verified. Ready to play.");
        }
    }
}
