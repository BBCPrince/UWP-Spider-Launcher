using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpiderManInstaller
{
    [DataContract] sealed class WifiInventoryRow
    {
        [DataMember(Name="path",IsRequired=true)] public string Path;
        [DataMember(Name="size",IsRequired=true)] public long Size;
    }
    [DataContract] sealed class WifiPlanRow
    {
        [DataMember(Name="path")] public string Path;
        [DataMember(Name="size")] public long Size;
        [DataMember(Name="done")] public bool Done;
    }
    [DataContract] sealed class WifiPlan
    {
        [DataMember(Name="files")] public WifiPlanRow[] Files;
        [DataMember(Name="total")] public long Total;
        [DataMember(Name="done")] public long Done;
    }

    // Shared by Xbox StreamSocket transport and the desktop loopback test server.
    sealed class WifiProtocol
    {
        readonly string root, page;
        readonly Func<long> free;
        readonly Action<string> status;
        readonly Action<bool> finished;
        readonly Action<long,long,string> progress;
        readonly WifiFolderTransfer folder;
        readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
        readonly object authLock=new object();
        DateTime authWindow=DateTime.UtcNow;
        int authFailures;
        volatile bool receiving;
        public bool Busy => receiving || folder.Active;
        public string PairCode { get; }
        public WifiProtocol(string root,string page,IList<ExpectedFile> expected,IList<LanguagePack> languages,Func<long> free,
            Action<string> status,Action<long,long,string> progress,Action<bool> finished)
        {
            this.root=root;this.page=page;this.free=free;this.status=status;this.progress=progress;this.finished=finished;
            folder=new WifiFolderTransfer(root,expected,languages,free,progress);
            byte[] random=new byte[4];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(random);
            PairCode=(BitConverter.ToUInt32(random,0)%1000000).ToString("D6");
        }
        internal static byte[] Json<T>(T value)
        {using(var s=new MemoryStream()){new DataContractJsonSerializer(typeof(T)).WriteObject(s,value);return s.ToArray();}}
        bool Authorize(string code)
        {
            lock(authLock)
            {
                if((DateTime.UtcNow-authWindow).TotalMinutes>=1){authWindow=DateTime.UtcNow;authFailures=0;}
                if(authFailures>=5)return false;
                if(code==PairCode)return true;
                authFailures++;return false;
            }
        }
        public async Task Handle(Stream input,Stream output,CancellationToken stop)
        {
            bool acquired=false;string partial=null;
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop))
            {
                try
                {
                    deadline.CancelAfter(TimeSpan.FromSeconds(30));
                    var header=new List<byte>();var one=new byte[1];bool ended=false;
                    while(header.Count<16384)
                    {
                        if(await input.ReadAsync(one,0,1,deadline.Token)!=1)return;
                        header.Add(one[0]);int n=header.Count;
                        if(n>=4 && header[n-4]==13 && header[n-3]==10 && header[n-2]==13 && header[n-1]==10){ended=true;break;}
                    }
                    if(!ended){await Respond(output,431,"Header too large.");return;}
                    string[] lines=Encoding.ASCII.GetString(header.ToArray()).Split(new[]{"\r\n"},StringSplitOptions.None);
                    string[] first=lines[0].Split(' ');
                    if(first.Length!=3){await Respond(output,400,"Invalid request.");return;}
                    var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                    foreach(string line in lines.Skip(1).Where(l=>l.Length>0))
                    {
                        int colon=line.IndexOf(':');
                        if(colon<=0 || headers.ContainsKey(line.Substring(0,colon)))throw new InvalidDataException("Invalid or duplicate HTTP header.");
                        headers.Add(line.Substring(0,colon),line.Substring(colon+1).Trim());
                    }
                    if(first[0]=="GET" && first[1]=="/"){await Respond(output,200,page,"text/html; charset=utf-8");return;}
                    string code;
                    if(!headers.TryGetValue("X-Transfer-Code",out code) || !Authorize(code))
                    {await Respond(output,403,"Enter the pairing code shown on Xbox. After several wrong attempts, wait one minute.");return;}
                    string origin,host;
                    if(headers.TryGetValue("Origin",out origin) && (!headers.TryGetValue("Host",out host) || origin!="http://"+host))
                    {await Respond(output,403,"Open the Xbox address directly in your browser.");return;}
                    string textLength;long length;
                    if(!headers.TryGetValue("Content-Length",out textLength) || !Int64.TryParse(textLength,out length) || length<0 || length>ImportEngine.MaxArchiveSize || headers.ContainsKey("Transfer-Encoding"))
                    {await Respond(output,411,"A known file size up to 100 GiB is required.");return;}
                    if(!await gate.WaitAsync(0)){await Respond(output,409,"Another request is still finishing. Try again shortly.");return;}
                    acquired=true;receiving=true;
                    if(first[0]=="POST" && first[1]=="/folder")
                    {
                        if(length==0 || length>4*1024*1024)throw new InvalidDataException("Folder list is too large or empty.");
                        byte[] data=new byte[(int)length];int done=0;
                        while(done<data.Length){int n=await input.ReadAsync(data,done,data.Length-done,deadline.Token);if(n==0)throw new IOException("Incomplete folder list.");done+=n;}
                        WifiInventoryRow[] inventory;
                        using(var s=new MemoryStream(data))inventory=(WifiInventoryRow[])new DataContractJsonSerializer(typeof(WifiInventoryRow[])).ReadObject(s);
                        if(inventory==null || inventory.Any(i=>i==null))throw new InvalidDataException("Invalid folder list.");
                        folder.Plan(inventory.Select(i=>new KeyValuePair<string,long>(i.Path,i.Size)));
                        var plan=new WifiPlan{Total=folder.Total,Done=folder.Done,Files=folder.Files.Select((f,i)=>new WifiPlanRow{Path=f.Path,Size=f.Size,Done=folder.IsVerified(i)}).ToArray()};
                        if(folder.Complete){receiving=false;finished?.Invoke(true);}
                        await Respond(output,200,Encoding.UTF8.GetString(Json(plan)),"application/json; charset=utf-8");return;
                    }
                    int index;
                    if(first[0]=="PUT" && first[1].StartsWith("/file/",StringComparison.Ordinal) && Int32.TryParse(first[1].Substring(6),out index))
                    {
                        // An idle-read deadline, not a whole-file timeout: multi-GiB archives can take minutes.
                        using(var timed=new IdleReadStream(input,deadline,stop))await folder.Receive(index,length,timed,stop);
                        receiving=false;
                        if(folder.Complete){status?.Invoke("Folder received and verified. Choose Prepare game.");finished?.Invoke(true);}
                        await Respond(output,200,folder.Complete?"Transfer complete. Choose Prepare game on Xbox.":"File verified.");return;
                    }
                    if(first[0]=="PUT" && first[1]=="/game.zip")
                    {
                        if(folder.Active)throw new IOException("Finish the folder transfer or restart the receiver before sending a ZIP.");
                        if(length==0)throw new InvalidDataException("Choose a ZIP file.");
                        if(File.Exists(Path.Combine(root,"received-game.zip")))throw new IOException("Import the ZIP already received before sending another.");
                        if(free()<checked(length+ImportEngine.Margin))throw new IOException("Not enough game storage for the ZIP.");
                        partial=Path.Combine(root,"upload-"+Guid.NewGuid().ToString("N")+".partial");
                        byte[] buffer=new byte[1024*1024];long done=0;
                        using(var file=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                        using(var timed=new IdleReadStream(input,deadline,stop))
                        {
                            while(done<length)
                            {
                                int n=await timed.ReadAsync(buffer,0,(int)Math.Min(buffer.Length,length-done),stop);
                                if(n==0)throw new IOException("Upload disconnected before completion.");
                                await file.WriteAsync(buffer,0,n,stop);done+=n;progress?.Invoke(done,length,"Receiving ZIP");
                            }
                            await file.FlushAsync(stop);
                        }
                        File.Move(partial,Path.Combine(root,"received-game.zip"));partial=null;receiving=false;
                        status?.Invoke("ZIP received. Choose Import received ZIP.");finished?.Invoke(false);
                        await Respond(output,200,"ZIP received. Choose Import received ZIP on Xbox.");return;
                    }
                    await Respond(output,404,"Unknown transfer request.");
                }
                catch(Exception ex)
                {
                    if(!stop.IsCancellationRequested)
                    {
                        string message=ex is OperationCanceledException?"Transfer timed out. Retry with the receiver still open.":ex.Message;
                        status?.Invoke("Transfer stopped: "+message);
                        try{await Respond(output,400,message);}catch{}
                    }
                }
                finally {if(partial!=null && File.Exists(partial))File.Delete(partial);if(acquired){receiving=false;gate.Release();}}
            }
        }
        static async Task Respond(Stream stream,int code,string text,string type="text/plain; charset=utf-8")
        {
            byte[] body=Encoding.UTF8.GetBytes(text);
            byte[] header=Encoding.ASCII.GetBytes("HTTP/1.1 "+code+" Response\r\nContent-Type: "+type+"\r\nContent-Length: "+body.Length+
                "\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nReferrer-Policy: no-referrer\r\nX-Frame-Options: DENY\r\nContent-Security-Policy: default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; base-uri 'none'; frame-ancestors 'none'\r\n\r\n");
            await stream.WriteAsync(header,0,header.Length);await stream.WriteAsync(body,0,body.Length);await stream.FlushAsync();
        }
        sealed class IdleReadStream : Stream
        {
            readonly Stream input;readonly CancellationTokenSource deadline;readonly CancellationToken stop;
            public IdleReadStream(Stream input,CancellationTokenSource deadline,CancellationToken stop){this.input=input;this.deadline=deadline;this.stop=stop;}
            public override async Task<int> ReadAsync(byte[] b,int o,int n,CancellationToken ignored)
            {stop.ThrowIfCancellationRequested();deadline.CancelAfter(TimeSpan.FromSeconds(60));return await input.ReadAsync(b,o,n,deadline.Token);}
            public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
            public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
            public override void Flush(){}public override int Read(byte[] b,int o,int n)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();
            public override void SetLength(long l)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int n)=>throw new NotSupportedException();
        }
    }
}
