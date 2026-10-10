// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using AssetPreparation;
static class BatchTests
{
    static int checks;
    static void Check(bool b,string m) { if(!b)throw new Exception(m);checks++; }
    static void Reject(Action a) { try { a(); }catch(InvalidDataException) { checks++;return; }catch(OperationCanceledException) { checks++;return; }throw new Exception("Expected failure"); }
    static void Put(byte[] b,int p,uint v) { Buffer.BlockCopy(BitConverter.GetBytes(v),0,b,p,4); }
    static byte[] Toc(byte[] raw)
    {
        using(var s=new MemoryStream()) { var w=new BinaryWriter(s);w.Write(0x77af12afu);w.Write(raw.Length);w.Write((byte)0x78);w.Write((byte)0x9c);using(var z=new DeflateStream(s,CompressionLevel.Optimal,true))z.Write(raw,0,raw.Length);uint a=1,b=0;foreach(byte x in raw) { a=(a+x)%65521;b=(b+a)%65521; }uint c=b<<16|a;for(int i=3;i>=0;i--)w.Write((byte)(c>>(8*i)));return s.ToArray(); }
    }
    static int Main()
    {
        string root=Path.Combine(Path.GetTempPath(),"AssetBatchTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            string source=Path.Combine(root,"source"),recipes=Path.Combine(root,"recipes"),output=Path.Combine(root,"output");Directory.CreateDirectory(source);Directory.CreateDirectory(recipes);
            byte[] tex=new byte[128+163840];Put(tex,0,0x5c4580b9);Put(tex,36,0x44415431);Put(tex,44,92);Bytes.Put16(tex,48,1);Put(tex,52,0x4ede3593);Put(tex,56,48);Put(tex,60,44);Put(tex,8,163840);Put(tex,20,163840);Put(tex,84,163840);Bytes.Put16(tex,92,512);Bytes.Put16(tex,94,512);Bytes.Put16(tex,96,512);Bytes.Put16(tex,98,512);Bytes.Put16(tex,100,1);Bytes.Put16(tex,104,72);tex[114]=2;
            for(int i=128;i<tex.Length;i++)tex[i]=(byte)(i*7);byte[] converted=Mip256.Transform(tex);
            using(var w=new DsarWriter(Path.Combine(source,"g00s000"),tex.Length)) { w.Write(tex);w.Complete(); }
            byte[] raw=new byte[144];Put(raw,0,0x44415431);Put(raw,8,144);Bytes.Put16(raw,12,3);
            Put(raw,16,0xdcd720b5);Put(raw,20,52);Put(raw,24,8);Put(raw,28,0x65bcf461);Put(raw,32,60);Put(raw,36,12);Put(raw,40,0x398abff0);Put(raw,44,72);Put(raw,48,72);Put(raw,60,1);Put(raw,64,(uint)tex.Length);Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes("g00s000"),0,raw,80,7);
            byte[] toc=Toc(raw);string original=Path.Combine(source,"toc");File.WriteAllBytes(original,toc);string originalHash=Bytes.Hash(toc);
            byte[] target=new byte[216];Buffer.BlockCopy(raw,0,target,0,raw.Length);Put(target,8,216);Put(target,48,144);Put(target,52,1);Put(target,64,(uint)converted.Length);Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes("local_test.dsar"),0,target,152,15);
            File.WriteAllText(Path.Combine(recipes,"texture.tsv"),"ASSETGEN_MIP256_V1\t1\t"+converted.Length+"\t"+originalHash+"\n"+"g00s000\t0\t"+tex.Length+"\t"+Bytes.Hash(tex)+"\t0\t"+converted.Length+"\t"+Bytes.Hash(converted)+"\n");
            File.WriteAllText(Path.Combine(recipes,"index.tsv"),"ASSETGEN_TOC_V1\t"+originalHash+"\t"+Bytes.Hash(target)+"\nARCHIVE\tlocal_test.dsar\nASSET\t0\t1\t0\t"+converted.Length+"\n");
            string manifest="ASSETGEN_BATCH_V1\t"+originalHash+"\nARCHIVE\tlocal_test.dsar\ttexture.tsv\t"+converted.Length+"\t"+Bytes.Hash(converted)+"\nINDEX\ttoc\tindex.tsv\t216\t"+Bytes.Hash(target)+"\n";
            File.WriteAllText(Path.Combine(recipes,"batch.tsv"),manifest);
            var cancel=new CancellationTokenSource();Reject(()=>AssetBatch.Run(source,original,recipes,output,s=>cancel.Cancel(),cancel.Token));Check(!File.Exists(Path.Combine(output,"local_test.dsar")),"Cancelled output published");
            var done=AssetBatch.Run(source,original,recipes,output,null,CancellationToken.None);Check(done.Count==2,"Full batch did not complete");Check(Bytes.FileHash(original)==originalHash,"Original index changed");
            var before=File.GetLastWriteTimeUtc(Path.Combine(output,"local_test.dsar"));var again=AssetBatch.Run(source,original,recipes,output,null,CancellationToken.None);Check(again.Select(x=>x.Hash).SequenceEqual(done.Select(x=>x.Hash)),"Resume content changed");Check(File.GetLastWriteTimeUtc(Path.Combine(output,"local_test.dsar"))==before,"Resume rewrote complete archive");
            byte[] corrupt=File.ReadAllBytes(Path.Combine(output,"local_test.dsar"));corrupt[0]=0;File.WriteAllBytes(Path.Combine(output,"local_test.dsar"),corrupt);Reject(()=>AssetBatch.Run(source,original,recipes,output,null,CancellationToken.None));Check(File.ReadAllBytes(Path.Combine(output,"local_test.dsar")).SequenceEqual(corrupt),"Bad existing output overwritten");
            File.WriteAllText(Path.Combine(recipes,"batch.tsv"),manifest.Replace("local_test.dsar","../outside.dsar"));Reject(()=>AssetBatch.Run(source,original,recipes,Path.Combine(root,"other"),null,CancellationToken.None));Check(!File.Exists(Path.Combine(root,"outside.dsar")),"Path escaped");
            Console.WriteLine("PASS "+checks+" batch checks: cancel/resume, decoded verification, source preservation, path rejection");return 0;
        }catch(Exception e) { Console.Error.WriteLine(e);return 1; }
        finally { if(Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(root).StartsWith("AssetBatchTests-",StringComparison.Ordinal))Directory.Delete(root,true); }
    }
}
