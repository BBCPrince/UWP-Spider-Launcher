// SPDX-License-Identifier: MIT
// Thin CPU-only DirectXTex adapter. No game content or GPU device is included.
#include "DirectXTex.h"
#include <cstring>
#include <new>
using namespace DirectX;
extern "C" __declspec(dllexport) int __cdecl ConvertPixels(
    unsigned mode, const unsigned char* input, unsigned inputBytes,
    unsigned width, unsigned height, unsigned slices, unsigned format,
    unsigned char* output, unsigned outputBytes) noexcept
{
    try {
        if (!input || !output || width<4 || height<4 || width>8192 || height>8192 || slices==0 || slices>64) return E_INVALIDARG;
        if (mode!=1 && mode!=2) return E_INVALIDARG;
        if (mode==1 && (format!=72 || width%16 || height%16)) return E_INVALIDARG;
        if (mode==2 && ((format!=28 && format!=61) || width%2 || height%2)) return E_INVALIDARG;
        const size_t bpp=(format==28)?4:1;
        const size_t stride=mode==1?size_t(width)*height/2:size_t(width)*height*bpp;
        const size_t outStride=mode==1?stride/16:stride/4;
        if (stride*slices!=inputBytes || outStride*slices!=outputBytes) return E_INVALIDARG;
        // Process one slice at a time, bounding temporary memory and using no GPU.
        for(unsigned slice=0;slice<slices;++slice) {
            Image source={width,height,static_cast<DXGI_FORMAT>(format), mode==1?size_t(width)*2:size_t(width)*bpp,stride,const_cast<uint8_t*>(input+stride*slice)};
            ScratchImage decoded,mips,packed,resized; HRESULT hr;
            const Image* result=nullptr;
            if(mode==1) {
                hr=Decompress(source,DXGI_FORMAT_UNKNOWN,decoded); if(FAILED(hr))return hr;
                hr=GenerateMipMaps(*decoded.GetImage(0,0,0),TEX_FILTER_BOX,3,mips); if(FAILED(hr))return hr;
                hr=Compress(*mips.GetImage(2,0,0),DXGI_FORMAT_BC1_UNORM_SRGB,TEX_COMPRESS_DEFAULT,0.5f,packed);if(FAILED(hr))return hr;
                result=packed.GetImage(0,0,0);
            } else {
                hr=Resize(source,width/2,height/2,TEX_FILTER_LINEAR|TEX_FILTER_FORCE_NON_WIC|TEX_FILTER_SEPARATE_ALPHA,resized);if(FAILED(hr))return hr;
                result=resized.GetImage(0,0,0);
            }
            if(!result || result->slicePitch!=outStride)return E_FAIL;
            memcpy(output+outStride*slice,result->pixels,outStride);
        }
        return S_OK;
    } catch(const std::bad_alloc&) { return E_OUTOFMEMORY; }
      catch(...) { return E_FAIL; }
}
