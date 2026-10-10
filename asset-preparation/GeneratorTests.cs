// SPDX-License-Identifier: GPL-3.0-only
// Synthetic-only integrity and cancellation checks; no game or console needed.
using System;
using System.IO;
using System.Linq;
using System.Threading;
using AssetPreparation;
static class GeneratorTests
{
    static int tests;
    static void Check(bool b,string why) { if(!b)throw new Exception(why);tests++; }
    static void Reject(Action action,string why) { try { action(); }catch(InvalidDataException) { tests++;return; }catch(IOException) { tests++;return; }catch(OperationCanceledException) { tests++;return; }throw new Exception("Accepted "+why); }
    static void Put(byte[] b,int p,uint n) { Buffer.BlockCopy(BitConverter.GetBytes(n),0,b,p,4); }
    static int Main()
    {
        string root=Path.Combine(Path.GetTempPath(),"AssetGeneratorTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            byte[] data=new byte[600003];new Random(193).NextBytes(data);string file=Path.Combine(root,"test.dsar");
            using(var w=new DsarWriter(file,data.Length)) { w.Write(data.Take(73).ToArray());w.Write(data.Skip(73).ToArray());w.Complete(); }
            using(var r=new DsarReader(file)) { Check(r.Read(0,data.Length).SequenceEqual(data),"Archive roundtrip");Check(r.Read(262100,200).SequenceEqual(data.Skip(262100).Take(200)),"Block crossing");Reject(()=>r.Read(-1,4),"negative range");Reject(()=>r.Read(data.Length,1),"past end"); }
            AssetBatch.Verify(file,false,data.Length,Bytes.Hash(data),CancellationToken.None);tests++;
            Reject(()=>AssetBatch.Verify(file,false,data.Length,new string('0',64),CancellationToken.None),"wrong decoded hash");
            Reject(()=>AssetBatch.Verify(file,false,data.Length,Bytes.Hash(data),new CancellationToken(true)),"cancelled verify");
            byte[] damaged=File.ReadAllBytes(file);damaged[32+24]=7;string bad=Path.Combine(root,"bad.dsar");File.WriteAllBytes(bad,damaged);Reject(()=> { using(var r=new DsarReader(bad)){} },"unknown compression");
            File.WriteAllBytes(bad,damaged.Take(20).ToArray());Reject(()=> { using(var r=new DsarReader(bad)){} },"truncated header");
            damaged=File.ReadAllBytes(file);Put(damaged,32+16,0xffffffff);File.WriteAllBytes(bad,damaged);Reject(()=> { using(var r=new DsarReader(bad)){} },"oversized block");
            Reject(()=> { using(var w=new DsarWriter(file,10)){} },"existing output");Check(Bytes.FileHash(file)==Bytes.Hash(File.ReadAllBytes(file)),"Existing file intact");
            Reject(()=>ModelLod.Transform(new byte[128]),"bad model");Reject(()=>Mip256.Transform(new byte[128]),"bad texture");Reject(()=>RestoreTexture.Transform(new byte[128],null,512),"bad restoration");Reject(()=>ResampledTexture.Transform(new byte[128],true,-1,1000,0),"bad embedded texture");
            string source=Path.Combine(root,"source");Directory.CreateDirectory(source);File.WriteAllText(Path.Combine(source,"toc"),"synthetic index");string recipe=Path.Combine(root,"recipe.tsv");
            File.WriteAllText(recipe,"ASSETGEN_MIP256_V1\t1\t100\t"+new string('0',64)+"\n");
            Reject(()=>Generate.Run(source,recipe,Path.Combine(source,"x.dsar"),CancellationToken.None,(a,b)=>{}),"write into source");
            Reject(()=>Generate.Run(source,recipe,Path.Combine(root,"x.dsar"),CancellationToken.None,(a,b)=>{}),"wrong game version");Check(!File.Exists(Path.Combine(root,"x.dsar")),"No output published on failure");
            Console.WriteLine("PASS "+tests+" synthetic checks; game files untouched");return 0;
        }catch(Exception e) { Console.Error.WriteLine(e);return 1; }
        finally { if(Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(root).StartsWith("AssetGeneratorTests-",StringComparison.Ordinal))Directory.Delete(root,true); }
    }
}
