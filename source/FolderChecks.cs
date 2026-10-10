using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpiderManInstaller
{
    internal static class FolderChecks
    {
        internal static async Task RunAsync(string parent, Func<string,Task<IFolderImportSource>> sourceFactory, Action<string> log)
        {
            Directory.CreateDirectory(parent);
            string sourcePath = Path.Combine(parent, "source"); Directory.CreateDirectory(Path.Combine(sourcePath,"asset_archive"));
            var data = new Dictionary<string,byte[]> {
                {"Spider-Man.exe",Encoding.UTF8.GetBytes("fixture exe - not executable")},
                {"asset_archive/toc",Encoding.UTF8.GetBytes("fixture index")},
                {"asset_archive/g00s000",Enumerable.Range(0,2097301).Select(i=>(byte)(i%251)).ToArray()}
            };
            foreach (var pair in data) File.WriteAllBytes(Path.Combine(sourcePath,pair.Key.Replace('/',Path.DirectorySeparatorChar)),pair.Value);
            File.WriteAllText(Path.Combine(sourcePath,"unrelated.txt"),"leave alone");
            var rows = data.Select(pair => new ExpectedFile {Path=pair.Key,Size=pair.Value.Length,Hash=ImportEngine.Digest(new MemoryStream(pair.Value))}).ToList();
            var source = await sourceFactory(sourcePath);
            long total = rows.Sum(r=>r.Size); int passed = 0;
            Action<bool,string> check = (ok,name)=>{if(!ok)throw new Exception(name);passed++;log("PASS "+name);};
            Func<string,string> dest = name=>Path.Combine(parent,name);
            Func<IFolderImportSource,string,IList<ExpectedFile>,long,CancellationToken,Action<long,long,string>,Task> run =
                (s,n,rs,free,ct,report)=>FolderImportEngine.ImportAsync(s,dest(n),rs,free,report,ct);
            Func<Func<Task>,string,Task> reject = async (action,name)=>{
                bool failed=false;
                try{await action();}catch(InvalidDataException){failed=true;}catch(IOException){failed=true;}catch(UnauthorizedAccessException){failed=true;}
                check(failed,name);
            };
            long last=0; int updates=0;
            await run(source,"ok",rows,total+ImportEngine.Margin,CancellationToken.None,(done,count,name)=>{
                if(done<last || done>count || count!=total)throw new Exception("Invalid progress");last=done;updates++;
            });
            check(last==total && updates>=3,"progress reaches total monotonically");
            foreach(var row in rows)
                using(var f=File.OpenRead(Path.Combine(dest("ok"),row.Path)))check(ImportEngine.Digest(f)==row.Hash,"verified folder copy "+row.Path);
            check(File.Exists(Path.Combine(dest("ok"),"import-receipt.txt")),"receipt only after verified copy");
            check(!File.Exists(Path.Combine(dest("ok"),"unrelated.txt")),"unrelated USB files excluded");
            check(File.ReadAllText(Path.Combine(sourcePath,"unrelated.txt"))=="leave alone","source left unchanged");
            await reject(()=>run(source,"ok",rows,Int64.MaxValue,CancellationToken.None,null),"existing destination protected");
            await reject(()=>run(source,"space",rows,total+ImportEngine.Margin-1,CancellationToken.None,null),"insufficient storage rejected");
            check(!Directory.Exists(dest("space")),"space failure writes nothing");
            var badManifest=rows.Select(r=>new ExpectedFile{Path=r.Path,Hash=r.Hash,Size=r.Size}).ToList();badManifest[2].Path="../escape";
            await reject(()=>run(source,"traversal",badManifest,Int64.MaxValue,CancellationToken.None,null),"manifest traversal rejected");
            var duplicates=rows.ToList();duplicates.Add(rows[0]);
            await reject(()=>run(source,"duplicate",duplicates,Int64.MaxValue,CancellationToken.None,null),"duplicate manifest rejected");
            string toc=Path.Combine(sourcePath,"asset_archive","toc");var original=File.ReadAllBytes(toc);var wrong=(byte[])original.Clone();wrong[0]^=1;File.WriteAllBytes(toc,wrong);
            await reject(()=>run(source,"version",rows,Int64.MaxValue,CancellationToken.None,null),"wrong version rejected before copy");
            check(!Directory.Exists(dest("version")),"version failure writes nothing");File.WriteAllBytes(toc,original);
            string archive=Path.Combine(sourcePath,"asset_archive","g00s000");File.Move(archive,archive+".hold");
            await reject(()=>run(source,"missing",rows,Int64.MaxValue,CancellationToken.None,null),"missing archive rejected");
            check(!Directory.Exists(dest("missing")),"missing-file preflight writes nothing");File.Move(archive+".hold",archive);
            File.WriteAllBytes(archive,new byte[3]);
            await reject(()=>run(source,"short",rows,Int64.MaxValue,CancellationToken.None,null),"wrong-size archive rejected");
            File.WriteAllBytes(archive,new byte[data["asset_archive/g00s000"].Length]);
            await reject(()=>run(source,"corrupt",rows,Int64.MaxValue,CancellationToken.None,null),"corrupt archive rejected during copy");
            check(!File.Exists(Path.Combine(dest("corrupt"),"import-receipt.txt")),"corrupt copy never marked complete");
            File.WriteAllBytes(archive,data["asset_archive/g00s000"]);
            using(var cancel=new CancellationTokenSource())
            {
                bool cancelled=false;
                try{await run(source,"cancel",rows,Int64.MaxValue,cancel.Token,(done,count,name)=>cancel.Cancel());}
                catch(OperationCanceledException){cancelled=true;}
                check(cancelled,"mid-copy cancellation");
                check(!File.Exists(Path.Combine(dest("cancel"),"import-receipt.txt")),"cancelled copy never marked complete");
            }
            // Opening exclusively after cancellation proves the input handle was released.
            using(var f=new FileStream(Path.Combine(sourcePath,"Spider-Man.exe"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                check(f.Length==rows[0].Size,"cancellation releases source handle");
            log("Passed "+passed+" folder-import checks.");
        }
    }
}
