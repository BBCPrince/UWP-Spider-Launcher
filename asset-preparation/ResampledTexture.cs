// SPDX-License-Identifier: GPL-3.0-only
// Format edits ported from the local GPL-3.0 zone/distant-light asset builders.
using System;
using System.Runtime.InteropServices;
namespace AssetPreparation
{
    public static class ResampledTexture
    {
        [DllImport("TextureConvert.dll",CallingConvention=CallingConvention.Cdecl)]
        static extern int ConvertPixels(uint mode,byte[] input,uint inputBytes,uint width,uint height,uint slices,uint format,[Out]byte[] output,uint outputBytes);
        static int N(byte[] b,int o) { return checked((int)Bytes.U32(b,o)); }
        public static byte[] Transform(byte[] raw,bool zone,int section,int header,int payload)
        {
            if(!zone&&raw.Length==164&&Bytes.U32(raw,0)==0x567cc2f0) {
                Bytes.Require(N(raw,116)==812&&N(raw,120)==1472,"Invalid distant-light dimensions");byte[] owner=(byte[])raw.Clone();Bytes.Put32(owner,116,406);Bytes.Put32(owner,120,736);return owner;
            }
            Bytes.Require(header>=0&&header<=raw.Length-44&&payload>=header+44&&payload<=raw.Length,"Invalid texture header range");
            int w=Bytes.U16(raw,header+12),h=Bytes.U16(raw,header+14),slices=Bytes.U16(raw,header+16),format=Bytes.U16(raw,header+20),old=raw.Length-payload;
            Bytes.Require(N(raw,header)==old&&slices>0&&slices<=64,"Invalid texture data size");
            int divisor=zone?4:2,nw=w/divisor,nh=h/divisor;
            if(zone) { Bytes.Require(section>=36&&section<=header&&N(raw,16)==old&&N(raw,section+4)==old&&N(raw,section+12)==old&&format==72&&w%16==0&&h%16==0&&old==(long)w*h*slices/2,"Unsupported embedded zone texture"); }
            else Bytes.Require(header==84&&payload==128&&w==812&&h==1472&&((format==28&&slices==12)||(format==61&&slices==1))&&N(raw,8)==old&&N(raw,20)==old,"Unsupported distant texture");
            byte[] pixels=new byte[old];Buffer.BlockCopy(raw,payload,pixels,0,old);byte[] converted=new byte[old/(divisor*divisor)];
            int hr=ConvertPixels(zone?1u:2u,pixels,(uint)pixels.Length,(uint)w,(uint)h,(uint)slices,(uint)format,converted,(uint)converted.Length);if(hr<0)Marshal.ThrowExceptionForHR(hr);
            byte[] output=new byte[payload+converted.Length];Buffer.BlockCopy(raw,0,output,0,payload);Buffer.BlockCopy(converted,0,output,payload,converted.Length);
            if(zone) {
                Bytes.Put32(output,16,converted.Length);Bytes.Put32(output,section+4,converted.Length);Bytes.Put32(output,section+12,converted.Length);Bytes.Put32(output,20,checked((converted.Length+65535)&~65535));
                Bytes.Put32(output,header+4,0);output[header+30]=1;output[header+32]=0;
            } else { Bytes.Put32(output,8,converted.Length);Bytes.Put32(output,20,converted.Length); }
            Bytes.Put32(output,header,converted.Length);Bytes.Put16(output,header+8,nw);Bytes.Put16(output,header+10,nh);Bytes.Put16(output,header+12,nw);Bytes.Put16(output,header+14,nh);
            return output;
        }
    }
}
