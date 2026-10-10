"""Inspect an MSIX without extracting it. Packaging checks are not legal clearance."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import zipfile

ROOT = Path(__file__).resolve().parents[1]
ALLOWED_ROOT = {
    'clrcompression.dll', 'TextureConvert.dll', 'SpiderManInstaller.dll',
    'SpiderManInstaller.exe', 'SteamFiles.json', 'LanguagePacks.json',
    'WifiTransfer.html', 'resources.pri', 'AppxManifest.xml', 'AppxBlockMap.xml',
    '[Content_Types].xml', 'AppxSignature.p7x', 'THIRD-PARTY-NOTICES.txt'
}
ALLOWED_ART = {'Logo.png', 'SmallLogo.png', 'StoreLogo.png', 'SplashScreen.png'}
REQUIRED_NOTICES = {
    'THIRD-PARTY-NOTICES.txt', 'Licenses/DepotDownloader-GPL-2.0.txt',
    'Licenses/SteamKit-LGPL.txt', 'Licenses/SteamKit-Notices.txt',
    'Licenses/AssetConverter-GPL-3.0.txt', 'Licenses/Apache-2.0.txt',
    'Licenses/protobuf-net.txt', 'Licenses/QRCoder-MIT.txt',
    'Licenses/DirectXTex-MIT.txt', 'Licenses/K4os-LICENSE.txt', 'Licenses/LZ4-BSD.txt',
    'Licenses/Microsoft-UWP.txt', 'Licenses/Microsoft-UWP-ThirdParty.txt'
}

def audit(path):
    errors = []
    names = set()
    case_names = set()
    total = 0
    with zipfile.ZipFile(path) as package:
        for item in package.infolist():
            name = item.filename
            parts = PurePosixPath(name).parts
            if item.is_dir():
                continue
            if '\\' in name or not parts or name.startswith('/') or any(x in ('..', '.') or ':' in x for x in parts):
                errors.append('Unsafe package entry: ' + name)
            if name.casefold() in case_names:
                errors.append('Duplicate package entry: ' + name)
            case_names.add(name.casefold()); names.add(name); total += item.file_size
            allowed = name in ALLOWED_ROOT
            allowed |= len(parts) == 2 and parts[0] == 'Assets' and parts[1] in ALLOWED_ART
            allowed |= len(parts) == 2 and parts[0] == 'Licenses' and name.endswith('.txt')
            allowed |= len(parts) == 2 and parts[0] == 'AssetRecipes' and name.endswith('.tsv')
            allowed |= name == 'AppxMetadata/CodeIntegrity.cat'
            if not allowed:
                errors.append('Unreviewed or private package entry: ' + name)
            if name.startswith('AssetRecipes/') and name.endswith('.tsv'):
                if item.file_size > 10 * 1024 * 1024:
                    errors.append('Oversized recipe: ' + name)
                else:
                    text = package.read(item).decode('utf-8-sig')
                    if '\x00' in text or any(len(field) > 80 for field in text.split()):
                        errors.append('Recipe is not bounded numeric/identifier metadata: ' + name)
        for required in sorted(REQUIRED_NOTICES - names):
            errors.append('Missing notice: ' + required)
    with open(path, 'rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    return {'package': Path(path).name, 'sha256': digest, 'entries': len(names),
            'uncompressed_bytes': total, 'packaging_checks_pass': not errors, 'errors': errors}

def release_blockers(policy):
    blockers = [name for name, item in policy['required_reviews'].items()
                if item.get('complete') is not True or not item.get('evidence', '').strip()]
    if policy.get('public_binary_approved') is not True:
        blockers.append('public_binary_approval')
    return blockers

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('package', type=Path)
    parser.add_argument('--report', type=Path)
    parser.add_argument('--packaging-only', action='store_true', help='For private validation; does not approve a release')
    args = parser.parse_args()
    result = audit(args.package)
    result['release_blockers'] = release_blockers(json.loads((ROOT/'release/policy.json').read_text(encoding='utf-8')))
    result['public_release_ready'] = result['packaging_checks_pass'] and not result['release_blockers']
    output = json.dumps(result, indent=2)
    if args.report:
        args.report.write_text(output, encoding='utf-8')
    print(output)
    return 0 if result['packaging_checks_pass'] and (args.packaging_only or result['public_release_ready']) else 1

if __name__ == '__main__':
    raise SystemExit(main())
