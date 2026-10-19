// SPDX-License-Identifier: GPL-3.0-only
// Generates a small UI correction from the user's owned Map.gfx. No game UI
// payload, game shader or decompiled class is distributed by this transform.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace AssetPreparation
{
    public static class MapTravelPreparation
    {
        const ulong MapId=0xad387989ef4a359a;
        const string OriginalHash="e696772ed46fee14724f03840eea449563265d59403b91890b27dcda7b6434a7";
        public const string FixedHash="a3ba114296dcdcaa3d267f73cd6e7cbcd7112ad71073cccf2a45c1d8215e5dbd";
        const string ArchiveName="local_map_travel_v1.dsar";
        const int OriginalSize=300536,FixedSize=300686;
        // Authored AVM2 entry guard. It retains Twitter/offline rejection except
        // during the active fast-travel reveal tutorial, for a destination the
        // native map has explicitly marked travelable AND unlocked. The original
        // body after its early-return guard is copied from the owned asset.
        static readonly byte[] EntryGuard=Hex("d030d0d2688b0ad0d3688a0ad066930a12050000d02761890ad066ef09d066e308133d0000d066930a12360000d066950a122d0000d066a008241414230000d066800a20131a0000d066800a46960100120e0000d212090000d3110400001001000047");
        // Tutorial step two registers its completion callback before forwarding A.
        // Recheck cached native eligibility here if its first cursor update arrived
        // before the tutorial became active. The game's callback assignment stays.
        static readonly byte[] RegistrationTail=Hex("d066930a12460000d066950a123e0000d066ef09240414340000d066a0082414142a0000d066800a2013210000d066800a4696010012150000d0668b0a120d0000d0668a0a11050000d02661890a47");
        static byte[] Hex(string s) { var b=new byte[s.Length/2];for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(s.Substring(i*2,2),16);return b; }
        static byte[] Variable(int n) { var b=new List<byte>();while(n>=128){b.Add((byte)((n&127)|128));n>>=7;}b.Add((byte)n);return b.ToArray(); }
        static byte[] Replace(byte[] input,int start,int length,byte[] replacement)
        {
            byte[] result=new byte[input.Length-length+replacement.Length];Buffer.BlockCopy(input,0,result,0,start);Buffer.BlockCopy(replacement,0,result,start,replacement.Length);Buffer.BlockCopy(input,start+length,result,start+replacement.Length,input.Length-start-length);return result;
        }

        public static byte[] Transform(byte[] owned)
        {
            Bytes.Require(owned.Length==OriginalSize&&Bytes.Hash(owned)==OriginalHash,"Unsupported map UI; original files preserved");
            const int codeLengthAt=291185,codeStart=291187,oldCodeLength=482,oldGuard=27,tagLengthAt=131333,oldTagLength=166919;
            byte[] length=Variable(oldCodeLength+EntryGuard.Length-oldGuard);
            Bytes.Require(length.Length==codeStart-codeLengthAt&&owned[codeStart]==0xd0&&owned[codeStart+1]==0x30&&owned[codeStart+26]==0x47,"Map UI layout differs");
            byte[] result=Replace(owned,codeStart,oldGuard,EntryGuard);
            Buffer.BlockCopy(length,0,result,codeLengthAt,length.Length);
            const int registrationLengthAt=285899,registrationCode=285900,registrationOldLength=8;
            Bytes.Require(owned[registrationCode+registrationOldLength-1]==0x47,"Map tutorial callback layout differs");
            byte[] registrationLength=Variable(registrationOldLength-1+RegistrationTail.Length);
            Bytes.Require(registrationLength.Length==1,"Map tutorial callback exceeds bounded recipe");
            result=Replace(result,registrationCode+registrationOldLength-1,1,RegistrationTail);
            Buffer.BlockCopy(registrationLength,0,result,registrationLengthAt,1);
            Bytes.Require(result.Length==FixedSize,"Map correction size differs");
            Bytes.Put32(result,4,result.Length);Bytes.Put32(result,tagLengthAt,oldTagLength+result.Length-owned.Length);
            Bytes.Require(Bytes.Hash(result)==FixedHash,"Map UI correction failed verification");return result;
        }

        sealed class Index
        {
            public byte[] Raw; public int Ids,Offsets,Sizes,Archives,ArchiveBytes,ArchiveTableEntry,Map;
            public List<string> Names=new List<string>();
            public Index(string file)
            {
                using(var f=File.OpenRead(file)) {
                    byte[] h=Bytes.Read(f,10);int size=checked((int)Bytes.U32(h,4));
                    Bytes.Require(Bytes.U32(h,0)==0x77af12af&&size>16&&size<=64*1024*1024&&h[8]==0x78&&((h[8]<<8)+h[9])%31==0&&(h[9]&32)==0,"Unsupported map index");
                    using(var z=new DeflateStream(f,CompressionMode.Decompress)) { Raw=Bytes.Read(z,size);Bytes.Require(z.ReadByte()==-1,"Index expands beyond declared size"); }
                }
                Bytes.Require(Bytes.U32(Raw,0)==0x44415431&&Bytes.U32(Raw,8)==Raw.Length,"Invalid map index data");
                int count=Bytes.U16(Raw,12);Bytes.Require(count>0&&count<32,"Invalid index section count");
                var sections=new Dictionary<uint,Tuple<int,int,int>>();
                for(int i=0;i<count;i++) {
                    int p=16+12*i,off=checked((int)Bytes.U32(Raw,p+4)),size=checked((int)Bytes.U32(Raw,p+8));uint tag=Bytes.U32(Raw,p);
                    Bytes.Require(off>=16+12*count&&size>=0&&off<=Raw.Length&&size<=Raw.Length-off&&!sections.ContainsKey(tag),"Invalid index section");sections.Add(tag,Tuple.Create(off,size,p));
                }
                foreach(uint tag in new[]{0x506d7b8au,0xdcd720b5u,0x65bcf461u,0x398abff0u})Bytes.Require(sections.ContainsKey(tag),"Missing map index section");
                var ids=sections[0x506d7b8a];var offsets=sections[0xdcd720b5];var sizes=sections[0x65bcf461];var archives=sections[0x398abff0];
                Bytes.Require(ids.Item2%8==0&&offsets.Item2==ids.Item2&&sizes.Item2/12==ids.Item2/8&&sizes.Item2%12==0&&archives.Item2>0&&archives.Item2%72==0&&archives.Item1+archives.Item2==Raw.Length,"Unsupported map index layout");
                Ids=ids.Item1;Offsets=offsets.Item1;Sizes=sizes.Item1;Archives=archives.Item1;ArchiveBytes=archives.Item2;ArchiveTableEntry=archives.Item3;
                Map=-1;for(int i=0;i<ids.Item2/8;i++)if(BitConverter.ToUInt64(Raw,Ids+8*i)==MapId){Bytes.Require(Map<0,"Duplicate map UI identity");Map=i;}
                Bytes.Require(Map>=0&&Bytes.U32(Raw,Sizes+12*Map)==1&&Bytes.U32(Raw,Sizes+12*Map+8)==Map,"Missing map UI identity");
                for(int i=0;i<ArchiveBytes/72;i++) {
                    int p=Archives+72*i+8,n=0;while(n<64&&Raw[p+n]!=0)n++;
                    Bytes.Require(n>0&&n<64,"Invalid archive name");string name=Encoding.ASCII.GetString(Raw,p,n);
                    Bytes.Require(Path.GetFileName(name)==name&&!name.Contains(":")&&!name.Contains("..")&&!name.Contains("/")&&!name.Contains("\\"),"Unsafe archive name");Names.Add(name);
                }
            }
            public byte[] ReadMap(string root)
            {
                int ar=checked((int)Bytes.U32(Raw,Offsets+Map*8)),off=checked((int)Bytes.U32(Raw,Offsets+Map*8+4)),size=checked((int)Bytes.U32(Raw,Sizes+Map*12+4));
                Bytes.Require(ar>=0&&ar<Names.Count&&size>0&&size<=1024*1024,"Invalid map asset range");
                using(var archive=new DsarReader(Path.Combine(root,Names[ar])))return archive.Read(off,size);
            }
            public byte[] RouteToFix()
            {
                int ar=Names.IndexOf(ArchiveName);bool append=ar<0;if(append)ar=Names.Count;
                byte[] result=new byte[Raw.Length+(append?72:0)];Buffer.BlockCopy(Raw,0,result,0,Raw.Length);
                if(append) {
                    Buffer.BlockCopy(Raw,Archives,result,Raw.Length,8);byte[] name=Encoding.ASCII.GetBytes(ArchiveName);Buffer.BlockCopy(name,0,result,Raw.Length+8,name.Length);
                    Bytes.Put32(result,8,result.Length);Bytes.Put32(result,ArchiveTableEntry+8,ArchiveBytes+72);
                }
                Bytes.Put32(result,Offsets+Map*8,ar);Bytes.Put32(result,Offsets+Map*8+4,0);Bytes.Put32(result,Sizes+Map*12+4,FixedSize);return result;
            }
        }
        static void SaveIndex(string path,byte[] raw)
        {
            using(var f=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                var w=new BinaryWriter(f,Encoding.UTF8,true);w.Write(0x77af12afu);w.Write(raw.Length);w.Write((byte)0x78);w.Write((byte)0x9c);
                using(var z=new DeflateStream(f,CompressionLevel.Optimal,true))z.Write(raw,0,raw.Length);
                uint a=1,b=0;foreach(byte value in raw){a=(a+value)%65521;b=(b+a)%65521;}uint sum=b<<16|a;
                w.Write((byte)(sum>>24));w.Write((byte)(sum>>16));w.Write((byte)(sum>>8));w.Write((byte)sum);f.Flush(true);
            }
        }
        public static bool Ensure(string ownedAssets,string runtimeAssets,Action<string> progress,CancellationToken cancel,string ownedIndex=null)
        {
            ownedAssets=Path.GetFullPath(ownedAssets);runtimeAssets=Path.GetFullPath(runtimeAssets);
            ownedIndex=Path.GetFullPath(ownedIndex??Path.Combine(ownedAssets,"toc"));
            // Preparation can move the owned archives into the runtime directory.
            // In that layout use its separately preserved original index; never
            // derive the source map from a previously generated/diagnostic TOC.
            Bytes.Require(!String.Equals(ownedIndex,Path.Combine(runtimeAssets,"toc"),StringComparison.OrdinalIgnoreCase),"UI correction must not edit the imported source");
            cancel.ThrowIfCancellationRequested();string toc=Path.Combine(runtimeAssets,"toc");var current=new Index(toc);
            try { if(Bytes.Hash(current.ReadMap(runtimeAssets))==FixedHash)return false; }
            catch(FileNotFoundException) { /* repair only the generated UI asset */ }
            var source=new Index(ownedIndex);byte[] fixedMap=Transform(source.ReadMap(ownedAssets));
            progress?.Invoke("Preparing the fast-travel map correction…");
            string archive=Path.Combine(runtimeAssets,ArchiveName),tempArchive=archive+"."+Guid.NewGuid().ToString("N")+".partial",tempToc=toc+"."+Guid.NewGuid().ToString("N")+".partial";
            try {
                cancel.ThrowIfCancellationRequested();
                using(var writer=new DsarWriter(tempArchive,fixedMap.Length)){writer.Write(fixedMap);writer.Complete();}
                using(var reader=new DsarReader(tempArchive))Bytes.Require(Bytes.Hash(reader.Read(0,FixedSize))==FixedHash,"Generated map archive failed verification");
                byte[] routed=current.RouteToFix();SaveIndex(tempToc,routed);var check=new Index(tempToc);
                Bytes.Require(check.Raw.SequenceEqual(routed),"Generated map index failed verification");
                cancel.ThrowIfCancellationRequested();
                // The archive is durable before the TOC can refer to it. The old
                // TOC is preserved once, including all texture/model/pool fixes.
                if(File.Exists(archive))File.Replace(tempArchive,archive,null);else File.Move(tempArchive,archive);
                string backup=toc+".before-map-travel-v1";if(!File.Exists(backup))File.Copy(toc,backup,false);
                File.Replace(tempToc,toc,null);
                Bytes.Require(Bytes.Hash(new Index(toc).ReadMap(runtimeAssets))==FixedHash,"Installed map correction failed verification");
                return true;
            } finally {if(File.Exists(tempArchive))File.Delete(tempArchive);if(File.Exists(tempToc))File.Delete(tempToc);}
        }
    }
}
