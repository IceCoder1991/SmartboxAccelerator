import json
import shutil
import subprocess
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CLI = ROOT / "tools/smartboxx-cli/smartboxx"
CLIENT = ROOT / "clients/golden-client"

class CliTests(unittest.TestCase):
    def tearDown(self): shutil.rmtree(CLIENT, ignore_errors=True)

    def run_cli(self, *args):
        return subprocess.run([str(CLI), *args], cwd=ROOT, text=True, capture_output=True)

    def test_generation_matches_golden_and_validates(self):
        result = self.run_cli("new-client", "golden-client", "--name", "Golden Client", "--industry", "insurance", "--cloud", "azure", "--identity", "entra-id", "--ai-providers", "azure-openai,anthropic", "--storage", "azure-blob", "--vector-store", "postgresql", "--environments", "local,demo")
        self.assertEqual(result.returncode, 0, result.stderr)
        actual = json.loads((CLIENT / "client.json").read_text())
        golden = json.loads((ROOT / "tests/cli/golden/client.json").read_text())
        self.assertEqual(actual, golden)
        self.assertEqual(self.run_cli("validate-client", str(CLIENT)).returncode, 0)

    def test_rejects_traversal_and_overwrite(self):
        self.assertNotEqual(self.run_cli("new-client", "../escape").returncode, 0)
        args=("new-client", "golden-client", "--name", "Golden", "--industry", "x", "--cloud", "local", "--identity", "synthetic", "--ai-providers", "synthetic", "--storage", "filesystem", "--vector-store", "none")
        self.assertEqual(self.run_cli(*args).returncode, 0)
        self.assertNotEqual(self.run_cli(*args).returncode, 0)

    def test_demo_reset_is_deterministic(self):
        args=("new-client", "golden-client", "--name", "Golden", "--industry", "x", "--cloud", "local", "--identity", "synthetic", "--ai-providers", "synthetic", "--storage", "filesystem", "--vector-store", "none")
        self.assertEqual(self.run_cli(*args).returncode, 0)
        self.assertEqual(self.run_cli("seed-demo", str(CLIENT)).returncode, 0)
        before={p.relative_to(CLIENT/'demo'):p.read_bytes() for p in (CLIENT/'demo').rglob('*') if p.is_file()}
        self.assertEqual(self.run_cli("reset-demo", str(CLIENT)).returncode, 0)
        after={p.relative_to(CLIENT/'demo'):p.read_bytes() for p in (CLIENT/'demo').rglob('*') if p.is_file()}
        self.assertEqual(before, after)

if __name__ == "__main__": unittest.main()
