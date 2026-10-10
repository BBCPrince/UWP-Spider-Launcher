using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using SpiderManInstaller;

class LanguageTests
{
    sealed class Source : IFolderImportSource
    {
        public Dictionary<string,byte[]> Data = new Dictionary<string,byte[]>();
        public bool DenyAudio;
        public Task<Stream> OpenReadAsync(string name,CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            if(DenyAudio && name.EndsWith(".fr"))throw new UnauthorizedAccessException("fixture denial");
            if(!Data.ContainsKey(name))throw new FileNotFoundException(name);
            return Task.FromResult<Stream>(new MemoryStream(Data[name]));
        }
    }
    static string root;
    static int count;
    static List<LanguagePack> packs;
    static byte[] Bytes(string text)=>Encoding.UTF8.GetBytes(text);
    static ExpectedFile FileRow(string path,byte[] data)=>new ExpectedFile{Path=path,Size=data.Length,Hash=ImportEngine.Digest(new MemoryStream(data))};
    static Source MakeSource(params LanguagePack[] selected)
    {
        var s=new Source();s.Data["Spider-Man.exe"]=Bytes("exe fixture");s.Data["asset_archive/toc"]=Bytes("index fixture");s.Data["asset_archive/g00s000"]=Bytes("world fixture");
        foreach(var p in selected)s.Data[p.File.Path]=Bytes("audio "+p.Language);
        return s;
    }
    static List<ExpectedFile> Core(Source s)=>s.Data.Where(p=>!packs.Any(a=>a.File.Path==p.Key)).Select(p=>FileRow(p.Key,p.Value)).Concat(new[]{packs[0].File}).ToList();
    static string Target()=>Path.Combine(root,Guid.NewGuid().ToString("N"));
    static void Assert(bool ok,string text){if(!ok)throw new Exception(text);}
    static void Check(string name,Action test){test();count++;Console.WriteLine("PASS "+name);}
    static void Reject(Action test)
    {try{test();}catch(IOException){return;}catch(InvalidDataException){return;}catch(UnauthorizedAccessException){return;}throw new Exception("Unexpected success");}
    static void Folder(Source s,string dest,List<ExpectedFile> core=null,long free=Int64.MaxValue,CancellationToken ct=default(CancellationToken))
    {FolderImportEngine.ImportAsync(s,dest,core??Core(s),free,null,ct,packs).GetAwaiter().GetResult();}
    static MemoryStream Zip(Source source)
    {
        var stream=new MemoryStream();using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true))
            foreach(var row in source.Data)using(var f=zip.CreateEntry("Game/"+row.Key).Open())f.Write(row.Value,0,row.Value.Length);
        stream.Position=0;return stream;
    }
    static void Main(string[] args)
    {
        var catalogue=new JavaScriptSerializer().Deserialize<List<Dictionary<string,object>>>(File.ReadAllText(args[0]));
        packs=catalogue.Select(p=>new LanguagePack{Language=(string)p["language"],Label=(string)p["label"],File=FileRow((string)p["path"],Bytes("audio "+p["language"]))}).ToList();
        root=Path.Combine(Path.GetTempPath(),"SpiderLanguages-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            Assert(packs.Count==12,"Expected all 12 voice packs");
            foreach(var p in packs)
            {
                Check(p.Label+" folder alone",()=>{var s=MakeSource(p);var d=Target();Folder(s,d);Assert(File.Exists(Path.Combine(d,p.File.Path)),"audio missing");Assert(File.ReadAllText(Path.Combine(d,"import-receipt.txt")).Contains(p.Label),"language absent from receipt");if(p.Language!="english")Assert(!File.Exists(Path.Combine(d,packs[0].File.Path)),"English unexpectedly copied");});
                Check(p.Label+" ZIP alone",()=>{var s=MakeSource(p);var d=Target();using(var z=Zip(s))ImportEngine.Import(z,d,Core(s),Int64.MaxValue,null,CancellationToken.None,packs);Assert(File.Exists(Path.Combine(d,p.File.Path)),"audio missing");});
            }
            Check("All 12 together",()=>{var s=MakeSource(packs.ToArray());var d=Target();Folder(s,d);Assert(packs.All(p=>File.Exists(Path.Combine(d,p.File.Path))),"pack lost");});
            Check("No audio refuses before copy",()=>{var s=MakeSource();var d=Target();Reject(()=>Folder(s,d));Assert(!Directory.Exists(d),"wrote destination");});
            Check("Incomplete pack refuses before copy",()=>{var s=MakeSource(packs[1]);s.Data[packs[1].File.Path]=Bytes("short");var d=Target();Reject(()=>Folder(s,d));Assert(!Directory.Exists(d),"wrote destination");});
            Check("Corrupt audio never completes",()=>{var s=MakeSource(packs[1]);s.Data[packs[1].File.Path][0]^=1;var d=Target();Reject(()=>Folder(s,d));Assert(!File.Exists(Path.Combine(d,"import-receipt.txt")),"false receipt");});
            Check("Corrupt ZIP audio never completes",()=>{var s=MakeSource(packs[1]);s.Data[packs[1].File.Path][0]^=1;var d=Target();using(var z=Zip(s))Reject(()=>ImportEngine.Import(z,d,Core(s),Int64.MaxValue,null,CancellationToken.None,packs));Assert(!File.Exists(Path.Combine(d,"import-receipt.txt")),"false receipt");});
            Check("Unknown audio cannot replace known pack",()=>{var s=MakeSource();s.Data["asset_archive/a00s099.xx"]=Bytes("unknown");Reject(()=>Folder(s,Target()));});
            Check("Denied pack is not silently ignored",()=>{var s=MakeSource(packs[0]);s.DenyAudio=true;Reject(()=>Folder(s,Target()));});
            Check("Space includes all imported packs",()=>{var s=MakeSource(packs.ToArray());Reject(()=>Folder(s,Target(),null,ImportEngine.Margin+s.Data.Values.Sum(b=>(long)b.Length)-1));});
            Check("Core version remains strict",()=>{var s=MakeSource(packs[1]);var core=Core(s);s.Data["Spider-Man.exe"][0]^=1;var d=Target();Reject(()=>Folder(s,d,core));Assert(!Directory.Exists(d),"version failure wrote files");});
            Check("Existing destination protected",()=>{var s=MakeSource(packs[1]);var d=Target();Directory.CreateDirectory(d);Reject(()=>Folder(s,d));});
            Check("Cancellation propagates",()=>{bool cancelled=false;try{Folder(MakeSource(packs[1]),Target(),null,Int64.MaxValue,new CancellationToken(true));}catch(OperationCanceledException){cancelled=true;}Assert(cancelled,"cancellation swallowed");});
            Check("All preparation packs selected, then verified",()=>
            {
                var d=Target();Directory.CreateDirectory(d);foreach(var p in packs)System.IO.File.WriteAllBytes(Path.Combine(d,Path.GetFileName(p.File.Path)),Bytes("audio "+p.Language));
                var rows=new List<PreparationFile>{new PreparationFile{Destination=packs[0].File.Path,Source=packs[0].File.Path,Origin="import"},new PreparationFile{Destination="keep",Source="keep",Origin="payload"}};
                LanguageImports.ApplyToPreparation(d,rows,packs);Assert(rows.Count==13 && rows.Count(r=>r.Source==packs[0].File.Path)==1,"wrong recipe");
                Assert(rows.Where(r=>r.Origin=="import").All(r=>r.Hash.Length==64 && r.Size>0),"unverified audio row");
                System.IO.File.Delete(Path.Combine(d,Path.GetFileName(packs[0].File.Path)));LanguageImports.ApplyToPreparation(d,rows,packs);Assert(rows.All(r=>r.Source!=packs[0].File.Path),"still requires English");
            });
            Check("Duplicate catalogue rejected",()=>{var dup=packs.Concat(new[]{packs[0]}).ToList();Reject(()=>LanguageImports.Validate(dup));});
            Console.WriteLine(count+" language checks passed.");
        }
        finally{Directory.Delete(root,true);}
    }
}
