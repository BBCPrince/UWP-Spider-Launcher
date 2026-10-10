using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.Sockets;

namespace SpiderManInstaller
{
    sealed class WifiReceiver : IDisposable
    {
        readonly CancellationTokenSource stop=new CancellationTokenSource();
        readonly HashSet<StreamSocket> clients=new HashSet<StreamSocket>();
        readonly WifiProtocol protocol;
        StreamSocketListener listener;
        public bool Uploading => protocol.Busy;
        public string Address { get; private set; }
        public string PairCode => protocol.PairCode;
        public string QrAddress => Address==null?null:Address+"#"+PairCode;
        public string Instructions => "Open "+(Address??"http://<Xbox IP>:8077/")+" on your PC. Pairing code: "+PairCode;
        public WifiReceiver(string local,string page,IList<ExpectedFile> expected,IList<LanguagePack> languages,
            Action<string> report,Action<long,long,string> progress,Action<bool> finished)
        {protocol=new WifiProtocol(local,page,expected,languages,()=>GameStorage.FreeBytes(local),report,progress,finished);}
        public async Task Start()
        {
            listener=new StreamSocketListener();listener.ConnectionReceived+=Receive;
            await listener.BindServiceNameAsync("8077");
            try
            {
                string ip=NetworkInformation.GetHostNames().Where(h=>h.Type==HostNameType.Ipv4 && PrivateAddress(h.CanonicalName))
                    .OrderByDescending(h=>h.IPInformation?.NetworkAdapter!=null).Select(h=>h.CanonicalName).FirstOrDefault();
                if(ip!=null)Address="http://"+ip+":8077/";
            }
            catch { }
        }
        internal static bool PrivateAddress(string text)
        {
            IPAddress ip;if(!IPAddress.TryParse(text,out ip))return false;
            if(ip.IsIPv4MappedToIPv6)ip=ip.MapToIPv4();var b=ip.GetAddressBytes();
            return b.Length==4 && (b[0]==10 || (b[0]==192 && b[1]==168) || (b[0]==172 && b[1]>=16 && b[1]<=31));
        }
        async void Receive(StreamSocketListener sender,StreamSocketListenerConnectionReceivedEventArgs args)
        {
            var socket=args.Socket;
            lock(clients){if(stop.IsCancellationRequested || clients.Count>=8){socket.Dispose();return;}clients.Add(socket);}
            try
            {
                if(!PrivateAddress(socket.Information.RemoteAddress.CanonicalName))return;
                if(Address==null && PrivateAddress(socket.Information.LocalAddress.CanonicalName))Address="http://"+socket.Information.LocalAddress.CanonicalName+":8077/";
                using(var input=socket.InputStream.AsStreamForRead())
                using(var output=socket.OutputStream.AsStreamForWrite())await protocol.Handle(input,output,stop.Token);
            }
            catch { }
            finally{lock(clients)clients.Remove(socket);socket.Dispose();}
        }
        public void Dispose()
        {
            if(stop.IsCancellationRequested)return;
            stop.Cancel();listener?.Dispose();lock(clients)foreach(var c in clients)c.Dispose();
        }
    }
}
