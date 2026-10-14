// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading;
namespace AssetPreparation
{
    public static class ComputeGenerator
    {
        public const int Count=639;
        public const string MapHash="89650dd933b7d3c942fbc1a0f0354ea533c401383d130f3da40753b5ec2382a5";
        public static void Run(string executable,string metadata,string output,Action<string> progress,CancellationToken cancel)
        {
            string selectedHash=Bytes.FileHash(metadata);Bytes.Require(selectedHash==MapHash||selectedHash=="58f12a70ca3bd056297d2dc3d83af11d6826302746e1feba0c1cdf5c78221152","Compute preparation map differs");
            string[] lines=File.ReadAllLines(metadata);Bytes.Require(lines.Length==Count+1&&(lines[0]=="SPIDER-COMPUTE-MAP-1\t639\t9154e4e7bedbc5704da75ed1a362286bfc287d4cab5168c4ce6d22da18f94db1"||lines[0]=="SPIDER-COMPUTE-MAP-1\t639\te297d4d94f1ffe4febf289745e79e7b6fa233a788e7a00f480fc77c55db81ad1"),"Compute map header differs");
            byte[] root=PipelineGenerator.Root(new[]{"ROOT","29545df5c98fb3c0bd88bdc15860ae0afcbff9f23eead28755f73e12509e5636","d0a61a0ec076709e14a54ab698c0493e","29","0,0,128,0,0,0;0,2,14,0,0,0;0,1,16,0,0,0;0,3,16,0,0,0"});
            Directory.CreateDirectory(output);string receipt=Path.Combine(output,"generated-compute-map.txt");
            if(File.Exists(receipt)&&File.ReadAllText(receipt)==selectedHash&&File.Exists(Path.Combine(output,"catalog.txt"))&&lines.Skip(1).All(l=>File.Exists(Path.Combine(output,l.Split('\t')[0]+".compute"))))return;
            var catalog=new StringBuilder("SPIDER-COMPUTE-CATALOG-1\n");int done=0;
            using(var f=File.OpenRead(executable))foreach(string line in lines.Skip(1))
            {
                cancel.ThrowIfCancellationRequested();var parts=line.Split('\t');Bytes.Require(parts.Length==3&&Bytes.IsHash(parts[0]),"Compute reference invalid");
                long offset=long.Parse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture);int size=int.Parse(parts[2],NumberStyles.None,CultureInfo.InvariantCulture);
                Bytes.Require(size>=32&&size<=2*1024*1024&&offset>=0&&offset<=f.Length&&size<=f.Length-offset,"Compute reference bounds");
                f.Position=offset;byte[] code=Bytes.Read(f,size);Bytes.Require(Bytes.Hash(code)==parts[0]&&Encoding.ASCII.GetString(code,0,4)=="DXBC","Compute shader differs from supported installation");
                byte[] body;using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){w.Write(1);w.Write(root.Length);w.Write(root);w.Write(code.Length);w.Write(code);w.Write(0);w.Write(0);body=m.ToArray();}
                string key=Bytes.Hash(body);byte[] record;using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){w.Write(0x31435053u);w.Write(1);w.Write(0);w.Write(body.Length);for(int i=0;i<64;i+=2)w.Write(byte.Parse(key.Substring(i,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture));w.Write(body);record=m.ToArray();}
                string name=parts[0]+".compute",path=Path.Combine(output,name);if(!File.Exists(path)){string temp=path+".partial";using(var o=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){o.Write(record,0,record.Length);o.Flush(true);}File.Move(temp,path);}
                Bytes.Require(new FileInfo(path).Length==record.Length&&Bytes.FileHash(path)==Bytes.Hash(record),"Existing compute recipe differs");catalog.Append(parts[0]).Append(' ').Append(Bytes.Hash(record)).Append(' ').Append(record.Length).Append('\n');
                if(++done%100==0||done==Count)progress?.Invoke("Building compute preparation "+done+" / "+Count);
            }
            cancel.ThrowIfCancellationRequested();File.WriteAllText(Path.Combine(output,"catalog.txt"),catalog.ToString(),new UTF8Encoding(false));File.WriteAllText(receipt,selectedHash);
        }
    }
}
