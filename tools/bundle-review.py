"""Bundle an exact committed launcher snapshot plus pinned source inputs, privately."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import zipfile

ROOT=Path(__file__).resolve().parents[1]
def digest(path):
    with path.open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('destination',type=Path)
    p.add_argument('--revision',required=True)
    p.add_argument('--package',required=True,type=Path)
    a=p.parse_args(); out=a.destination.resolve(); out.mkdir(parents=True,exist_ok=True)
    revision=subprocess.check_output(['git','rev-parse','--verify',a.revision+'^{commit}'],cwd=ROOT,text=True).strip()
    spec=importlib.util.spec_from_file_location('collector',ROOT/'tools/collect-release-sources.py')
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    vendors=module.collect(out/'upstream')
    launcher=out/'launcher-source.zip'
    subprocess.run(['git','archive','--format=zip','--output='+str(launcher),revision],cwd=ROOT,check=True)
    source_files=[]
    with zipfile.ZipFile(launcher) as z:
        for item in z.infolist():
            if item.is_dir(): continue
            if Path(item.filename).suffix.lower() in {'.dll','.exe','.pfx','.msix','.appx','.save','.dsar'}:
                raise ValueError('Unexpected launcher source entry: '+item.filename)
            data=z.read(item)
            source_files.append({'path':item.filename,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
    (out/'launcher-source-files.json').write_text(json.dumps(source_files,indent=2)+'\n',encoding='utf-8')
    record={'private_review_only':True,'public_release_approved':False,'launcher_revision':revision,
            'reference_package':a.package.name,'reference_package_sha256':digest(a.package),
            'launcher_archive_sha256':digest(launcher),'upstream_archives':vendors,
            'scope':'Launcher/converter and upstream sources only. No game/runtime payload. See redistribution review; collection is not legal clearance.'}
    (out/'BUNDLE.json').write_text(json.dumps(record,indent=2)+'\n',encoding='utf-8')
    package=out/'SpiderMan-Launcher-Source-Review.zip'
    with zipfile.ZipFile(package,'w',zipfile.ZIP_DEFLATED) as z:
        for file in [launcher,out/'launcher-source-files.json',out/'BUNDLE.json',out/'upstream/source-inputs.json']:
            z.write(file,file.relative_to(out).as_posix())
        for row in vendors:
            file=out/'upstream'/row['file'];z.write(file,'upstream/'+file.name)
        for name in ['REBUILD-FROM-SOURCE.md','REDISTRIBUTION-REVIEW.md','SOURCE-INPUTS.json']:
            z.write(ROOT/'release'/name,'review/release/'+name)
        for name in ['collect-release-sources.py','bundle-review.py']:
            z.write(ROOT/'tools'/name,'review/tools/'+name)
    print(json.dumps({'bundle':str(package),'bytes':package.stat().st_size,'sha256':digest(package)},indent=2))
if __name__=='__main__': main()
