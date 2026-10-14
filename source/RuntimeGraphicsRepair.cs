using System;
using System.Collections.Generic;
using System.IO;

namespace SpiderManInstaller
{
    // Only restores two exact, separately supplied compatibility binaries.
    // This launcher does not contain either binary or any game shader payload.
    internal static class RuntimeGraphicsRepair
    {
        internal sealed class Entry
        {
            internal string Name, Hash, PreviousHash, OlderHash;
        }
        internal static readonly Entry[] Expected = {
            new Entry { Name="SpD12.dll", Hash="a50e1995cd6c7d97f2f2ba2ba118721450dea6b0077d8b40e433b3593959924b", PreviousHash="6bcc22513a9287636fdeca9c9c7488c5b1498528b7343df5082b98d8ebcec457", OlderHash="82f42f93abc1919acc4f2b01a2845c660b5e64bdc19a4d2b3055ca13ef540719" },
            new Entry { Name="SpDx.dll", Hash="cff105c90622ab1588a0b54337101a33973317a4a3dfc2301195c275f323da9f", PreviousHash="6579386fa068e147cc41584a76acaa2aaa4c292ad1abeca9f99aa3d900c81549", OlderHash="c8eb4424eee6a1cf2c8765d8caa656a8f13c298b43bea3729d0f54295736774b" }
        };
        internal static void Ensure(string target, string support, Action<string> progress)
        { EnsureFiles(target,support,Expected,AssetPreparation.Bytes.FileHash,progress); }

        internal static void EnsureFiles(string target,string support,IEnumerable<Entry> entries,Func<string,string> hash,Action<string> progress)
        {
            var pending=new List<Entry>();
            foreach(var entry in entries)
            {
                string destination=Path.Combine(target,entry.Name);
                string current=File.Exists(destination)?hash(destination):null;
                if(current==entry.Hash)continue;
                if(current!=null && current!=entry.PreviousHash && current!=entry.OlderHash)
                    throw new IOException("Runtime graphics version differs. Install the matching separate runtime; your game files are preserved.");
                string source=Path.Combine(support,entry.Name);
                if(!File.Exists(source)||hash(source)!=entry.Hash)
                    throw new IOException("Update the separate runtime preparation support first. Its graphics files are outdated or missing; your game files are preserved.");
                pending.Add(entry);
            }
            // Verify every source and acquire all existing destinations before changing any.
            // A running game or another installer must not be replaced underneath itself.
            var locks=new List<FileStream>();
            try
            {
                foreach(var entry in pending)
                {
                    string path=Path.Combine(target,entry.Name);
                    if(File.Exists(path))locks.Add(new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None));
                }
            }
            finally { foreach(var file in locks)file.Dispose(); }
            foreach(var entry in pending)
            {
                progress("Restoring startup shader support: "+entry.Name);
                string path=Path.Combine(target,entry.Name),temporary=path+".repair-"+Guid.NewGuid().ToString("N");
                try
                {
                    File.Copy(Path.Combine(support,entry.Name),temporary,false);
                    if(hash(temporary)!=entry.Hash)throw new IOException("Runtime graphics copy failed verification");
                    if(File.Exists(path))
                    {
                        string current=hash(path);
                        if(current==entry.Hash)continue;
                        if(current!=entry.PreviousHash && current!=entry.OlderHash)throw new IOException("Runtime changed during repair; retry after closing the game");
                        File.Replace(temporary,path,path+".before-startup-repair",true);
                    }
                    else File.Move(temporary,path);
                    if(hash(path)!=entry.Hash)throw new IOException("Runtime graphics verification failed");
                }
                finally { if(File.Exists(temporary))File.Delete(temporary); }
            }
        }
    }
}
