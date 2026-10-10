using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SpiderManInstaller;
class PreparationTests
{
 static string root,import,runtime,payload; static List<PreparationFile> rows; static ModulePatch patch;
 static string Hash(byte[] b){using(var m=new MemoryStream(b))return ImportEngine.Digest(m);}
 static void Write(string dir,string file,byte[] bytes){var path=Path.Combine(dir,file);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,bytes);}
 static void Init()
 {
  root=Path.Combine(Path.GetTempPath(),"SpiderPreparation-"+Guid.NewGuid().ToString("N"));import=Path.Combine(root,"import");runtime=Path.Combine(root,"runtime");payload=Path.Combine(root,"payload");
  Directory.CreateDirectory(payload);Write(runtime,"runtime-bootstrap.json",new byte[]{1});Write(import,"import-receipt.txt",new byte[]{1});
  Write(import,"Spider-Man.exe",new byte[]{1,2,3,4});Write(import,"asset_archive/toc",new byte[]{9,9});Write(import,"asset_archive/world",new byte[]{6,7,8});
  Write(payload,"asset_archive/toc",new byte[]{8,8});Write(payload,"asset_archive/local_fix",new byte[]{4,5});Write(payload,"bridge.dll",new byte[]{7});
  patch=new ModulePatch{SourceSize=4,SourceHash=Hash(new byte[]{1,2,3,4}),ResultSize=5,ResultHash=Hash(new byte[]{1,6,3,4,9}),Append=new byte[]{9},Replacements=new List<ModuleReplacement>{new ModuleReplacement{Offset=1,Before=new byte[]{2},After=new byte[]{6}}}};
  rows=new List<PreparationFile>{Row("asset_archive/world","import","asset_archive/world",new byte[]{6,7,8}),Row("bridge.dll","payload","bridge.dll",new byte[]{7}),Row("SpiderManStartupModule.dll","derived","Spider-Man.exe",new byte[]{1,6,3,4,9}),Row("asset_archive/toc","payload","asset_archive/toc",new byte[]{8,8}),Row("asset_archive/local_fix","payload","asset_archive/local_fix",new byte[]{4,5})};
 }
 static PreparationFile Row(string d,string o,string s,byte[] b)=>new PreparationFile{Destination=d,Origin=o,Source=s,Size=b.Length,Hash=Hash(b)};
 static void Run(Action<string> report=null,CancellationToken token=default(CancellationToken))=>PreparationEngine.Run(import,runtime,payload,rows,patch,new string('a',64),report,token);
 static void Assert(bool v,string msg){if(!v)throw new Exception(msg);}
 static void Reject(Action f){bool rejected=false;try{f();}catch(IOException){rejected=true;}catch(InvalidDataException){rejected=true;} Assert(rejected,"Expected rejection");}
 static void Check(string name,Action action){Init();try{action();Console.WriteLine("PASS "+name);}finally{Directory.Delete(root,true);}}
 static void Main()
 {
  Check("Exact module and assets; no large archive duplication",()=>{Run();Assert(!Directory.Exists(Path.Combine(import,"asset_archive")),"archive not moved");Assert(File.Exists(Path.Combine(runtime,"prepared-receipt.txt")),"no receipt");Assert(File.ReadAllBytes(Path.Combine(runtime,"original-asset-toc")).SequenceEqual(new byte[]{9,9}),"original index lost");Assert(File.ReadAllBytes(Path.Combine(runtime,"StartupContent/SpiderManStartupModule.dll")).SequenceEqual(new byte[]{1,6,3,4,9}),"wrong derived module");Assert(File.Exists(Path.Combine(import,"Spider-Man.exe")),"original EXE lost");});
  Check("Cancellation before commit preserves original archives; resume succeeds",()=>{var c=new CancellationTokenSource();try{Run(s=>{if(s.StartsWith("Preparing"))c.Cancel();},c.Token);}catch(OperationCanceledException){}Assert(File.Exists(Path.Combine(import,"asset_archive/toc")),"source lost");Assert(!File.Exists(Path.Combine(runtime,"prepared-receipt.txt")),"false success");Run();});
  Check("Resume after archive rename before index commit",()=>{Write(runtime,"preparation-source.txt",Encoding.UTF8.GetBytes(new string('a',64)+"\n"+import));Directory.CreateDirectory(Path.Combine(runtime,"StartupContent"));Directory.Move(Path.Combine(import,"asset_archive"),Path.Combine(runtime,"StartupContent/asset_archive"));Run();});
  Check("Corrupt EXE refused before relocation",()=>{Write(import,"Spider-Man.exe",new byte[]{9,2,3,4});Reject(()=>Run());Assert(Directory.Exists(Path.Combine(import,"asset_archive")),"source moved");});
  Check("Corrupt archive refused",()=>{Write(import,"asset_archive/world",new byte[]{9,7,8});Reject(()=>Run());});
  Check("Corrupt overlay refused before commit",()=>{Write(payload,"asset_archive/local_fix",new byte[]{9,5});Reject(()=>Run());Assert(Directory.Exists(Path.Combine(import,"asset_archive")),"source moved");});
  Check("Destination traversal refused",()=>{rows[0].Destination="../unrelated";Reject(()=>Run());});
  Check("Normalized duplicate destination refused",()=>{rows.Add(Row("asset_archive\\world","import","asset_archive/world",new byte[]{6,7,8}));Reject(()=>Run());});
  Check("Unrelated runtime journal protected",()=>{Write(runtime,"preparation-source.txt",new byte[]{1});Reject(()=>Run());});
  Check("Unexpected target protected",()=>{Write(runtime,"StartupContent/bridge.dll",new byte[]{99});Reject(()=>Run());Assert(File.ReadAllBytes(Path.Combine(runtime,"StartupContent/bridge.dll"))[0]==99,"overwritten");});
  Check("Unverified import refused",()=>{File.Delete(Path.Combine(import,"import-receipt.txt"));Reject(()=>Run());});
  Check("Missing bootstrap refused",()=>{File.Delete(Path.Combine(runtime,"runtime-bootstrap.json"));Reject(()=>Run());});
  Check("Repeated prepare protects ready runtime",()=>{Run();Reject(()=>Run());});
  Check("Wrong replacement expected bytes refused",()=>{patch.Replacements[0].Before=new byte[]{9};Reject(()=>Run());});
  Check("Nested roots refused",()=>{Reject(()=>PreparationEngine.Run(import,Path.Combine(import,"runtime"),payload,rows,patch,new string('a',64),null,CancellationToken.None));});
  Check("French-only preparation verifies audio and does not require English",()=>{
    var b=new byte[]{3,2,1};Write(import,"asset_archive/a00s035.fr",b);
    var packs=new List<LanguagePack>{new LanguagePack{Language="english",Label="English",File=new ExpectedFile{Path="asset_archive/a00s034.us",Size=3,Hash=Hash(b)}},new LanguagePack{Language="french",Label="French",File=new ExpectedFile{Path="asset_archive/a00s035.fr",Size=3,Hash=Hash(b)}}};
    rows.Add(Row("asset_archive/a00s034.us","import","asset_archive/a00s034.us",b));LanguageImports.ApplyToPreparation(Path.Combine(import,"asset_archive"),rows,packs);Run();Assert(File.Exists(Path.Combine(runtime,"StartupContent/asset_archive/a00s035.fr")),"French archive lost");
  });
  Check("Corrupt selected audio rejected before archive relocation",()=>{
    var b=new byte[]{3,2,1};Write(import,"asset_archive/a00s044.ar",new byte[]{3,2,0});
    var packs=new List<LanguagePack>{new LanguagePack{Language="arabic",Label="Arabic",File=new ExpectedFile{Path="asset_archive/a00s044.ar",Size=3,Hash=Hash(b)}}};
    LanguageImports.ApplyToPreparation(Path.Combine(import,"asset_archive"),rows,packs);Reject(()=>Run());Assert(Directory.Exists(Path.Combine(import,"asset_archive")),"source moved");
  });
  Check("Language selection resumes after archive relocation",()=>{
    var b=new byte[]{3,2,1};Write(import,"asset_archive/a00s044.ar",b);
    var packs=new List<LanguagePack>{new LanguagePack{Language="arabic",Label="Arabic",File=new ExpectedFile{Path="asset_archive/a00s044.ar",Size=3,Hash=Hash(b)}}};
    Write(runtime,"preparation-source.txt",Encoding.UTF8.GetBytes(new string('a',64)+"\n"+import));Directory.CreateDirectory(Path.Combine(runtime,"StartupContent"));Directory.Move(Path.Combine(import,"asset_archive"),Path.Combine(runtime,"StartupContent/asset_archive"));
    LanguageImports.ApplyToPreparation(Path.Combine(runtime,"StartupContent/asset_archive"),rows,packs);Run();Assert(File.Exists(Path.Combine(runtime,"prepared-receipt.txt")),"resume failed");
  });
  Console.WriteLine("18 preparation checks passed.");
 }
}
