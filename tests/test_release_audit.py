import importlib.util
from pathlib import Path
import tempfile
import unittest
import zipfile

spec = importlib.util.spec_from_file_location('audit', Path(__file__).resolve().parents[1]/'tools/audit_release.py')
audit = importlib.util.module_from_spec(spec); spec.loader.exec_module(audit)

class PackageChecks(unittest.TestCase):
    def inspect(self, extra=(), omit=None):
        with tempfile.TemporaryDirectory(prefix='LauncherAuditTests-') as folder:
            path = Path(folder)/'test.msix'
            with zipfile.ZipFile(path, 'w') as z:
                for name in sorted(audit.REQUIRED_NOTICES):
                    if name != omit: z.writestr(name, 'synthetic notice')
                for name, body in extra: z.writestr(name, body)
            return audit.audit(path)
    def test_required_notices(self):
        self.assertTrue(self.inspect()['packaging_checks_pass'])
        self.assertFalse(self.inspect(omit='Licenses/SteamKit-LGPL.txt')['packaging_checks_pass'])
    def test_private_runtime_is_rejected(self):
        for name in ['PreparationPayload/SpD12.dll', 'ModulePatch.json', 'asset_archive/g00s000', 'signing.pfx', 'SpArgs32.dll']:
            with self.subTest(name=name): self.assertFalse(self.inspect([(name,b'private')])['packaging_checks_pass'])
    def test_recipes_must_be_metadata(self):
        self.assertTrue(self.inspect([('AssetRecipes/test.tsv','ASSETGEN_V1\t1\t'+('a'*64))])['packaging_checks_pass'])
        self.assertFalse(self.inspect([('AssetRecipes/test.tsv','a'*1000)])['packaging_checks_pass'])
    def test_paths_and_duplicates(self):
        self.assertFalse(self.inspect([('../outside','x')])['packaging_checks_pass'])
        self.assertFalse(self.inspect([('SteamFiles.json','x'),('steamfiles.json','x')])['packaging_checks_pass'])
    def test_packaging_is_not_release_approval(self):
        self.assertEqual(audit.release_blockers({'public_binary_approved':False,'required_reviews':{'test':{'complete':False,'evidence':'pending'}}}), ['test','public_binary_approval'])

if __name__ == '__main__': unittest.main()
