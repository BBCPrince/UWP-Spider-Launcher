using System;
using System.Collections.Generic;
using System.IO;

namespace DepotDownloader
{
    // Only explicit stage/result codes are passed here. Never pass URLs,
    // account names, passwords, access tokens or manifest request codes.
    internal static class TransferDiagnostics
    {
        static readonly object Sync=new object();
        static readonly Queue<string> Lines=new Queue<string>();
        static string path, failure;
        internal static string Failure { get { lock(Sync)return failure; } }
        internal static void Begin(string file)
        {
            lock(Sync){path=file;failure=null;Lines.Clear();Record("transfer_begin");}
        }
        internal static void Record(string text)
        {
            lock(Sync)
            {
                if(path==null)return;
                text=text.Replace('\r',' ').Replace('\n',' ');
                if(text.Length>320)text=text.Substring(0,320);
                Lines.Enqueue(DateTime.UtcNow.ToString("o")+" "+text);
                while(Lines.Count>96)Lines.Dequeue();
                try{File.WriteAllLines(path,Lines);}catch{}
            }
        }
        internal static void Fail(string reason)
        {
            lock(Sync){if(failure==null)failure=reason;Record("failure: "+reason);}
        }
        internal static void Error(string stage,Exception ex)
        {
            Record(stage+" exception="+ex.GetType().Name+" hresult=0x"+ex.HResult.ToString("X8"));
            if(ex.InnerException!=null)Record(stage+" inner="+ex.InnerException.GetType().Name+" hresult=0x"+ex.InnerException.HResult.ToString("X8"));
        }
    }
}
