// SPDX-License-Identifier: GPL-3.0-only
// Port of the local authored-LOD validator (ALERT-derived format research).
// Copies retained authored geometry from owned input; no model data is embedded.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AssetPreparation
{
    public static class ModelLod
    {
        sealed class Section { public uint Tag; public int Offset, Size, Table; }
        sealed class Mesh { public int V,I,VC,IC,Flags; }
        static int N(byte[] b,int o) { return checked((int)Bytes.U32(b,o)); }
        static void Range(int start,int size,int length) { Bytes.Require(start>=0&&size>=0&&start<=length&&size<=length-start,"Model range outside input"); }
        static int[] Map(int total,IEnumerable<Tuple<int,int>> ranges,out int[] keep)
        {
            Bytes.Require(total>0&&total<=16000000,"Model element count exceeds limit");
            int[] map=new int[total];for(int i=0;i<total;i++)map[i]=-1;
            foreach(var r in ranges) { Range(r.Item1,r.Item2,total);for(int i=r.Item1;i<r.Item1+r.Item2;i++)map[i]=0; }
            int count=0;for(int i=0;i<total;i++)if(map[i]==0)map[i]=count++;
            Bytes.Require(count>0,"Empty selected model");keep=new int[count];for(int i=0;i<total;i++)if(map[i]>=0)keep[map[i]]=i;
            return map;
        }
        // Vertex streams: seven planar XOR channels, independently reset every
        // 32768 vertices. Extra attributes interleave planes before XOR decoding.
        static byte[] SelectStream(byte[] raw,int offset,int length,int nv,int[] keep,bool vertex,int unit)
        {
            int stride=vertex?16:4;Bytes.Require(length==checked(nv*stride),"Model stream length mismatch");Range(offset,length,raw.Length);
            byte[] result=new byte[checked(keep.Length*stride)];int channels=vertex?7:1;
            for(int channel=0;channel<channels;channel++) {
                int width=vertex?(channel==0?4:2):4;uint[] values=new uint[nv];
                for(int start=0;start<nv;start+=32768) {
                    int count=Math.Min(32768,nv-start);uint previous=0;
                    for(int i=0;i<count;i++) {
                        uint delta;
                        if(vertex) { int p=offset+start*16+(channel==0?0:4+(channel-1)*2)*count+i*width;delta=width==4?Bytes.U32(raw,p):Bytes.U16(raw,p); }
                        else { delta=0;for(int plane=0;plane<4/unit;plane++) { int p=offset+start*4+plane*count*unit+i*unit;delta|=(uint)(unit==2?Bytes.U16(raw,p):raw[p])<<(plane*unit*8); } }
                        previous^=delta;values[start+i]=previous;
                    }
                }
                for(int start=0;start<keep.Length;start+=32768) {
                    int count=Math.Min(32768,keep.Length-start);uint previous=0;
                    for(int i=0;i<count;i++) {
                        uint value=values[keep[start+i]],delta=value^previous;previous=value;
                        if(vertex) { int p=start*16+(channel==0?0:4+(channel-1)*2)*count+i*width;for(int k=0;k<width;k++)result[p+k]=(byte)(delta>>(8*k)); }
                        else for(int plane=0;plane<4/unit;plane++) { int p=start*4+plane*count*unit+i*unit;for(int k=0;k<unit;k++)result[p+k]=(byte)(delta>>(8*(plane*unit+k))); }
                    }
                }
            }
            return result;
        }
        public static byte[] Transform(byte[] raw)
        {
            Bytes.Require(raw.Length>=64&&Bytes.U32(raw,0)==0x98906b9f&&Bytes.U32(raw,36)==0x44415431&&N(raw,44)==raw.Length-36,"Invalid model header");
            int count=Bytes.U16(raw,48),stream=N(raw,4);Bytes.Require(count>=8&&count<=40&&N(raw,8)==raw.Length-36-stream,"Invalid model stream header");
            Range(52,count*12,raw.Length);Range(36,stream,raw.Length);
            var ss=new Dictionary<uint,Section>();
            for(int i=0;i<count;i++) { int p=52+i*12;var s=new Section{Tag=Bytes.U32(raw,p),Offset=N(raw,p+4),Size=N(raw,p+8),Table=p};Range(36+s.Offset,s.Size,raw.Length);Bytes.Require(!ss.ContainsKey(s.Tag),"Duplicate model section");ss.Add(s.Tag,s); }
            var built=ss[0x283d0383];Bytes.Require(built.Size>=108,"Short model metadata");int bo=36+built.Offset,nv=N(raw,bo+104),ni=N(raw,bo+100);Bytes.Require((Bytes.U32(raw,bo+40)&0x100000)==0,"Unsupported morph model");
            var looks=ss[0x06eb7efc];var subsets=ss[0x78d9cbde];var lookBuilt=ss[0x811902d7];
            Bytes.Require(looks.Size%32==0&&subsets.Size%64==0&&subsets.Size/64<=2048&&lookBuilt.Size>=looks.Size/32*80,"Invalid model tables");
            int lc=looks.Size/32,mc=subsets.Size/64;var meshes=new Mesh[mc];var selected=new HashSet<int>();var fallback=new int[lc];
            for(int i=0;i<mc;i++) { int p=36+subsets.Offset+i*64;meshes[i]=new Mesh{V=N(raw,p+20),I=N(raw,p+24),IC=N(raw,p+28),VC=N(raw,p+32),Flags=Bytes.U16(raw,p+36)};Range(meshes[i].V,meshes[i].VC,nv);Range(meshes[i].I,meshes[i].IC,ni); }
            for(int i=0;i<lc;i++) {
                int p=36+looks.Offset+i*32,chosen=-1;
                for(int lod=3;lod<8;lod++)if(Bytes.U16(raw,p+lod*4+2)>0) { chosen=lod;break; }
                if(chosen<0)for(int lod=2;lod>=0;lod--)if(Bytes.U16(raw,p+lod*4+2)>0) { chosen=lod;break; }
                Bytes.Require(chosen>=0&&chosen<=6,"Unsupported model LOD");fallback[i]=chosen;
                for(int lod=chosen;lod<8;lod++) { int first=Bytes.U16(raw,p+lod*4),n=Bytes.U16(raw,p+lod*4+2);Range(first,n,mc);for(int j=first;j<first+n;j++)selected.Add(j); }
            }
            int[] vk,ik;int[] vm=Map(nv,selected.Select(i=>Tuple.Create(meshes[i].V,meshes[i].VC)),out vk),im=Map(ni,selected.Select(i=>Tuple.Create(meshes[i].I,meshes[i].IC)),out ik);
            var indexSection=ss[0x0859863d];Bytes.Require(indexSection.Size==checked(ni*2)&&indexSection.Offset==stream,"Invalid model index stream");
            ushort[] indices=new ushort[ni];ushort last=0;for(int i=0;i<ni;i++) { last=unchecked((ushort)(last+Bytes.U16(raw,36+indexSection.Offset+i*2)));indices[i]=last; }
            ushort[] mapped=new ushort[ik.Length];bool[] seen=new bool[ik.Length];
            foreach(int i in selected) {
                Mesh m=meshes[i];Bytes.Require(m.VC>0&&m.IC>0,"Empty selected subset");int newV=vm[m.V],newI=im[m.I];
                Bytes.Require(newV>=0&&newI>=0&&vm[m.V+m.VC-1]==newV+m.VC-1&&im[m.I+m.IC-1]==newI+m.IC-1,"Noncontiguous retained subset");
                for(int j=0;j<m.IC;j++) { int old=indices[m.I+j],value;
                    if((m.Flags&16)!=0) { Bytes.Require(old<m.VC,"Relative index outside subset");value=old; }
                    else { Bytes.Require(old>=m.V&&old<m.V+m.VC,"Absolute index outside subset");value=vm[old];Bytes.Require(value>=newV&&value<newV+m.VC,"Invalid remapped index"); }
                    Bytes.Require(value>=0&&value<=65535&&(!seen[newI+j]||mapped[newI+j]==value),"Conflicting index remap");mapped[newI+j]=(ushort)value;seen[newI+j]=true;
                }
            }
            Bytes.Require(seen.All(x=>x),"Unmapped retained indices");byte[] packedIndices=new byte[mapped.Length*2];last=0;
            for(int i=0;i<mapped.Length;i++) { ushort delta=unchecked((ushort)(mapped[i]-last));last=mapped[i];packedIndices[i*2]=(byte)delta;packedIndices[i*2+1]=(byte)(delta>>8); }
            var payload=new Dictionary<uint,byte[]>();payload.Add(0x0859863d,packedIndices);
            var vertices=ss[0xa98be69b];payload.Add(vertices.Tag,SelectStream(raw,36+vertices.Offset,vertices.Size,nv,vk,true,0));
            foreach(uint tag in new[]{0x6b855eedu,0x5cba9de9u})if(ss.ContainsKey(tag)) { var s=ss[tag];payload.Add(tag,SelectStream(raw,36+s.Offset,s.Size,nv,vk,false,tag==0x6b855eed?2:1)); }
            Bytes.Require(ss.Values.Where(s=>s.Offset>=stream).Select(s=>s.Tag).OrderBy(x=>x).SequenceEqual(payload.Keys.OrderBy(x=>x)),"Unknown model stream");
            int prefix=36+stream;byte[] head=new byte[prefix];Buffer.BlockCopy(raw,0,head,0,prefix);Bytes.Put32(head,bo+100,ik.Length);Bytes.Put32(head,bo+104,vk.Length);
            for(int i=0;i<lc;i++) {
                int p=36+looks.Offset+i*32,chosen=fallback[i],first=Bytes.U16(raw,p+chosen*4),n=Bytes.U16(raw,p+chosen*4+2);long mask=checked((long)BitConverter.ToUInt64(raw,36+lookBuilt.Offset+i*80+48));Range(checked((int)mask),1536,lookBuilt.Size);
                for(int lod=0;lod<chosen;lod++) { Bytes.Put16(head,p+lod*4,first);Bytes.Put16(head,p+lod*4+2,n);int mp=checked(36+lookBuilt.Offset+(int)mask+lod*256);Array.Clear(head,mp,256);for(int j=first;j<first+n;j++)head[mp+j/8]|=(byte)(1<<(j%8)); }
            }
            for(int i=0;i<mc;i++) { int p=36+subsets.Offset+i*64;Mesh m=meshes[i];if(selected.Contains(i)) { Bytes.Put32(head,p+20,vm[m.V]);Bytes.Put32(head,p+24,im[m.I]); }else { Array.Clear(head,p+20,16);head[p+42]=0; } }
            using(var output=new MemoryStream()) {
                output.Write(head,0,head.Length);
                foreach(var s in ss.Values.Where(s=>payload.ContainsKey(s.Tag)).OrderBy(s=>s.Offset)) { while((output.Position-36)%16!=0)output.WriteByte(0);Bytes.Put32(head,s.Table+4,checked((int)output.Position-36));Bytes.Put32(head,s.Table+8,payload[s.Tag].Length);output.Write(payload[s.Tag],0,payload[s.Tag].Length); }
                Bytes.Put32(head,8,checked((int)output.Length-prefix));Bytes.Put32(head,44,checked((int)output.Length-36));output.Position=0;output.Write(head,0,head.Length);return output.ToArray();
            }
        }
    }
}
