using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace SpiderManInstaller
{
    // Derive the existing compatibility import from the verified owned SDK.
    // Contains library names and identities only; no SDK code is distributed.
    internal static class CrashReporterPreparation
    {
        const int Size=400384,ImportOffset=374318;
        const string OriginalHash="2c32f92818cbed7cd1205bc07c4f76a7be9c8be3199fdaef9138f8ea75b47f84";
        const string CompatibleHash="b74c86dcc3e5830a8a4c8a39609a48ea5416157647355757be9a073877ea6695";
        static string Digest(byte[] data){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
        internal static void Ensure(string file,Action<string> progress,CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            bool newer=GamePreparation.Profile==GameVersionSupport.Newer;
            int selectedSize=newer?479400:Size,selectedOffset=newer?427328:ImportOffset;
            string selectedOriginal=newer?"3436f313213dc9cbc22329401e0b1e427c135b22a9c55b1bd3725a31c0dbe464":OriginalHash,selectedCompatible=newer?"acfac7efcbd9d6b9e72711d96b660a57ad9980c925901d39d23c52404e2299b5":CompatibleHash;
            if(!File.Exists(file)||new FileInfo(file).Length!=selectedSize)throw new IOException("Crash-reporting component is missing or unsupported. Prepare the supported game files first.");
            byte[] original=File.ReadAllBytes(file);string hash=Digest(original);
            if(hash==selectedCompatible)return;
            if(hash!=selectedOriginal)throw new IOException("Crash-reporting component failed verification; your file was preserved.");
            byte[] before=Encoding.ASCII.GetBytes("KERNEL32.dll\0"),after=Encoding.ASCII.GetBytes(newer?"SpCr40.dll\0\0\0":"SpCrs.dll\0\0\0\0");
            for(int i=0;i<before.Length;i++)if(original[selectedOffset+i]!=before[i])throw new IOException("Crash-reporting import differs; your file was preserved.");
            string backup=file+(newer?".preparation-original-4.0630":".preparation-original"),partial=file+".prepare-crs.partial";
            if(File.Exists(backup)&&(new FileInfo(backup).Length!=selectedSize||Digest(File.ReadAllBytes(backup))!=selectedOriginal))throw new IOException("Crash-reporting backup differs; your files were preserved.");
            Array.Copy(after,0,original,selectedOffset,after.Length);
            if(Digest(original)!=selectedCompatible)throw new IOException("Crash-reporting compatibility result failed verification.");
            progress?.Invoke("Preparing the crash-reporting compatibility adapter…");
            using(var output=new FileStream(partial,FileMode.Create,FileAccess.Write,FileShare.None)){output.Write(original,0,original.Length);output.Flush(true);}
            if(Digest(File.ReadAllBytes(partial))!=selectedCompatible)throw new IOException("Crash-reporting staged file failed verification.");
            cancel.ThrowIfCancellationRequested();
            // Atomic replacement retains the complete original for rollback.
            File.Replace(partial,file,backup);
            if(Digest(File.ReadAllBytes(file))!=selectedCompatible)throw new IOException("Crash-reporting installed file failed verification.");
        }
    }
}
