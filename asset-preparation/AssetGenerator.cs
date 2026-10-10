// SPDX-License-Identifier: GPL-3.0-only
// Adapted from the local GPL-3.0 texture archive/mip research scripts.
// Format attribution: Tkachov/ALERT, bc90ed9b53fbcaa3b7acf94b36f00e77eab0dacb.
// No game payload bytes are embedded. The caller supplies owned input files.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using K4os.Compression.LZ4;

namespace AssetPreparation
{
    public static class DiskSpace
    {
#if WINDOWS_UWP
        [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true,ExactSpelling=true)]
        [return:System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        static extern bool GetDiskFreeSpaceExW(string directory,out ulong available,out ulong total,out ulong free);
#endif
        public static long Available(string directory)
        {
#if WINDOWS_UWP
            ulong available,total,free;
            if(!GetDiskFreeSpaceExW(directory,out available,out total,out free))throw new IOException("Cannot query preparation folder free space (Windows error "+System.Runtime.InteropServices.Marshal.GetLastWin32Error()+")");
            return checked((long)available);
#else
            return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(directory))).AvailableFreeSpace;
#endif
        }
    }
    public static class Bytes
    {
        public static void Require(bool value,string message) { if(!value)throw new InvalidDataException(message); }
        public static uint U32(byte[] b,int o) { return BitConverter.ToUInt32(b,o); }
        public static ushort U16(byte[] b,int o) { return BitConverter.ToUInt16(b,o); }
        public static void Put32(byte[] b,int o,int n) { Buffer.BlockCopy(BitConverter.GetBytes(n),0,b,o,4); }
        public static void Put16(byte[] b,int o,int n) { Require(n>=0&&n<=65535,"Dimension overflow");Buffer.BlockCopy(BitConverter.GetBytes((ushort)n),0,b,o,2); }
        public static byte[] Read(Stream s,int length)
        {
            Require(length>=0&&length<=64*1024*1024,"Read exceeds asset limit");
            byte[] b=new byte[length];int p=0,n;
            while(p<length) { n=s.Read(b,p,length-p);if(n==0)throw new EndOfStreamException();p+=n; }
            return b;
        }
        public static string Hash(byte[] b) { using(var h=SHA256.Create())return Hex(h.ComputeHash(b)); }
        public static string FileHash(string p) { using(var f=File.OpenRead(p))using(var h=SHA256.Create())return Hex(h.ComputeHash(f)); }
        static string Hex(byte[] b) { return BitConverter.ToString(b).Replace("-","").ToLowerInvariant(); }
        public static bool IsHash(string s) { return s.Length==64&&s.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f'); }
    }

    // One cached block, at most 4 MiB. Archive and block-table bounds are checked
    // before allocations, seeking, or decompression.
    public sealed class DsarReader : IDisposable
    {
        struct Block { public long Start,Offset;public int Raw,Packed; }
        FileStream stream; Block[] blocks;long total;int cached=-1;byte[] cache;
        public long Length { get { return total; } }
        public DsarReader(string path)
        {
            stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            try {
                byte[] h=Bytes.Read(stream,32);
                Bytes.Require(Bytes.U32(h,0)==0x52415344&&Bytes.U32(h,4)==0x10003,"Unsupported DSAR header");
                uint n=Bytes.U32(h,8),end=Bytes.U32(h,12);ulong size=BitConverter.ToUInt64(h,16);
                Bytes.Require(n>0&&n<=1000000&&end>=32L+32L*n&&end<=stream.Length&&size<=long.MaxValue,"Invalid DSAR table");
                total=(long)size;blocks=new Block[(int)n];long expected=0;
                for(int i=0;i<blocks.Length;i++) {
                    byte[] row=Bytes.Read(stream,32);ulong start=BitConverter.ToUInt64(row,0),offset=BitConverter.ToUInt64(row,8);
                    uint raw=Bytes.U32(row,16),packed=Bytes.U32(row,20);
                    Bytes.Require(start==(ulong)expected&&raw>0&&raw<=4*1024*1024&&packed>0&&packed<=8*1024*1024&&row[24]==3,"Invalid DSAR block");
                    Bytes.Require(offset>=end&&offset<=(ulong)stream.Length&&packed<=(ulong)stream.Length-offset,"DSAR data exceeds file");
                    blocks[i]=new Block {Start=expected,Offset=(long)offset,Raw=(int)raw,Packed=(int)packed};expected=checked(expected+raw);
                }
                Bytes.Require(expected==total,"DSAR length mismatch");
            } catch { stream.Dispose();throw; }
        }
        public byte[] Read(long offset,int length)
        {
            Bytes.Require(offset>=0&&length>=0&&length<=64*1024*1024&&offset<=total&&length<=total-offset,"Asset range exceeds archive");
            byte[] result=new byte[length];int done=0;
            while(done<length) {
                int lo=0,hi=blocks.Length-1;
                while(lo<hi) { int mid=lo+(hi-lo+1)/2;if(blocks[mid].Start<=offset)lo=mid;else hi=mid-1; }
                Block b=blocks[lo];
                if(cached!=lo) {
                    stream.Position=b.Offset;byte[] packed=Bytes.Read(stream,b.Packed);cache=new byte[b.Raw];
                    Bytes.Require(LZ4Codec.Decode(packed,0,packed.Length,cache,0,cache.Length)==b.Raw,"Invalid LZ4 payload");cached=lo;
                }
                int within=checked((int)(offset-b.Start)),take=Math.Min(length-done,b.Raw-within);
                Bytes.Require(take>0,"Invalid archive range");Buffer.BlockCopy(cache,within,result,done,take);offset+=take;done+=take;
            }
            return result;
        }
        public void Dispose() { if(stream!=null)stream.Dispose();cache=null; }
    }

    public sealed class DsarWriter : IDisposable
    {
        const int BlockSize=262144;
        readonly FileStream stream; readonly long total; readonly int count;
        readonly byte[] pending=new byte[BlockSize],packed=new byte[LZ4Codec.MaximumOutputSize(BlockSize)];
        int used,done;bool completed;
        public long Position { get;private set; }
        public DsarWriter(string path,long length)
        {
            Bytes.Require(length>0&&length<uint.MaxValue,"Unsupported output size");total=length;count=checked((int)((length+BlockSize-1)/BlockSize));
            stream=new FileStream(path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);
            var w=new BinaryWriter(stream,Encoding.UTF8,true);w.Write(0x52415344u);w.Write(0x10003u);w.Write(count);w.Write(32+32*count);w.Write(total);w.Write(0L);
            stream.SetLength(32+32L*count);stream.Position=stream.Length;
        }
        public void Write(byte[] data)
        {
            Bytes.Require(!completed&&Position+data.Length<=total,"Output exceeds planned size");int p=0;
            while(p<data.Length) { int n=Math.Min(BlockSize-used,data.Length-p);Buffer.BlockCopy(data,p,pending,used,n);p+=n;used+=n;Position+=n;if(used==BlockSize)FlushBlock(); }
        }
        void FlushBlock()
        {
            int n=LZ4Codec.Encode(pending,0,used,packed,0,packed.Length,LZ4Level.L00_FAST);
            Bytes.Require(n>0,"LZ4 encoder failed");long off=stream.Position;stream.Write(packed,0,n);long end=stream.Position;
            stream.Position=32+32L*done;var w=new BinaryWriter(stream,Encoding.UTF8,true);w.Write((long)done*BlockSize);w.Write(off);w.Write(used);w.Write(n);w.Write((byte)3);w.Write(new byte[7]);
            stream.Position=end;done++;used=0;
        }
        public void Complete()
        {
            Bytes.Require(Position==total&&!completed,"Incomplete archive");if(used!=0)FlushBlock();Bytes.Require(done==count,"Incomplete block table");stream.Flush(true);completed=true;
        }
        public void Dispose() { stream.Dispose(); }
    }

    public static class Mip256
    {
        static int BlockBytes(int f)
        {
            switch(f) { case 71:case 72:case 80:return 8;case 77:case 78:case 83:case 95:case 98:case 99:return 16;default:throw new InvalidDataException("Unsupported texture format"); }
        }
        static bool Pow2(int n) { return n>0&&(n&(n-1))==0; }
        static int Level(int w,int h,int bytes) { return checked(Math.Max(1,(w+3)/4)*Math.Max(1,(h+3)/4)*bytes); }
        public static byte[] Transform(byte[] source)
        {
            Bytes.Require(source.Length>=128&&Bytes.U32(source,0)==0x5c4580b9&&Bytes.U32(source,36)==0x44415431,"Unsupported texture layout");
            Bytes.Require(Bytes.U32(source,44)==92&&Bytes.U16(source,48)==1&&Bytes.U32(source,52)==0x4ede3593&&Bytes.U32(source,56)==48&&Bytes.U32(source,60)==44,"Unsupported texture header");
            int sd=checked((int)Bytes.U32(source,84)),hd=checked((int)Bytes.U32(source,88));
            int w=Bytes.U16(source,96),h=Bytes.U16(source,98),mips=source[114],hdmips=source[116],fmt=Bytes.U16(source,104);
            Bytes.Require(Bytes.U16(source,100)==1&&Pow2(w)&&Pow2(h)&&mips>=2&&mips<=16&&source.Length==128L+sd,"Unsupported texture dimensions");
            Bytes.Require(Bytes.U32(source,8)==sd&&Bytes.U32(source,20)==sd,"Texture length fields differ");
            int bytes=BlockBytes(fmt),sum=0,cut=0,drop=0,nw=w,nh=h;
            for(int i=0;i<mips;i++) { int lw=Math.Max(1,w>>i),lh=Math.Max(1,h>>i),n=Level(lw,lh,bytes);sum=checked(sum+n);if(Math.Max(lw,lh)>256) { cut=checked(cut+n);drop++; } }
            Bytes.Require(sum==sd&&drop<mips,"Invalid mip chain");nw=Math.Max(1,w>>drop);nh=Math.Max(1,h>>drop);
            if(drop==0) {
                Bytes.Require(hdmips>0&&hdmips<=16&&hd>0,"Small texture has no HD chain to close");
                int hw=Bytes.U16(source,92),hh=Bytes.U16(source,94),hs=0;
                Bytes.Require(Math.Max(1,hw>>hdmips)==w&&Math.Max(1,hh>>hdmips)==h,"HD dimensions mismatch");
                for(int i=0;i<hdmips;i++)hs=checked(hs+Level(Math.Max(1,hw>>i),Math.Max(1,hh>>i),bytes));
                Bytes.Require(hs==hd,"HD byte count mismatch");
            }
            byte[] result=new byte[128+sd-cut];Buffer.BlockCopy(source,0,result,0,128);Buffer.BlockCopy(source,128+cut,result,128,sd-cut);
            Bytes.Put32(result,8,sd-cut);Bytes.Put32(result,20,sd-cut);Bytes.Put32(result,84,sd-cut);Bytes.Put32(result,88,0);
            Bytes.Put16(result,92,nw);Bytes.Put16(result,94,nh);Bytes.Put16(result,96,nw);Bytes.Put16(result,98,nh);result[114]=(byte)(mips-drop);result[116]=0;
            return result;
        }
    }

    public static class RestoreTexture
    {
        static int BlockBytes(int f) { switch(f) { case 71:case 72:case 80:return 8;case 77:case 78:case 83:case 95:case 98:case 99:return 16;default:throw new InvalidDataException("Unsupported texture format"); } }
        static int Level(int w,int h,int b) { return checked(Math.Max(1,(w+3)/4)*Math.Max(1,(h+3)/4)*b); }
        public static byte[] Transform(byte[] source,byte[] hd,int maximum)
        {
            Bytes.Require(maximum>=128&&maximum<=4096&&(maximum&(maximum-1))==0,"Unsupported maximum dimension");
            Bytes.Require(source.Length>=128&&Bytes.U32(source,0)==0x5c4580b9&&Bytes.U32(source,36)==0x44415431&&Bytes.U32(source,44)==92,"Unsupported texture layout");
            Bytes.Require(Bytes.U16(source,48)==1&&Bytes.U32(source,52)==0x4ede3593&&Bytes.U32(source,56)==48&&Bytes.U32(source,60)==44&&Bytes.U16(source,100)==1,"Unsupported texture header");
            int sd=checked((int)Bytes.U32(source,84)),sw=Bytes.U16(source,96),sh=Bytes.U16(source,98),sm=source[114],b=BlockBytes(Bytes.U16(source,104));
            Bytes.Require(sw>0&&sh>0&&(sw&(sw-1))==0&&(sh&(sh-1))==0&&sm>=1&&sm<=16&&source.Length==128L+sd&&Bytes.U32(source,8)==sd&&Bytes.U32(source,20)==sd,"Bad source dimensions");
            int ss=0;for(int i=0;i<sm;i++)ss=checked(ss+Level(Math.Max(1,sw>>i),Math.Max(1,sh>>i),b));Bytes.Require(ss==sd,"Invalid base mip chain");
            int w=sw,h=sh,mips=sm,hdLength=0;
            if(hd!=null) {
                int hm=source[116];w=Bytes.U16(source,92);h=Bytes.U16(source,94);hdLength=hd.Length;
                Bytes.Require(hm>0&&hm<=16&&w>0&&h>0&&Bytes.U32(source,88)==hdLength&&Math.Max(1,w>>hm)==sw&&Math.Max(1,h>>hm)==sh,"Invalid HD chain");
                int hs=0;for(int i=0;i<hm;i++)hs=checked(hs+Level(Math.Max(1,w>>i),Math.Max(1,h>>i),b));Bytes.Require(hs==hdLength,"Invalid HD length");mips+=hm;
            }
            int skip=0,cut=0;while(Math.Max(Math.Max(1,w>>skip),Math.Max(1,h>>skip))>maximum) { Bytes.Require(skip<mips,"No requested mip");cut=checked(cut+Level(Math.Max(1,w>>skip),Math.Max(1,h>>skip),b));skip++; }
            int pixels=checked(hdLength+sd-cut);Bytes.Require(pixels>0&&pixels<=64*1024*1024-128&&mips-skip<=255,"Invalid output length");
            byte[] output=new byte[128+pixels];Buffer.BlockCopy(source,0,output,0,128);int copied=0;
            if(cut<hdLength) { copied=hdLength-cut;Buffer.BlockCopy(hd,cut,output,128,copied); }
            int sdCut=Math.Max(0,cut-hdLength);Buffer.BlockCopy(source,128+sdCut,output,128+copied,sd-sdCut);
            int nw=Math.Max(1,w>>skip),nh=Math.Max(1,h>>skip);
            Bytes.Put32(output,8,pixels);Bytes.Put32(output,20,pixels);Bytes.Put32(output,84,pixels);Bytes.Put32(output,88,0);
            Bytes.Put16(output,92,nw);Bytes.Put16(output,94,nh);Bytes.Put16(output,96,nw);Bytes.Put16(output,98,nh);output[114]=(byte)(mips-skip);output[116]=0;
            return output;
        }
    }

    public static class Generate
    {
        static string SourceName(string name)
        {
            Bytes.Require(name.Length>0&&name.Length<=64&&name==Path.GetFileName(name)&&name.All(c=>c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='_'||c=='.'||c=='-')&&name!="."&&name!="..","Unsafe source archive name");return name;
        }
        public static void Run(string inputRoot,string recipe,string output,CancellationToken cancel,Action<int,int> progress,string originalIndex=null)
        {
            inputRoot=Path.GetFullPath(inputRoot).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;output=Path.GetFullPath(output);
            Bytes.Require(!output.StartsWith(inputRoot,StringComparison.OrdinalIgnoreCase),"Output must be outside original game files");
            Bytes.Require(!File.Exists(output)&&!File.Exists(output+".partial"),"Output already exists; no overwrite allowed");
            using(var rows=new StreamReader(recipe)) {
                string[] header=(rows.ReadLine()??"").Split('\t');
                Bytes.Require(header.Length==4&&(header[0]=="ASSETGEN_MIP256_V1"||header[0]=="ASSETGEN_TEXTURE_V2"||header[0]=="ASSETGEN_MODELLOD3_V1"||header[0]=="ASSETGEN_ZONE_V1"||header[0]=="ASSETGEN_DISTANT_V1")&&Bytes.IsHash(header[3]),"Unknown recipe");bool restore=header[0]=="ASSETGEN_TEXTURE_V2",model=header[0]=="ASSETGEN_MODELLOD3_V1",zone=header[0]=="ASSETGEN_ZONE_V1",distant=header[0]=="ASSETGEN_DISTANT_V1";
                int count=int.Parse(header[1],CultureInfo.InvariantCulture);long total=long.Parse(header[2],CultureInfo.InvariantCulture);
                Bytes.Require(count>0&&count<=100000&&total>0&&total<uint.MaxValue,"Recipe limit exceeded");
                Bytes.Require(Bytes.FileHash(originalIndex??Path.Combine(inputRoot,"toc"))==header[3],"Wrong original game version");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                Bytes.Require(DiskSpace.Available(Path.GetDirectoryName(output))>total+128L*1024*1024,"Insufficient output disk space");
                string active=null;DsarReader reader=null;int completed=0;
                try {
                    using(var writer=new DsarWriter(output+".partial",total)) {
                        string line;
                        while((line=rows.ReadLine())!=null) {
                            cancel.ThrowIfCancellationRequested();string[] r=line.Split('\t');Bytes.Require(r.Length==(restore?12:zone?10:7),"Malformed recipe row");
                            string name=SourceName(r[0]);
                            long off=long.Parse(r[1],CultureInfo.InvariantCulture),outoff=long.Parse(r[4],CultureInfo.InvariantCulture);int size=int.Parse(r[2],CultureInfo.InvariantCulture),outsize=int.Parse(r[5],CultureInfo.InvariantCulture);
                            Bytes.Require(Bytes.IsHash(r[3])&&Bytes.IsHash(r[6])&&outoff==(writer.Position+15L&~15L),"Invalid recipe metadata");
                            if(active!=name) { if(reader!=null)reader.Dispose();reader=new DsarReader(Path.Combine(inputRoot,name));active=name; }
                            byte[] original=reader.Read(off,size);Bytes.Require(Bytes.Hash(original)==r[3],"Original asset hash mismatch at row "+completed);
                            byte[] changed;
                            if(restore) {
                                byte[] hd=null;
                                if(r[8]!="-") {
                                    Bytes.Require(Bytes.IsHash(r[11]),"Invalid HD hash");
                                    using(var hreader=new DsarReader(Path.Combine(inputRoot,SourceName(r[8]))))hd=hreader.Read(long.Parse(r[9],CultureInfo.InvariantCulture),int.Parse(r[10],CultureInfo.InvariantCulture));
                                    Bytes.Require(Bytes.Hash(hd)==r[11],"HD asset hash mismatch");
                                }
                                changed=RestoreTexture.Transform(original,hd,int.Parse(r[7],CultureInfo.InvariantCulture));
                            } else if(zone||distant)changed=ResampledTexture.Transform(original,zone,zone?int.Parse(r[7],CultureInfo.InvariantCulture):0,zone?int.Parse(r[8],CultureInfo.InvariantCulture):84,zone?int.Parse(r[9],CultureInfo.InvariantCulture):128);
                            else changed=model?ModelLod.Transform(original):Mip256.Transform(original);
                            Bytes.Require(changed.Length==outsize&&Bytes.Hash(changed)==r[6],"Generated asset mismatch at row "+completed);
                            writer.Write(new byte[checked((int)(outoff-writer.Position))]);writer.Write(changed);completed++;
                            Bytes.Require(completed<=count,"Extra recipe rows");if(completed%500==0||completed==count)progress(completed,count);
                        }
                        Bytes.Require(completed==count,"Missing recipe rows");writer.Complete();
                    }
                    cancel.ThrowIfCancellationRequested();File.Move(output+".partial",output);
                } finally { if(reader!=null)reader.Dispose(); }
            }
        }
    }

#if !WINDOWS_UWP
    static class Program
    {
        static int Main(string[] args)
        {
            if(args.Length==5&&args[0]=="--batch") {
                var batchWatch=Stopwatch.StartNew();
                try { var files=AssetBatch.Run(args[1],Path.Combine(args[1],"toc"),args[2],args[3],Console.WriteLine,CancellationToken.None);File.WriteAllLines(args[4],files.Select(f=>f.Name+"\t"+f.Size+"\t"+f.Hash));Console.WriteLine("SUCCESS batch files="+files.Count+" elapsed_s="+batchWatch.Elapsed.TotalSeconds.ToString("F2",CultureInfo.InvariantCulture)+" peak_working_set="+Process.GetCurrentProcess().PeakWorkingSet64+" peak_paged_memory="+Process.GetCurrentProcess().PeakPagedMemorySize64);return 0; }
                catch(Exception e) { Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1; }
            }
            if(args.Length==4&&args[0]=="--toc") {
                try { AssetIndex.Run(args[1],args[2],args[3]);Console.WriteLine("SUCCESS index decoded bytes match reference; compressed_sha256="+Bytes.FileHash(args[3]));return 0; }
                catch(Exception e) { Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1; }
            }
            if(args.Length!=3) { Console.Error.WriteLine("Usage: AssetGenerator <original asset_archive> <recipe.tsv> <new output.dsar>");return 2; }
            var stop=new CancellationTokenSource();Console.CancelKeyPress+=(s,e)=>{e.Cancel=true;stop.Cancel();};var watch=Stopwatch.StartNew();
            try {
                Generate.Run(args[0],args[1],args[2],stop.Token,(done,total)=>Console.WriteLine("assets="+done+"/"+total+" elapsed_s="+watch.Elapsed.TotalSeconds.ToString("F1",CultureInfo.InvariantCulture)));
                var p=Process.GetCurrentProcess();Console.WriteLine("SUCCESS elapsed_s="+watch.Elapsed.TotalSeconds.ToString("F2",CultureInfo.InvariantCulture)+" peak_working_set="+p.PeakWorkingSet64+" peak_paged_memory="+p.PeakPagedMemorySize64+" output_sha256="+Bytes.FileHash(args[2]));return 0;
            } catch(Exception e) { Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1; }
        }
    }
#endif
}
