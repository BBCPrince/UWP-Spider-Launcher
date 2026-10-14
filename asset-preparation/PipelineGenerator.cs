// SPDX-License-Identifier: GPL-3.0-only
// Reconstructs preparation descriptors from the user's original files.
// Packaged metadata contains hashes, ranges and D3D state; no shader bytecode.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace AssetPreparation
{
    public static class PipelineGenerator
    {
        public const string MapHash="bd06bb91762917ab0418833d4c0656109cce09d80920a5ca301e3ea404d7db7a";
        public const string CatalogHash="40787f3d38a66bae7ae9435ff1b63dd4fc6d74a308d3e45954f89b1cd480084b";
        public const string NewMapHash="07cff66e69548eb0a0471bd0ba3121c247cf3d4c36841cf19b8d3e7f99dae8d7";
        public const int Count=8435;
        static uint Number(string s) { return uint.Parse(s,NumberStyles.None,CultureInfo.InvariantCulture); }
        static byte[] Hex(string s,int maximum)
        {
            Bytes.Require(s.Length%2==0&&s.Length<=maximum*2&&s.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f'),"Invalid pipeline metadata bytes");
            byte[] b=new byte[s.Length/2];for(int i=0;i<b.Length;i++)b[i]=byte.Parse(s.Substring(2*i,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture);return b;
        }
        static string Name(string s)
        {
            Bytes.Require(s.Length>0&&s.Length<=100&&s!="."&&s!=".."&&s.All(c=>c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='.'||c=='_'||c=='-'),"Unsafe shader source filename");return s;
        }
        static void NoLinks(string path)
        {
            for(string p=Path.GetFullPath(path);p!=null;p=Path.GetDirectoryName(p))
                if(File.Exists(p)||Directory.Exists(p))Bytes.Require((File.GetAttributes(p)&FileAttributes.ReparsePoint)==0,"Pipeline paths cannot use links");
        }
        static bool Inside(string path,string root)
        {
            root=Path.GetFullPath(root).TrimEnd('\\','/');path=Path.GetFullPath(path);
            return path.Equals(root,StringComparison.OrdinalIgnoreCase)||path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
        }
        internal static byte[] Root(string[] row)
        {
            Bytes.Require(row.Length==5&&Bytes.IsHash(row[1]),"Invalid root settings");
            byte[] checksum=Hex(row[2],16);Bytes.Require(checksum.Length==16,"Invalid root checksum");
            uint flags=Number(row[3]);string[] tables=row[4].Split(';');
            Bytes.Require(tables.Length>0&&tables.Length<=32&&flags<=255,"Unsupported root settings");
            int count=tables.Length,bodyLength=24+40*count;
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream))
            {
                // Standard single RTS0 container. Only descriptor tables are used
                // by these four verified roots; it contains no shader program.
                w.Write(Encoding.ASCII.GetBytes("DXBC"));w.Write(checksum);w.Write(1);w.Write(44+bodyLength);w.Write(1);w.Write(36);
                w.Write(Encoding.ASCII.GetBytes("RTS0"));w.Write(bodyLength);
                w.Write(1);w.Write(count);w.Write(24);w.Write(0);w.Write(bodyLength);w.Write(flags);
                uint[][] ranges=tables.Select(t=>t.Split(',').Select(Number).ToArray()).ToArray();
                for(int i=0;i<count;i++)
                {
                    Bytes.Require(ranges[i].Length==6&&ranges[i][0]<=5&&ranges[i][1]<=3&&ranges[i][2]<=4096,"Invalid root range");
                    w.Write(0);w.Write(ranges[i][0]);w.Write(24+12*count+28*i);
                }
                for(int i=0;i<count;i++) { w.Write(1);w.Write(24+12*count+28*i+8);for(int j=1;j<6;j++)w.Write(ranges[i][j]); }
                byte[] b=stream.ToArray();Bytes.Require(Bytes.Hash(b)==row[1],"Generated root settings differ");return b;
            }
        }
        sealed class Sources:IDisposable
        {
            readonly string game,archives;readonly Dictionary<string,string[]> rows;
            readonly Dictionary<string,byte[]> cache=new Dictionary<string,byte[]>();readonly Queue<string> order=new Queue<string>();int cacheBytes;
            DsarReader archive;string archivePath;FileStream file;string filePath;
            public Sources(string game,string archives,Dictionary<string,string[]> rows) { this.game=game;this.archives=archives;this.rows=rows; }
            public byte[] Read(string hash)
            {
                if(hash=="-")return new byte[0];byte[] data;if(cache.TryGetValue(hash,out data))return data;
                string[] row;Bytes.Require(Bytes.IsHash(hash)&&rows.TryGetValue(hash,out row),"Shader reference is missing");row=rows[hash];
                string name=Name(row[3]);long offset=long.Parse(row[4],NumberStyles.None,CultureInfo.InvariantCulture);int size=checked((int)Number(row[5]));
                Bytes.Require(offset>=0&&size>=32&&size<=2*1024*1024,"Shader range is invalid");
                string path=Path.Combine(row[2]=="DSAR"?archives:game,name);NoLinks(path);
                if(row[2]=="DSAR")
                {
                    if(archivePath!=path) { if(archive!=null)archive.Dispose();archive=null;archivePath=null;archive=new DsarReader(path);archivePath=path; }
                    data=archive.Read(offset,size);
                }
                else
                {
                    Bytes.Require(row[2]=="FILE","Unknown shader source");
                    if(filePath!=path) { if(file!=null)file.Dispose();file=null;filePath=null;file=File.OpenRead(path);filePath=path; }
                    Bytes.Require(offset<=file.Length&&size<=file.Length-offset,"Shader exceeds source file");file.Position=offset;data=Bytes.Read(file,size);
                }
                Bytes.Require(Bytes.Hash(data)==hash&&Encoding.ASCII.GetString(data,0,4)=="DXBC"&&Bytes.U32(data,24)==size,"Shader source differs: "+name+". Use the supported original game files.");
                while(order.Count>0&&cacheBytes+size>16*1024*1024) { string first=order.Dequeue();cacheBytes-=cache[first].Length;cache.Remove(first); }
                cache.Add(hash,data);order.Enqueue(hash);cacheBytes+=size;return data;
            }
            public void Dispose() { if(archive!=null)archive.Dispose();if(file!=null)file.Dispose();cache.Clear(); }
        }
        static void Blob(BinaryWriter w,byte[] b) { w.Write(b.Length);w.Write(b); }
        public static bool IsReady(string output)
        {
            string receipt=Path.Combine(output,"generated-map.txt"),catalog=Path.Combine(output,"catalog.txt");
            if(!File.Exists(receipt)||(File.ReadAllText(receipt)!=MapHash&&File.ReadAllText(receipt)!=NewMapHash)||!File.Exists(catalog)||Bytes.FileHash(catalog)!=CatalogHash)return false;
            foreach(string line in File.ReadLines(catalog).Skip(1))
            {
                string[] p=line.Split(' ');string file=Path.Combine(output,p[0]+".recipe");
                if(!File.Exists(file)||new FileInfo(file).Length!=Number(p[2]))return false;
            }
            return true;
        }
        public static bool VerifyExisting(string output,Action<string> progress,CancellationToken cancel)
        {
            NoLinks(output);string catalog=Path.Combine(output,"catalog.txt");NoLinks(catalog);
            if(!File.Exists(catalog)||Bytes.FileHash(catalog)!=CatalogHash)return false;
            int done=0;
            foreach(string line in File.ReadLines(catalog).Skip(1))
            {
                cancel.ThrowIfCancellationRequested();string[] p=line.Split(' ');string file=Path.Combine(output,p[0]+".recipe");NoLinks(file);
                if(!File.Exists(file)||new FileInfo(file).Length!=Number(p[2])||Bytes.FileHash(file)!=p[1])return false;
                ++done;if(done%250==0||done==Count)progress?.Invoke("Verifying existing shader preparation "+done+" / "+Count);
            }
            Bytes.Require(done==Count,"Incomplete existing catalog");
            cancel.ThrowIfCancellationRequested();string receipt=Path.Combine(output,"generated-map.txt");NoLinks(receipt);File.WriteAllText(receipt,MapHash);return true;
        }
        public static void Run(string game,string archives,string metadata,string output,Action<string> progress,CancellationToken cancel)
        {
            game=Path.GetFullPath(game);archives=Path.GetFullPath(archives);output=Path.GetFullPath(output);
            Bytes.Require(!Inside(output,game)&&!Inside(output,archives),"Pipeline output must be outside original game files");
            NoLinks(game);NoLinks(archives);NoLinks(output);
            Bytes.Require(new FileInfo(metadata).Length<=8*1024*1024&&(Bytes.FileHash(metadata)==MapHash||Bytes.FileHash(metadata)==NewMapHash),"Pipeline reference map differs");
            string[] lines=File.ReadAllLines(metadata);Bytes.Require(lines.Length<=20000&&lines[0]=="SPIDER-PIPELINE-MAP-1\t"+Count+"\t"+CatalogHash,"Unsupported pipeline map");
            var roots=new Dictionary<string,byte[]>();var states=new Dictionary<string,byte[]>();var shaders=new Dictionary<string,string[]>();var pipes=new List<string[]>();var keys=new HashSet<string>();
            foreach(string line in lines.Skip(1))
            {
                cancel.ThrowIfCancellationRequested();var r=line.Split('\t');Bytes.Require(r.Length>=3&&Bytes.IsHash(r[1]),"Invalid pipeline map row");
                switch(r[0])
                {
                    case "ROOT":roots.Add(r[1],Root(r));break;
                    case "STATE":Bytes.Require(r.Length==3,"Invalid state row");byte[] b=Hex(r[2],8192);Bytes.Require(Bytes.Hash(b)==r[1],"State identity differs");states.Add(r[1],b);break;
                    case "SHADER":Bytes.Require(r.Length==6,"Invalid shader row");shaders.Add(r[1],r);break;
                    case "PIPE":Bytes.Require(r.Length==12&&keys.Add(r[1])&&Bytes.IsHash(r[2]),"Invalid or duplicate pipeline");pipes.Add(r);break;
                    default:throw new InvalidDataException("Unknown pipeline metadata");
                }
            }
            Bytes.Require(pipes.Count==Count,"Incomplete pipeline reference list");
            Directory.CreateDirectory(output);long needed=0;
            foreach(var p in pipes)if(!File.Exists(Path.Combine(output,p[1]+".recipe")))needed=checked(needed+Number(p[3]));
            Bytes.Require(DiskSpace.Available(output)>needed+64L*1024*1024,"Not enough storage for shader preparation recipes");
            var catalogText=new StringBuilder("SPIDER-FIRST-LAUNCH-1\n");int done=0;
            using(var source=new Sources(game,archives,shaders))foreach(var p in pipes.OrderBy(p=>p[1],StringComparer.Ordinal))
            {
                cancel.ThrowIfCancellationRequested();string path=Path.Combine(output,p[1]+".recipe");NoLinks(path);
                uint expectedSize=Number(p[3]);Bytes.Require(expectedSize>=48&&expectedSize<=2*1024*1024+48,"Invalid recipe size");
                if(File.Exists(path))Bytes.Require(new FileInfo(path).Length==expectedSize&&Bytes.FileHash(path)==p[2],"Existing pipeline recipe differs; original files kept");
                else
                {
                    byte[] body;
                    using(var memory=new MemoryStream())using(var writer=new BinaryWriter(memory))
                    {
                        writer.Write(1);Blob(writer,roots[p[5]]);for(int i=6;i<=10;i++)Blob(writer,source.Read(p[i]));writer.Write(states[p[11]]);body=memory.ToArray();
                    }
                    Bytes.Require(body.Length+48==expectedSize&&Bytes.Hash(body)==p[1],"Reconstructed pipeline differs from expected identity");
                    byte[] recipe;
                    using(var memory=new MemoryStream())using(var writer=new BinaryWriter(memory))
                    { writer.Write(0x31525053u);writer.Write(1);writer.Write(Number(p[4]));writer.Write(body.Length);writer.Write(Hex(p[1],32));writer.Write(body);recipe=memory.ToArray(); }
                    Bytes.Require(Bytes.Hash(recipe)==p[2],"Reconstructed recipe checksum differs");
                    string partial=path+".generate-partial";NoLinks(partial);
                    using(var f=new FileStream(partial,FileMode.Create,FileAccess.Write,FileShare.None)) { f.Write(recipe,0,recipe.Length);f.Flush(true); }
                    cancel.ThrowIfCancellationRequested();File.Move(partial,path);
                }
                catalogText.Append(p[1]).Append(' ').Append(p[2]).Append(' ').Append(p[3]).Append('\n');
                ++done;if(done==1||done%100==0||done==Count)progress?.Invoke("Building shader preparation list "+done+" / "+Count);
            }
            byte[] catalog=Encoding.ASCII.GetBytes(catalogText.ToString());Bytes.Require(Bytes.Hash(catalog)==CatalogHash,"Generated catalog differs");
            cancel.ThrowIfCancellationRequested();string target=Path.Combine(output,"catalog.txt"),temp=target+".generate-partial";NoLinks(target);NoLinks(temp);
            if(!File.Exists(target)||Bytes.FileHash(target)!=CatalogHash)
            {
                using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) { f.Write(catalog,0,catalog.Length);f.Flush(true); }
                if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);
            }
            string receiptPath=Path.Combine(output,"generated-map.txt");NoLinks(receiptPath);File.WriteAllText(receiptPath,Bytes.FileHash(metadata));
            progress?.Invoke("Verified "+Count+" shader preparation recipes from your game files. Compilation runs when the game starts.");
        }
    }
}
