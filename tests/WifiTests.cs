using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SpiderManInstaller;

class WifiTests
{
    static string root,page;static int passed;
    static readonly Dictionary<string,byte[]> data=new Dictionary<string,byte[]> {
        {"Spider-Man.exe",Encoding.UTF8.GetBytes("version fingerprint")},
        {"asset_archive/toc",Encoding.UTF8.GetBytes("asset index")},
        {"asset_archive/world",Enumerable.Range(0,2048*1024).Select(i=>(byte)(i%251)).ToArray()},
        {"asset_archive/a00s035.fr",Encoding.UTF8.GetBytes("French voice data")},
        {"asset_archive/a00s044.ar",Encoding.UTF8.GetBytes("Arabic voice data")}
    };
    static ExpectedFile Row(string p)=>new ExpectedFile{Path=p,Size=data[p].Length,Hash=ImportEngine.Digest(new MemoryStream(data[p]))};
    static List<ExpectedFile> Core()=>data.Keys.Take(3).Select(Row).ToList();
    static List<LanguagePack> Languages()=>data.Keys.Skip(3).Select(p=>new LanguagePack{Language=p,Label=p,File=Row(p)}).ToList();
    static WifiInventoryRow[] Inventory()=>data.Select(p=>new WifiInventoryRow{Path=p.Key,Size=p.Value.Length}).ToArray();
    static string NewRoot(){var p=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);return p;}
    static WifiProtocol Protocol(string dir,Func<long> free=null)=>new WifiProtocol(dir,page,Core(),Languages(),free??(()=>Int64.MaxValue),null,null,null);
    static void Check(string name,Action action){action();passed++;Console.WriteLine("PASS "+name);}
    static void Assert(bool value,string message){if(!value)throw new Exception(message);}
    static string Request(WifiProtocol server,string method,string path,byte[] body=null,string code=null,string extra="",long? declared=null,CancellationToken ct=default(CancellationToken))
    {
        body=body??new byte[0];string head=method+" "+path+" HTTP/1.1\r\nHost: 127.0.0.1:8077\r\nContent-Length: "+(declared??body.Length)+"\r\n"+(code==null?"":"X-Transfer-Code: "+code+"\r\n")+extra+"\r\n";
        using(var input=new MemoryStream(Encoding.ASCII.GetBytes(head).Concat(body).ToArray()))
        using(var output=new MemoryStream()){server.Handle(input,output,ct).GetAwaiter().GetResult();return Encoding.UTF8.GetString(output.ToArray());}
    }
    static string Plan(WifiProtocol p,WifiInventoryRow[] inventory=null)=>Request(p,"POST","/folder",WifiProtocol.Json(inventory??Inventory()),p.PairCode);
    static void Ok(string r)=>Assert(r.StartsWith("HTTP/1.1 200 "),r);
    static void Bad(string r)=>Assert(r.StartsWith("HTTP/1.1 4"),"Expected rejection: "+r);
    static string Body(string response)=>response.Substring(response.IndexOf("\r\n\r\n")+4);
    static List<string> UploadPaths()=>data.Keys.ToList();
    static void Send(WifiProtocol p,int i)=>Ok(Request(p,"PUT","/file/"+i,data[UploadPaths()[i]],p.PairCode));
    static void Main(string[] args)
    {
        root=Path.Combine(Path.GetTempPath(),"SpiderWifiTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);page=File.ReadAllText(args[0]);
        if(args.Length>1 && args[1]=="serve")
        {
            var p=Protocol(NewRoot());var listener=new TcpListener(IPAddress.Loopback,8078);listener.Start();
            File.WriteAllText(args[2],p.PairCode);Console.WriteLine("Loopback test server ready at http://127.0.0.1:8078/");
            string fixture=args[3];Directory.CreateDirectory(fixture);foreach(var f in data){string dest=Path.Combine(fixture,f.Key.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(dest));File.WriteAllBytes(dest,f.Value);}
            while(true){var client=listener.AcceptTcpClient();Task.Run(async()=>{using(client)using(var stream=client.GetStream())await p.Handle(stream,stream,CancellationToken.None);});}
        }
        try
        {
            Check("short public page hides pairing secret",()=>{var p=Protocol(NewRoot());var r=Request(p,"GET","/");Ok(r);Assert(!Body(r).Contains(p.PairCode),"Code leaked in public HTML");Assert(r.Contains("frame-ancestors 'none'"),"Missing frame restriction");});
            Check("missing pairing code rejected",()=>Bad(Request(Protocol(NewRoot()),"POST","/folder",WifiProtocol.Json(Inventory()))));
            Check("wrong pairing code rejected",()=>{var p=Protocol(NewRoot());Bad(Request(p,"POST","/folder",WifiProtocol.Json(Inventory()),"wrong"));});
            Check("pairing guesses are rate limited",()=>{var p=Protocol(NewRoot());for(int i=0;i<5;i++)Bad(Request(p,"POST","/folder",WifiProtocol.Json(Inventory()),"wrong"));Bad(Plan(p));});
            Check("foreign Origin rejected",()=>{var p=Protocol(NewRoot());Bad(Request(p,"POST","/folder",WifiProtocol.Json(Inventory()),p.PairCode,"Origin: http://example.invalid\r\n"));});
            Check("same-origin browser accepted",()=>{var p=Protocol(NewRoot());Ok(Request(p,"POST","/folder",WifiProtocol.Json(Inventory()),p.PairCode,"Origin: http://127.0.0.1:8077\r\n"));});
            Check("duplicate framing headers rejected",()=>{var p=Protocol(NewRoot());Bad(Request(p,"POST","/folder",WifiProtocol.Json(Inventory()),p.PairCode,"Content-Length: 1\r\n"));});
            Check("chunked request rejected",()=>{var p=Protocol(NewRoot());Bad(Request(p,"POST","/folder",WifiProtocol.Json(Inventory()),p.PairCode,"Transfer-Encoding: chunked\r\n"));});
            Check("oversize folder list rejected",()=>{var p=Protocol(NewRoot());Bad(Request(p,"POST","/folder",new byte[0],p.PairCode,declared:5000000));});
            Check("malformed JSON rejected",()=>{var p=Protocol(NewRoot());Bad(Request(p,"POST","/folder",Encoding.UTF8.GetBytes("{"),p.PairCode));});
            Check("traversal inventory rejected",()=>{var p=Protocol(NewRoot());Bad(Plan(p,Inventory().Concat(new[]{new WifiInventoryRow{Path="../escape",Size=1}}).ToArray()));});
            Check("case-insensitive duplicate rejected",()=>{var p=Protocol(NewRoot());Bad(Plan(p,Inventory().Concat(new[]{new WifiInventoryRow{Path="SPIDER-MAN.EXE",Size=1}}).ToArray()));});
            Check("missing core file rejected",()=>Bad(Plan(Protocol(NewRoot()),Inventory().Skip(1).ToArray())));
            Check("missing voice packs rejected",()=>Bad(Plan(Protocol(NewRoot()),Inventory().Take(3).ToArray())));
            Check("French-only folder accepted",()=>Ok(Plan(Protocol(NewRoot()),Inventory().Take(4).ToArray())));
            Check("Arabic-only folder accepted",()=>Ok(Plan(Protocol(NewRoot()),Inventory().Where(f=>!f.Path.EndsWith(".fr")).ToArray())));
            Check("wrong-size file rejected",()=>{var rows=Inventory();rows[2].Size--;Bad(Plan(Protocol(NewRoot()),rows));});
            Check("insufficient storage leaves no directory",()=>{var dir=NewRoot();Bad(Plan(Protocol(dir,()=>1)));Assert(!Directory.EnumerateFileSystemEntries(dir).Any(),"Files created before storage check");});
            Check("archives cannot precede fingerprints",()=>{var p=Protocol(NewRoot());Ok(Plan(p));Bad(Request(p,"PUT","/file/2",data["asset_archive/world"],p.PairCode));});
            Check("wrong file length rejected",()=>{var p=Protocol(NewRoot());Ok(Plan(p));Bad(Request(p,"PUT","/file/0",new byte[1],p.PairCode));});
            Check("unsupported executable hash rejected without receipt",()=>{var dir=NewRoot();var p=Protocol(dir);Ok(Plan(p));Bad(Request(p,"PUT","/file/0",new byte[data["Spider-Man.exe"].Length],p.PairCode));Assert(!Directory.GetFiles(dir,"*.partial",SearchOption.AllDirectories).Any(),"Partial retained");Assert(!Directory.GetFiles(dir,"import-receipt.txt",SearchOption.AllDirectories).Any(),"False receipt");});
            Check("disconnect then retry retains verified files",()=>{var p=Protocol(NewRoot());Ok(Plan(p));Send(p,0);Bad(Request(p,"PUT","/file/1",new byte[1],p.PairCode,declared:data["asset_archive/toc"].Length));string plan=Plan(p);Ok(plan);Assert(plan.Contains("\"done\":true"),"Verified prefix not reusable");Send(p,1);});
            Check("cancel never commits receipt",()=>{var dir=NewRoot();var p=Protocol(dir);Ok(Plan(p));var c=new CancellationTokenSource();c.Cancel();Request(p,"PUT","/file/0",data["Spider-Man.exe"],p.PairCode,ct:c.Token);Assert(!Directory.GetFiles(dir,"import-receipt.txt",SearchOption.AllDirectories).Any(),"Receipt after cancel");});
            Check("free-space change checked before each file",()=>{long free=Int64.MaxValue;var p=Protocol(NewRoot(),()=>free);Ok(Plan(p));Send(p,0);free=1;Bad(Request(p,"PUT","/file/1",data["asset_archive/toc"],p.PairCode));});
            Check("complete direct folder import writes one verified copy",()=>{var dir=NewRoot();var p=Protocol(dir);Ok(Plan(p));for(int i=0;i<data.Count;i++)Send(p,i);Assert(!p.Busy,"Completed transfer remains busy");var receipt=Directory.GetFiles(dir,"import-receipt.txt",SearchOption.AllDirectories).Single();string target=Path.GetDirectoryName(receipt);foreach(var f in data)Assert(File.ReadAllBytes(Path.Combine(target,f.Key)).SequenceEqual(f.Value),"Mismatch "+f.Key);Assert(!Directory.GetFiles(dir,"*.zip",SearchOption.AllDirectories).Any(),"Unexpected ZIP");Assert(Directory.GetFiles(dir,"*",SearchOption.AllDirectories).Length==data.Count+1,"Extra payload copy");Ok(Plan(p));Send(p,0);});
            Check("ZIP remains available",()=>{var dir=NewRoot();var p=Protocol(dir);Ok(Request(p,"PUT","/game.zip",new byte[]{80,75,3,4},p.PairCode));Assert(File.Exists(Path.Combine(dir,"received-game.zip")),"ZIP missing");Bad(Request(p,"PUT","/game.zip",new byte[]{80,75,3,4},p.PairCode));});
            Check("ZIP cannot interrupt active folder",()=>{var p=Protocol(NewRoot());Ok(Plan(p));Bad(Request(p,"PUT","/game.zip",new byte[]{80,75,3,4},p.PairCode));});
            Check("unknown endpoints cannot write files",()=>{var p=Protocol(NewRoot());Bad(Request(p,"PUT","/anything",new byte[0],p.PairCode));});
            Console.WriteLine("PASS "+passed+" Wi-Fi tests");
        }
        finally{Directory.Delete(root,true);}
    }
}
