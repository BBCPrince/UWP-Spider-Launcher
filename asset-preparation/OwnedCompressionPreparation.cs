// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
namespace AssetPreparation
{
    // Hashes and offsets are identification metadata, not game program bytes.
    public static class OwnedCompressionPreparation
    {
        sealed class Reference
        {
            public long Offset;public int InputSize,OutputSize;public string InputHash,OutputHash;
            public Reference(long offset,int inputSize,string inputHash,int outputSize,string outputHash)
            {Offset=offset;InputSize=inputSize;InputHash=inputHash;OutputSize=outputSize;OutputHash=outputHash;}
        }
        static readonly long[] NewOffsets={71732844,71481116,71604268,66527484};
        static readonly Reference[] References={
            new Reference(71748508,67160,"3f253cfd5bb2b09791448b754e15319414a9f45671dde6c707e535eae74f86aa",104092,"507184227097375ae42c12619b4b5cd7c1ef3f66e5e7a10952b284d23bfdaade"),
            new Reference(71496780,61016,"757fb90486afeaa1cbc36f033d7dc72e43a0313959f26267c79b6fd33b8a1a68",51076,"f051e9e714b1210cc2a721c2bc801b34a79fbf8d3afbe2fed7c4a28c2c72d3f0"),
            new Reference(71619932,67936,"2a6c2e8ce2344425d9c9a0e3f843306ff8f366442dc0164c9b05b2eeaadaf7eb",61428,"9f98b8e26c7d067e9cac38ed4eca3433b39b52ecc0bd122a533d9f3fac349bf3"),
            new Reference(66543148,11488,"76cef3dfd989f5861f874818bb819314918cb8e546a2d8022e6661f1bf7b9419",8436,"4da1ffdbfae54d36a94c52437cf18cfb103083126479971602c01cfdd88fba62"),
        };
        [DllImport("OwnedShaderConvert.dll",ExactSpelling=true,CharSet=CharSet.Unicode,CallingConvention=CallingConvention.Winapi)]
        static extern int ConvertOwnedCompression(string compilerRoot,byte[] input,uint inputSize,uint variant,[Out] byte[] output,uint capacity,out uint written);
        // Trust only roots supplied by the OS, never roots from imported files.
        // Xbox mounts can be reparse points; descendants must still be ordinary files.
        static void NoLinks(string path)
        {
            string trustedRoot=null;
#if WINDOWS_UWP
            string full=Path.GetFullPath(path);
            foreach(string osRoot in new[]{Windows.ApplicationModel.Package.Current.InstalledLocation.Path,Windows.Storage.ApplicationData.Current.LocalFolder.Path}) {
                string root=Path.GetFullPath(osRoot).TrimEnd('\\','/');
                if(full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)){trustedRoot=root;break;}
            }
#endif
            AssetBatch.CheckPathLinks(path,trustedRoot,p=>File.Exists(p)||Directory.Exists(p)?(FileAttributes?)File.GetAttributes(p):null);
        }
        static bool Verified(string path,Reference item)
        {
            if(!File.Exists(path))return false;
            NoLinks(path);
            return new FileInfo(path).Length==item.OutputSize&&Bytes.FileHash(path)==item.OutputHash;
        }
        public static void Run(string executable,string compilerRoot,string output,Action<string> progress,CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();NoLinks(executable);NoLinks(output);
            bool all=true;foreach(var r in References)all&=Verified(Path.Combine(output,r.InputHash+".dxil"),r);
            if(all)return;
            // Never copy a system converter into the package. The native helper
            // uses the OS converter and separately reviewed compiler libraries.
            NoLinks(compilerRoot);
            if(!File.Exists(Path.Combine(compilerRoot,"dxcompiler.dll"))||!File.Exists(Path.Combine(compilerRoot,"dxil.dll")))
                throw new IOException("The included shader-conversion tools are incomplete. Original game files are preserved.");
            Directory.CreateDirectory(output);
            using(var source=new FileStream(executable,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                for(int i=0;i<References.Length;++i)
                {
                    cancel.ThrowIfCancellationRequested();var r=References[i];string target=Path.Combine(output,r.InputHash+".dxil");
                    if(Verified(target,r))continue;
                    Bytes.Require(r.Offset>=0&&r.Offset<=source.Length&&r.InputSize<=source.Length-r.Offset,"Owned shader reference is outside the supported game.");
                    source.Position=r.Offset;byte[] input=Bytes.Read(source,r.InputSize);
                    if(Bytes.Hash(input)!=r.InputHash){source.Position=NewOffsets[i];input=Bytes.Read(source,r.InputSize);}
                    Bytes.Require(Bytes.Hash(input)==r.InputHash,"Owned compression shader differs from the supported game.");
                    byte[] result=new byte[r.OutputSize];uint written;
                    progress?.Invoke("Converting owned compression shader "+(i+1)+" / "+References.Length);
                    int hr=ConvertOwnedCompression(compilerRoot,input,(uint)input.Length,(uint)i,result,(uint)result.Length,out written);
                    if(hr<0)throw new IOException("Owned shader conversion failed (0x"+unchecked((uint)hr).ToString("x8")+"). Original game files are preserved.");
                    cancel.ThrowIfCancellationRequested();
                    Bytes.Require(written==result.Length&&Bytes.Hash(result)==r.OutputHash,"Converted shader differs from the verified working program.");
                    string partial=target+"."+Guid.NewGuid().ToString("N")+".partial";
                    try
                    {
                        using(var file=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(result,0,result.Length);file.Flush(true);}
                        cancel.ThrowIfCancellationRequested();
                        if(File.Exists(target))File.Replace(partial,target,null);else File.Move(partial,target);
                    }
                    finally{if(File.Exists(partial))File.Delete(partial);}
                    Bytes.Require(Verified(target,r),"Owned shader output failed verification.");
                }
            }
            cancel.ThrowIfCancellationRequested();
            foreach(var r in References)Bytes.Require(Verified(Path.Combine(output,r.InputHash+".dxil"),r),"Owned shader preparation is incomplete.");
        }
    }
}
