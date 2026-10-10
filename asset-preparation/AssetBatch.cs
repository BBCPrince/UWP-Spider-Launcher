// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
namespace AssetPreparation
{
    public sealed class GeneratedFile { public string Name,Hash;public long Size; }
    public static class AssetBatch
    {
        static string Name(string n) { Bytes.Require(n.Length>0&&n.Length<=80&&n!="."&&n!=".."&&n.All(c=>c>='a'&&c<='z'||c>='0'&&c<='9'||c=='_'||c=='.'||c=='-'),"Unsafe preparation filename");return n; }
        // Only packaged, read-only recipes can use the OS-provided package mount.
        // Imported data and writable output never receive this exception.
        internal static void CheckPathLinks(string path,string trustedRoot,Func<string,FileAttributes?> attributes)
        {
            path=Path.GetFullPath(path);
            if(trustedRoot!=null) {
                trustedRoot=Path.GetFullPath(trustedRoot).TrimEnd('\\','/');
                Bytes.Require(path.StartsWith(trustedRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Recipe path is outside the installed package");
            }
            for(string p=path;p!=null;p=Path.GetDirectoryName(p)) {
                if(trustedRoot!=null&&p.Equals(trustedRoot,StringComparison.OrdinalIgnoreCase))return;
                FileAttributes? value=attributes(p);
                Bytes.Require(!value.HasValue||(value.Value&FileAttributes.ReparsePoint)==0,"Preparation paths cannot use links: "+p);
            }
        }
        static void NoLinks(string path,string trustedRoot=null)
        {
            CheckPathLinks(path,trustedRoot,p=>Directory.Exists(p)||File.Exists(p)?(FileAttributes?)File.GetAttributes(p):null);
        }
        public static void Verify(string path,bool index,long length,string expected,CancellationToken cancel)
        {
            using(var hash=SHA256.Create()) {
                if(index) {
                    using(var f=File.OpenRead(path)) {
                        byte[] h=Bytes.Read(f,10);Bytes.Require(Bytes.U32(h,0)==0x77af12af&&Bytes.U32(h,4)==length&&h[8]==0x78&&((h[8]<<8)+h[9])%31==0&&(h[9]&32)==0,"Generated index header mismatch");
                        using(var z=new DeflateStream(f,CompressionMode.Decompress)) { long left=length;while(left>0) { cancel.ThrowIfCancellationRequested();byte[] b=Bytes.Read(z,(int)Math.Min(1048576,left));hash.TransformBlock(b,0,b.Length,b,0);left-=b.Length; }Bytes.Require(z.ReadByte()==-1,"Generated index exceeds expected size"); }
                    }
                } else using(var archive=new DsarReader(path)) {
                    Bytes.Require(archive.Length==length,"Generated archive length mismatch");for(long offset=0;offset<length;offset+=1048576) { cancel.ThrowIfCancellationRequested();byte[] b=archive.Read(offset,(int)Math.Min(1048576,length-offset));hash.TransformBlock(b,0,b.Length,b,0); }
                }
                hash.TransformFinalBlock(new byte[0],0,0);Bytes.Require(BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant()==expected,"Generated content failed verification");
            }
        }
        // Owns only the generated output directory. Never removes or modifies
        // source archives. Complete files are verified and reused after restart.
        public static List<GeneratedFile> Run(string source,string originalIndex,string recipes,string output,Action<string> progress,CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();source=Path.GetFullPath(source);output=Path.GetFullPath(output);recipes=Path.GetFullPath(recipes);originalIndex=Path.GetFullPath(originalIndex);
            string recipeMount=null;
#if WINDOWS_UWP
            string installed=Path.GetFullPath(Windows.ApplicationModel.Package.Current.InstalledLocation.Path).TrimEnd('\\','/');
            if(recipes.StartsWith(installed+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))recipeMount=installed;
#endif
            NoLinks(source);NoLinks(output);NoLinks(recipes,recipeMount);NoLinks(originalIndex);
            string sp=source.TrimEnd('\\','/')+Path.DirectorySeparatorChar,op=output.TrimEnd('\\','/')+Path.DirectorySeparatorChar;
            Bytes.Require(!op.StartsWith(sp,StringComparison.OrdinalIgnoreCase)&&!sp.StartsWith(op,StringComparison.OrdinalIgnoreCase),"Generated output must be separate from input");
            string manifest=Path.Combine(recipes,"batch.tsv");Bytes.Require(new FileInfo(manifest).Length<=65536,"Oversized generation manifest");string[] lines=File.ReadAllLines(manifest);var header=lines[0].Split('\t');
            Bytes.Require(header.Length==2&&header[0]=="ASSETGEN_BATCH_V1"&&Bytes.IsHash(header[1])&&Bytes.FileHash(originalIndex)==header[1],"Unsupported original game index");
            Bytes.Require(lines.Length>1&&lines.Length<=130,"Unexpected generation manifest count");var names=new HashSet<string>();var rows=new List<string[]>();long space=0;bool foundIndex=false;
            foreach(string line in lines.Skip(1)) {
                var r=line.Split('\t');Bytes.Require(r.Length==5&&(r[0]=="ARCHIVE"||r[0]=="INDEX")&&(!foundIndex||r[0]=="INDEX"),"Invalid generation order");Name(r[1]);Name(r[2]);Bytes.Require(names.Add(r[1])&&Bytes.IsHash(r[4]),"Duplicate or invalid generated file");
                long size=long.Parse(r[3],CultureInfo.InvariantCulture);Bytes.Require(size>0&&size<uint.MaxValue,"Invalid decoded size");
                if(r[0]=="INDEX") { Bytes.Require((r[1]=="toc"||r[1]=="runtime-toc")&&size<=64*1024*1024,"Invalid index");foundIndex=true; }else Bytes.Require(r[1].StartsWith("local_",StringComparison.Ordinal)&&r[1].EndsWith(".dsar",StringComparison.Ordinal),"Invalid archive filename");
                string target=Path.Combine(output,r[1]);NoLinks(target);NoLinks(target+".partial");NoLinks(Path.Combine(recipes,r[2]),recipeMount);if(!File.Exists(target))space=checked(space+size);rows.Add(r);
            }
            Bytes.Require(foundIndex,"Missing generated index");Directory.CreateDirectory(output);Bytes.Require(DiskSpace.Available(output)>space+128L*1024*1024,"Insufficient space to generate assets");
            var result=new List<GeneratedFile>();int done=0;
            foreach(var r in rows) {
                cancel.ThrowIfCancellationRequested();string target=Path.Combine(output,r[1]);bool index=r[0]=="INDEX";long size=long.Parse(r[3],CultureInfo.InvariantCulture);
                progress?.Invoke("Preparing assets "+(++done)+" / "+rows.Count+": "+r[1]);
                if(!File.Exists(target)) {
                    // A validated basename under this task-owned directory only.
                    if(File.Exists(target+".partial"))File.Delete(target+".partial");
                    if(index)AssetIndex.Run(originalIndex,Path.Combine(recipes,r[2]),target);
                    else Generate.Run(source,Path.Combine(recipes,r[2]),target,cancel,(n,total)=>progress?.Invoke("Preparing assets "+done+" / "+rows.Count+": "+n+" / "+total),originalIndex);
                }
                Verify(target,index,size,r[4],cancel);result.Add(new GeneratedFile{Name=r[1],Size=new FileInfo(target).Length,Hash=Bytes.FileHash(target)});
            }
            return result;
        }
    }
}
