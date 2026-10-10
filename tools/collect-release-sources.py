"""Collect pinned upstream source archives for private release review.

This produces source inputs, not a legal approval or a game/runtime bundle.
"""
import argparse
import hashlib
import json
from pathlib import Path
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]

def sha(path):
    with path.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def collect(destination):
    destination.mkdir(parents=True, exist_ok=True)
    rows = json.loads((ROOT/'release/SOURCE-INPUTS.json').read_text(encoding='utf-8'))
    results = []
    for row in rows:
        target = destination/(row['name']+'-source.zip')
        if not target.exists():
            temporary = target.with_suffix('.partial')
            request = urllib.request.Request(row['url'], headers={'User-Agent':'Launcher-Source-Review'})
            with urllib.request.urlopen(request, timeout=90) as response, temporary.open('wb') as output:
                while block := response.read(1024*1024):
                    output.write(block)
            if sha(temporary) != row['sha256']:
                raise ValueError('Source archive hash mismatch: '+row['name'])
            temporary.replace(target)
        if sha(target) != row['sha256']:
            raise ValueError('Cached source archive hash mismatch: '+row['name'])
        with zipfile.ZipFile(target) as archive:
            if archive.testzip() is not None:
                raise ValueError('Corrupt source archive: '+row['name'])
        results.append({**row, 'file':target.name, 'bytes':target.stat().st_size})
        print('Verified '+target.name, flush=True)
    (destination/'source-inputs.json').write_text(json.dumps(results,indent=2)+'\n',encoding='utf-8')
    return results

if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('destination',type=Path)
    collect(parser.parse_args().destination)
