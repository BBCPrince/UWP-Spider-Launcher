// SPDX-License-Identifier: GPL-3.0-only
// Rebuilds an index from the user's original index and numeric routing metadata.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace AssetPreparation
{
    public static class AssetIndex
    {
        public static void Run(string original,string recipe,string output)
        {
            original=Path.GetFullPath(original);output=Path.GetFullPath(output);
            string inputRoot=Path.GetDirectoryName(original).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            Bytes.Require(!output.StartsWith(inputRoot,StringComparison.OrdinalIgnoreCase),"Index output must be outside source folder");
            Bytes.Require(!File.Exists(output)&&!File.Exists(output+".partial"),"Index output exists");
            string[] lines=File.ReadAllLines(recipe);Bytes.Require(lines.Length>1&&lines.Length<=100001,"Invalid index recipe size");
            string[] header=lines[0].Split('\t');Bytes.Require(header.Length==3&&header[0]=="ASSETGEN_TOC_V1"&&Bytes.IsHash(header[1])&&Bytes.IsHash(header[2]),"Invalid index recipe");
            Bytes.Require(Bytes.FileHash(original)==header[1],"Wrong original index");
            byte[] raw = ReadDecoded(original);
            Bytes.Require(Bytes.U32(raw,0)==0x44415431&&Bytes.U32(raw,8)==raw.Length,"Invalid DAT1 index");
            int count=Bytes.U16(raw,12);Bytes.Require(count>0&&count<32,"Invalid section count");
            var sections=new Dictionary<uint,Tuple<int,int,int>>();
            for(int i=0;i<count;i++) {
                int p=16+12*i;uint tag=Bytes.U32(raw,p);int offset=checked((int)Bytes.U32(raw,p+4)),size=checked((int)Bytes.U32(raw,p+8));
                Bytes.Require(offset>=16+12*count&&size>=0&&offset<=raw.Length&&size<=raw.Length-offset&&!sections.ContainsKey(tag),"Invalid index section");sections.Add(tag,Tuple.Create(offset,size,p));
            }
            var archives=sections[0x398abff0];var offsets=sections[0xdcd720b5];var sizes=sections[0x65bcf461];
            Bytes.Require(archives.Item1+archives.Item2==raw.Length&&archives.Item2%72==0&&offsets.Item2%8==0&&sizes.Item2/12==offsets.Item2/8,"Unsupported index layout");
            int originalArchives=archives.Item2/72;var added=new List<string>();var changes=new List<int[]>();var seen=new HashSet<int>();
            foreach(string line in lines.Skip(1)) {
                string[] r=line.Split('\t');
                if(r[0]=="ARCHIVE") {
                    Bytes.Require(r.Length==2&&changes.Count==0&&r[1].StartsWith("local_",StringComparison.Ordinal)&&r[1].EndsWith(".dsar",StringComparison.Ordinal)&&r[1].Length<64&&r[1].All(c=>c>='a'&&c<='z'||c>='0'&&c<='9'||c=='_'||c=='.')&&!added.Contains(r[1]),"Invalid added archive");added.Add(r[1]);Bytes.Require(added.Count<=128,"Too many archives");
                } else {
                    Bytes.Require(r.Length==5&&r[0]=="ASSET","Invalid index operation");int i=int.Parse(r[1]),ar=int.Parse(r[2]),off=int.Parse(r[3]),size=int.Parse(r[4]);
                    Bytes.Require(i>=0&&i<offsets.Item2/8&&seen.Add(i)&&ar>=0&&ar<originalArchives+added.Count&&off>=0&&size>0&&size<=64*1024*1024,"Invalid asset routing");
                    Bytes.Require(Bytes.U32(raw,sizes.Item1+12*i)==1&&Bytes.U32(raw,sizes.Item1+12*i+8)==i,"Unexpected asset identity");changes.Add(new[]{i,ar,off,size});
                }
            }
            byte[] result=new byte[checked(raw.Length+added.Count*72)];Buffer.BlockCopy(raw,0,result,0,raw.Length);
            for(int i=0;i<added.Count;i++) { int p=raw.Length+72*i;Buffer.BlockCopy(raw,archives.Item1,result,p,8);byte[] name=Encoding.ASCII.GetBytes(added[i]);Buffer.BlockCopy(name,0,result,p+8,name.Length); }
            Bytes.Put32(result,8,result.Length);Bytes.Put32(result,archives.Item3+8,archives.Item2+72*added.Count);
            foreach(int[] r in changes) { Bytes.Put32(result,offsets.Item1+8*r[0],r[1]);Bytes.Put32(result,offsets.Item1+8*r[0]+4,r[2]);Bytes.Put32(result,sizes.Item1+12*r[0]+4,r[3]); }
            Bytes.Require(Bytes.Hash(result)==header[2],"Generated index differs from reference");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            WriteDecoded(output+".partial",result);
            File.Move(output+".partial",output);
        }

        static byte[] ReadDecoded(string path)
        {
            byte[] raw;
            using(var f=File.OpenRead(path)) {
                byte[] h=Bytes.Read(f,10);int size=checked((int)Bytes.U32(h,4));
                Bytes.Require(Bytes.U32(h,0)==0x77af12af&&size>16&&size<=64*1024*1024&&h[8]==0x78&&((h[8]<<8)+h[9])%31==0&&(h[9]&32)==0,"Invalid compressed index header");
                using(var inflate=new DeflateStream(f,CompressionMode.Decompress)) { raw=Bytes.Read(inflate,size);Bytes.Require(inflate.ReadByte()==-1,"Unexpected index expansion"); }
            }
            return raw;
        }

        static void WriteDecoded(string path, byte[] result)
        {
            using(var f=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                var writer=new BinaryWriter(f,Encoding.UTF8,true);writer.Write(0x77af12afu);writer.Write(result.Length);writer.Write((byte)0x78);writer.Write((byte)0x9c);
                using(var deflate=new DeflateStream(f,CompressionLevel.Optimal,true))deflate.Write(result,0,result.Length);
                uint a=1,b=0;foreach(byte value in result) { a=(a+value)%65521;b=(b+a)%65521; }uint adler=(b<<16)|a;
                writer.Write((byte)(adler>>24));writer.Write((byte)(adler>>16));writer.Write((byte)(adler>>8));writer.Write((byte)adler);f.Flush(true);
            }
        }

        // Both generated TOCs, after the fast-travel UI overlay. Pin the full
        // decoded hashes so a different index layout never receives these offsets.
        static readonly Dictionary<string,string> IronArmsIndexes = new Dictionary<string,string>
        {
            { "91ea10fb9b964b7904d2258230a6df62061f9472e20f8e7d269f51f609ed6c96", "f601d02527dcfe96c8a3d3a8e5eebf24d4a9fbf57ab9efe2c962df5eb90083d1" },
            { "624dd2d58ab76da0e689d42edf29ccae898f6ba0b184dc004c9465d9c46ba368", "6bac94d9df2f175c06d2717a09f91442901923e2d048689eef6d3170370416a7" },
        };

        public static bool RestoreIronArms(string runtimeAssets, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            string toc = Path.Combine(runtimeAssets, "toc");
            using (var operation = new FileStream(toc + ".iron-arms.lock", FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose))
            {
                byte[] raw = ReadDecoded(toc);
                if (!IronArmsIndexes.TryGetValue(Bytes.Hash(raw), out string expected))
                    return false;

                const int modelOffset = 186275964, modelSize = 2282916;
                using (var archive = new DsarReader(Path.Combine(runtimeAssets, "g00s000")))
                    Bytes.Require(Bytes.Hash(archive.Read(modelOffset, modelSize)) ==
                        "dcce86fd722f28f56c701f48faed87014821896b6343d82e4bd676b45541eb63",
                        "Original Iron Arms model failed verification");

                // Asset 0xa3b37bb30e0ab835 uses the authored geometry for its
                // animations. Restore its original archive, offset and length.
                Bytes.Put32(raw, 16482768, 0);
                Bytes.Put32(raw, 16482772, modelOffset);
                Bytes.Put32(raw, 7697516, modelSize);
                Bytes.Require(Bytes.Hash(raw) == expected, "Iron Arms index repair failed verification");

                cancel.ThrowIfCancellationRequested();
                string temporary = toc + "." + Guid.NewGuid().ToString("N") + ".partial";
                try
                {
                    WriteDecoded(temporary, raw);
                    Bytes.Require(Bytes.Hash(ReadDecoded(temporary)) == expected, "Iron Arms index write failed verification");
                    cancel.ThrowIfCancellationRequested();
                    string backup = toc + ".before-iron-arms-v1";
                    File.Replace(temporary, toc, File.Exists(backup) ? null : backup);
                    return true;
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
            }
        }
    }
}
