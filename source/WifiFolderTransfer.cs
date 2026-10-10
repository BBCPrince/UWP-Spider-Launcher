using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpiderManInstaller
{
    // Direct-to-import streaming: no ZIP, second game copy, or client-supplied hashes.
    // Called under the receiver's request gate. Only packaged, verified paths are written.
    sealed class WifiFolderTransfer
    {
        readonly string root;
        readonly IList<ExpectedFile> manifest;
        readonly IList<LanguagePack> languages;
        readonly Func<long> free;
        readonly Action<long,long,string> progress;
        List<ExpectedFile> selected;
        readonly HashSet<int> verified = new HashSet<int>();
        public string Destination { get; private set; }
        public IList<ExpectedFile> Files => selected;
        public bool Complete { get; private set; }
        public bool Active => selected != null && !Complete;
        public long Total { get; private set; }
        public long Done { get; private set; }
        public bool IsVerified(int index) => verified.Contains(index);

        public WifiFolderTransfer(string root, IList<ExpectedFile> manifest, IList<LanguagePack> languages,
            Func<long> free, Action<long,long,string> progress)
        { this.root=root; this.manifest=manifest; this.languages=languages; this.free=free; this.progress=progress; }

        public void Plan(IEnumerable<KeyValuePair<string,long>> inventory)
        {
            var available=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
            foreach(var file in inventory)
            {
                string name=ImportEngine.SafePath(file.Key);
                if(available.Count>=20000 || name.EndsWith("/") || file.Value<0 || file.Value>ImportEngine.MaxArchiveSize || available.ContainsKey(name))
                    throw new InvalidDataException("Invalid or duplicate folder entry.");
                available.Add(name,file.Value);
            }
            var next=LanguageImports.Select(manifest,languages,p=>available.ContainsKey(p)?(long?)available[p]:null)
                .OrderBy(f=>f.Path.Equals("Spider-Man.exe",StringComparison.OrdinalIgnoreCase)?0:f.Path.Equals("asset_archive/toc",StringComparison.OrdinalIgnoreCase)?1:2).ToList();
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total=0;
            foreach(var f in next)
            {
                if(!names.Add(ImportEngine.SafePath(f.Path)) || f.Size<0 || f.Hash==null || f.Hash.Length!=64 || f.Hash.Any(c=>!Uri.IsHexDigit(c)))
                    throw new InvalidDataException("Invalid supported-version catalogue.");
                if(!available.ContainsKey(f.Path) || available[f.Path]!=f.Size)
                    throw new InvalidDataException("Missing or wrong-size file: "+f.Path+". Choose the supported Steam game folder.");
                total=checked(total+f.Size);
            }
            if(next.Count<2 || next[0].Path!="Spider-Man.exe" || next[1].Path!="asset_archive/toc" || total>ImportEngine.MaxArchiveSize)
                throw new InvalidDataException("Invalid version fingerprints or game size.");
            if(selected!=null)
            {
                if(next.Count!=selected.Count || next.Where((f,i)=>f.Path!=selected[i].Path || f.Hash!=selected[i].Hash).Any())
                    throw new IOException("A different folder transfer is already open. Stop the receiver on Xbox first.");
                CommitIfComplete();
                return; // Re-selecting the same folder resumes verified files in this receiver session.
            }
            if(free()<checked(total+ImportEngine.Margin))throw new IOException("Not enough game storage for the files plus 1 GiB free.");
            selected=next;Total=total;
            Destination=Path.Combine(root,"SteamImport-"+Guid.NewGuid().ToString("N"));
            progress?.Invoke(0,Total,"Checking the game version");
        }

        public async Task Receive(int index, long length, Stream input, CancellationToken cancel)
        {
            if(selected==null || index<0 || index>=selected.Count)throw new InvalidDataException("Choose a game folder first.");
            var row=selected[index];
            if(length!=row.Size)throw new InvalidDataException("File size changed: "+row.Path);
            int first=Enumerable.Range(0,selected.Count).FirstOrDefault(i=>!verified.Contains(i));
            if(!verified.Contains(index) && index!=first)throw new InvalidDataException("Send the requested files in order; the game version is checked first.");
            if(!verified.Contains(index) && free()<checked(length+ImportEngine.Margin))throw new IOException("Not enough game storage to finish this file safely.");
            string target=Path.Combine(Destination,row.Path.Replace('/',Path.DirectorySeparatorChar));
            // Destination is generated locally, and row.Path comes only from the pinned manifest.
            string prefix=Path.GetFullPath(Destination)+Path.DirectorySeparatorChar;
            if(!Path.GetFullPath(target).StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Unsafe destination.");
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            string partial=target+".partial";
            bool already=verified.Contains(index);
            try
            {
                using(var output=already?Stream.Null:new FileStream(partial,FileMode.Create,FileAccess.Write,FileShare.None))
                using(var hash=SHA256.Create())
                {
                    byte[] buffer=new byte[1024*1024];long written=0;
                    while(written<length)
                    {
                        int count=await input.ReadAsync(buffer,0,(int)Math.Min(buffer.Length,length-written),cancel).ConfigureAwait(false);
                        if(count==0)throw new IOException("Connection ended before the file finished. Reconnect and send the folder again.");
                        await output.WriteAsync(buffer,0,count,cancel).ConfigureAwait(false);
                        hash.TransformBlock(buffer,0,count,buffer,0);written+=count;
                        if(!already)progress?.Invoke(Done+written,Total,row.Path);
                    }
                    hash.TransformFinalBlock(new byte[0],0,0);
                    if(!BitConverter.ToString(hash.Hash).Replace("-","").Equals(row.Hash,StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Unsupported or damaged game file: "+row.Path+" (SHA-256 mismatch).");
                    await output.FlushAsync(cancel).ConfigureAwait(false);
                }
                cancel.ThrowIfCancellationRequested();
                if(!already){File.Move(partial,target);verified.Add(index);Done+=length;}
                CommitIfComplete();
                progress?.Invoke(Done,Total,Complete?"Copy complete and verified — choose Prepare game.":row.Path);
            }
            finally { if(File.Exists(partial))File.Delete(partial); }
        }
        void CommitIfComplete()
        {
            if(verified.Count!=selected.Count || Complete)return;
            string receipt=Path.Combine(Destination,"import-receipt.txt");
            File.WriteAllText(receipt+".partial",
                "Original Steam folder received over Wi-Fi and SHA-256 verified.\r\nImported UTC: "+DateTime.UtcNow.ToString("o")+
                "\r\nFiles: "+selected.Count+"\r\nBytes: "+Total+"\r\n"+LanguageImports.Description(selected,languages)+"\r\nReady for game preparation.\r\n",Encoding.UTF8);
            File.Move(receipt+".partial",receipt);Complete=true;
        }
    }
}
