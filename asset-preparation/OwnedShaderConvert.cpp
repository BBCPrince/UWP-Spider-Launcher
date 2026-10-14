// SPDX-License-Identifier: GPL-3.0-only
// Own preparation glue. No game shader source, bytecode or converted program
// is embedded here. Microsoft compiler components require independent review.
#define NOMINMAX
#include <Windows.h>
#include <wrl/client.h>
#include <string>
#include <regex>
#include <map>
#include <array>
#include <stdexcept>
#include <mutex>
#include <functional>
#include <cstring>
#include "DxbcConverter.h"
using Microsoft::WRL::ComPtr;
namespace {
using Create=HRESULT(WINAPI*)(REFCLSID,REFIID,void**);
void Check(HRESULT hr){if(FAILED(hr))throw hr;}
struct Module {
    HMODULE handle=nullptr;
    Module(const std::wstring& path,DWORD flags){handle=LoadLibraryExW(path.c_str(),nullptr,flags);if(!handle)throw HRESULT_FROM_WIN32(GetLastError());}
    ~Module(){if(handle)FreeLibrary(handle);}
    Create Factory(){auto p=reinterpret_cast<Create>(GetProcAddress(handle,"DxcCreateInstance"));if(!p)throw HRESULT_FROM_WIN32(ERROR_PROC_NOT_FOUND);return p;}
};
struct Api {
    Module converter,compiler,validator;
    ComPtr<IDxbcConverter> legacy;
    ComPtr<IDxcLibrary> library;
    ComPtr<IDxcCompiler> disassembler;
    ComPtr<IDxcAssembler> assembler;
    ComPtr<IDxcOptimizer> optimizer;
    ComPtr<IDxcValidator> validation;
    explicit Api(const std::wstring& root):converter(L"dxilconv.dll",LOAD_LIBRARY_SEARCH_SYSTEM32),
        compiler(root+L"\\dxcompiler.dll",LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR|LOAD_LIBRARY_SEARCH_DEFAULT_DIRS),
        validator(root+L"\\dxil.dll",LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR|LOAD_LIBRARY_SEARCH_DEFAULT_DIRS){
        Check(converter.Factory()(CLSID_DxbcConverter,IID_PPV_ARGS(&legacy)));
        auto make=compiler.Factory();
        Check(make(CLSID_DxcLibrary,IID_PPV_ARGS(&library)));
        Check(make(CLSID_DxcCompiler,IID_PPV_ARGS(&disassembler)));
        Check(make(CLSID_DxcAssembler,IID_PPV_ARGS(&assembler)));
        Check(make(CLSID_DxcOptimizer,IID_PPV_ARGS(&optimizer)));
        Check(validator.Factory()(CLSID_DxcValidator,IID_PPV_ARGS(&validation)));
    }
    ComPtr<IDxcBlobEncoding> Blob(const void* data,UINT32 length){ComPtr<IDxcBlobEncoding> b;Check(library->CreateBlobWithEncodingFromPinned(data,length,CP_UTF8,&b));return b;}
    ComPtr<IDxcBlob> Result(IDxcOperationResult* operation){HRESULT status=E_FAIL;Check(operation->GetStatus(&status));Check(status);ComPtr<IDxcBlob> result;Check(operation->GetResult(&result));if(!result)throw E_FAIL;return result;}
    ComPtr<IDxcBlob> Validate(IDxcBlob* shader){ComPtr<IDxcOperationResult> operation;Check(validation->Validate(shader,DxcValidatorFlags_InPlaceEdit,&operation));auto result=Result(operation.Get());return result;}
    ComPtr<IDxcBlob> Assemble(const std::string& text){auto b=Blob(text.data(),static_cast<UINT32>(text.size()));ComPtr<IDxcOperationResult> op;Check(assembler->AssembleToContainer(b.Get(),&op));auto result=Result(op.Get());return Validate(result.Get());}
    std::string Text(IDxcBlob* shader){ComPtr<IDxcBlobEncoding> b;Check(disassembler->Disassemble(shader,&b));if(b->GetBufferSize()>16*1024*1024)throw E_INVALIDARG;std::string s(static_cast<const char*>(b->GetBufferPointer()),b->GetBufferSize());while(!s.empty()&&s.back()==0)s.pop_back();return s;}
    ComPtr<IDxcBlob> Optimize(IDxcBlob* shader,bool combine=false){LPCWSTR passes[]={combine?L"-instcombine":L"-mem2reg",L"-simplifycfg",L"-gvn",L"-adce"};ComPtr<IDxcBlob> code;ComPtr<IDxcBlobEncoding> messages;Check(optimizer->RunOptimizer(shader,passes,4,&code,&messages));ComPtr<IDxcOperationResult> op;Check(assembler->AssembleToContainer(code.Get(),&op));auto result=Result(op.Get());return Validate(result.Get());}
};
std::string Replace(const std::string& text,const std::regex& pattern,const std::function<std::string(const std::smatch&)>& value){
    std::string out;size_t position=0;
    for(std::sregex_iterator i(text.begin(),text.end(),pattern),end;i!=end;++i){const auto& m=*i;out.append(text,position,size_t(m.position())-position);out+=value(m);position=size_t(m.position()+m.length());}
    out.append(text,position,std::string::npos);return out;
}
std::string Normalize(std::string text){
    if(text.find("target triple")==std::string::npos)text="target triple = \"dxil-ms-dx\"\n"+text;
    for(const auto name:{"tempRegStore.i32","tempRegStore.f32","tempRegLoad.i32","tempRegLoad.f32"}){
        std::string marker="@dx.op."+std::string(name)+"(";size_t first=text.find(marker);
        if(first!=std::string::npos&&text.find(marker,first+marker.size())==std::string::npos){size_t begin=text.rfind('\n',first);begin=begin==std::string::npos?0:begin+1;size_t end=text.find('\n',first);if(text.compare(begin,8,"declare ")==0)text.erase(begin,end-begin);}
    }
    if(text.find("Typed UAV Load Additional Formats")!=std::string::npos){
        std::smatch entry,node;Check(std::regex_search(text,entry,std::regex(R"(!dx.entryPoints = !\{!(\d+)\})"))?S_OK:E_INVALIDARG);
        Check(std::regex_search(text,node,std::regex("!"+entry[1].str()+R"( = !\{[^\n]*!(\d+)\})"))?S_OK:E_INVALIDARG);
        unsigned count=0;text=Replace(text,std::regex("(!"+node[1].str()+R"( = !\{i32 0, i64 )(\d+))"),[&](const std::smatch& m){++count;return m[1].str()+std::to_string(std::stoull(m[2].str())|8192);});Check(count==1?S_OK:E_INVALIDARG);
    }
    return text;
}
std::string Canonical(std::string text){
    for(const auto& item:std::array<std::array<const char*,4>,2>{{{{"bitcastI32toF32","126","i32","float"}},{{"bitcastF32toI32","127","float","i32"}}}}){
        std::string name=item[0];text=std::regex_replace(text,std::regex("call "+std::string(item[3])+" @dx\\.op\\."+name+"\\(i32 "+item[1]+", "+item[2]+" ([^)]+)\\)"),"bitcast "+std::string(item[2])+" $1 to "+item[3]);
        text=std::regex_replace(text,std::regex("declare [^\\n]*@dx\\.op\\."+name+"\\([^\\n]*"),"");
    }
    return text;
}
std::string Compact(std::string text){
    text=std::regex_replace(text,std::regex(R"(%(\d+)\b)"),"%compat$1");
    for(const char* check:{"%compat3073 = select i1 %compat3072, i32 2, i32 15","%compat3075 = select i1 %compat3074, i32 8, i32 %compat3073","%compat3079 = mul i32 %compat3075, 4","%compat3082 = add i32 %compat3079, 1","%compat3085 = add i32 %compat3079, 2"})Check(text.find(check)!=std::string::npos?S_OK:E_INVALIDARG);
    std::map<unsigned,std::string> table;
    text=Replace(text,std::regex(R"(  (%[\w.]+) = getelementptr \[64 x i32\], \[64 x i32\]\* %dx\.v32\.x01, i32 0, i32 (\d+)\n  store i32 (%[\w.]+), i32\* \1, align 4\n)"),[&](const std::smatch& m){Check(table.emplace(std::stoul(m[2].str()),m[3].str()).second?S_OK:E_INVALIDARG);return "";});Check(table.size()==42?S_OK:E_INVALIDARG);
    unsigned loads=0;
    text=Replace(text,std::regex(R"(  (%[\w.]+) = getelementptr \[64 x i32\], \[64 x i32\]\* %dx\.v32\.x01, i32 0, i32 (%[\w.]+)\n  (%[\w.]+) = load i32, i32\* \1, align 4\n)"),[&](const std::smatch& m){
        Check(loads<3&&m[2].str()==std::array<const char*,3>{"%compat3079","%compat3082","%compat3085"}[loads]?S_OK:E_INVALIDARG);
        std::string c=std::to_string(loads),prefix="  %lookup"+c;unsigned i=loads++;
        return prefix+"_is2 = icmp eq i32 %compat3075, 2\n"+prefix+"_is8 = icmp eq i32 %compat3075, 8\n"+prefix+"_tail = select i1 %lookup"+c+"_is8, i32 "+table.at(32+i)+", i32 "+table.at(60+i)+"\n  "+m[3].str()+" = select i1 %lookup"+c+"_is2, i32 "+table.at(8+i)+", i32 %lookup"+c+"_tail\n";
    });Check(loads==3?S_OK:E_INVALIDARG);
    text=std::regex_replace(text,std::regex(R"(  %dx\.v32\.x01 = alloca \[64 x i32\], align 4\n)"),"");Check(text.find("%dx.v32.x01")==std::string::npos?S_OK:E_INVALIDARG);return text;
}
std::string ScalarConstants(std::string text){
    std::smatch table;Check(std::regex_search(text,table,std::regex(R"(@dx\.icb = internal addrspace\(5\) constant \[32 x float\] \[([^\n]+)\])"))?S_OK:E_INVALIDARG);
    std::vector<std::string> values;const auto entries=table[1].str();
    const std::regex entry(R"(float ([^,]+))");
    for(std::sregex_iterator i(entries.begin(),entries.end(),entry),end;i!=end;++i)values.push_back((*i)[1].str());
    Check(values.size()==32?S_OK:E_INVALIDARG);
    text.erase(size_t(table.position()),size_t(table.length()));unsigned loads=0;
    text=Replace(text,std::regex(R"(  %(\d+) = getelementptr \[32 x float\], \[32 x float\] addrspace\(5\)\* @dx\.icb, i32 0, i32 %(\d+)\n  %(\d+) = load float, float addrspace\(5\)\* %\1, align 4\n)"),[&](const std::smatch& m){
        ++loads;std::string prefix="%icb_"+m[1].str(),previous="0.000000e+00",out="  %"+m[1].str()+" = add i32 %"+m[2].str()+", 0\n";
        for(unsigned i=0;i<32;++i){std::string n=std::to_string(i),condition=prefix+"_is_"+n,value=i==31?"%"+m[3].str():prefix+"_value_"+n;
            out+="  "+condition+" = icmp eq i32 %"+m[1].str()+", "+n+"\n  "+value+" = select i1 "+condition+", float "+values[i]+", float "+previous+"\n";previous=value;}
        return out;
    });Check(loads==2&&text.find("@dx.icb")==std::string::npos?S_OK:E_INVALIDARG);return text;
}
}
extern "C" __declspec(dllexport) HRESULT WINAPI ConvertOwnedCompression(LPCWSTR compilerRoot,const BYTE* input,UINT32 length,UINT32 variant,BYTE* output,UINT32 capacity,UINT32* written){
    if(written)*written=0;
    if(!compilerRoot||!*compilerRoot||!input||length<32||length>1024*1024||variant>=4||!output||!capacity||capacity>1024*1024||!written)return E_INVALIDARG;
    static std::mutex serial;std::lock_guard<std::mutex> guard(serial);
    try{
        Api api(compilerRoot);void* data=nullptr;UINT32 size=0;LPWSTR diagnostics=nullptr;
        HRESULT hr=api.legacy->Convert(input,length,nullptr,&data,&size,&diagnostics);if(diagnostics)CoTaskMemFree(diagnostics);
        struct Memory {void* p;~Memory(){CoTaskMemFree(p);}} memory{data};Check(hr);if(!data||size>1024*1024)throw E_FAIL;
        auto raw=api.Blob(data,size);auto text=Normalize(api.Text(raw.Get()));
        if(variant==3)text=Canonical(ScalarConstants(text));
        auto shader=api.Assemble(text);
        if(variant==0){shader=api.Assemble(Canonical(api.Text(shader.Get())));shader=api.Optimize(shader.Get());}
        if(variant==3)shader=api.Optimize(shader.Get(),true);
        if(variant==0){shader=api.Assemble(Compact(api.Text(shader.Get())));shader=api.Optimize(shader.Get());}
        if(shader->GetBufferSize()>capacity)throw HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER);
        memcpy(output,shader->GetBufferPointer(),shader->GetBufferSize());*written=static_cast<UINT32>(shader->GetBufferSize());return S_OK;
    }catch(HRESULT hr){return hr;}catch(const std::bad_alloc&){return E_OUTOFMEMORY;}catch(...){return E_INVALIDARG;}
}
