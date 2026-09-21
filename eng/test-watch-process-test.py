"""Integration checks for the independent process-test watchdog."""

import csv
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time
import unittest


ROOT = Path(__file__).resolve().parent.parent
WATCHDOG = ROOT / "eng/watch-process-test.py"


class WatchdogTests(unittest.TestCase):
    def setUp(self):
        self.scratch_root = (ROOT / "artifacts/watchdog-tests").resolve()
        self.scratch_root.mkdir(parents=True, exist_ok=True)
        self.work = Path(tempfile.mkdtemp(dir=self.scratch_root)).resolve()

    def tearDown(self):
        if self.work.parent != self.scratch_root:
            raise RuntimeError("Refusing cleanup outside the watchdog test directory.")
        shutil.rmtree(self.work)

    def run_watchdog(self, script, timeout="10", sample_after="9"):
        return subprocess.run(
            [sys.executable, str(WATCHDOG), "--timeout", timeout, "--sample-after", sample_after,
             "--", sys.executable, "-c", script],
            cwd=self.work, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=20, check=False,
        )

    def test_preserves_success_and_failure(self):
        for exit_code in (0, 7):
            with self.subTest(exit_code=exit_code):
                result = self.run_watchdog(f"print('child output'); raise SystemExit({exit_code})")
                self.assertEqual(exit_code, result.returncode, result.stdout)
                self.assertIn("child output", result.stdout)

    def test_reads_only_complete_trace_records(self):
        result = self.run_watchdog("""
import json, os, time
record = json.dumps({"processId": os.getpid(), "stage": "ready"})
with open(os.environ["RAFTER_PROCESS_TREE_TRACE"], "w", encoding="utf-8") as trace:
    trace.write(record[:10])
    trace.flush()
    time.sleep(0.3)
    trace.write(record[10:] + "\\n")
""")
        self.assertEqual(0, result.returncode, result.stdout)
        self.assertEqual(1, result.stdout.count("Test stage:"), result.stdout)

    def test_deadline_terminates_the_owned_tree(self):
        started = time.monotonic()
        result = self.run_watchdog("""
import json, os, subprocess, sys, time
from pathlib import Path
child = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(15)"])
Path("pids.json").write_text(json.dumps([os.getpid(), child.pid]), encoding="utf-8")
Path(os.environ["RAFTER_PROCESS_TREE_TRACE"]).write_text(
    json.dumps({"processId": os.getpid(), "stage": "waiting"}) + "\\n", encoding="utf-8")
time.sleep(15)
""", timeout="2", sample_after="0.5")
        self.assertEqual(124, result.returncode, result.stdout)
        self.assertLess(time.monotonic() - started, 8, result.stdout)
        self.assertIn("Independent process-test deadline exceeded", result.stdout)
        for process_id in json.loads((self.work / "pids.json").read_text(encoding="utf-8")):
            if os.name == "nt":
                listing = subprocess.run(
                    ["tasklist", "/FI", f"PID eq {process_id}", "/FO", "CSV", "/NH"],
                    text=True, stdout=subprocess.PIPE, timeout=5, check=True,
                )
                rows = csv.reader(listing.stdout.splitlines())
                self.assertFalse(any(len(row) > 1 and row[1] == str(process_id) for row in rows))
            else:
                listing = subprocess.run(
                    ["ps", "-p", str(process_id), "-o", "state="],
                    text=True, stdout=subprocess.PIPE, timeout=5, check=False,
                )
                state = listing.stdout.strip()
                self.assertTrue(not state or state.startswith("Z"), f"PID {process_id} still running: {state}")


if __name__ == "__main__":
    unittest.main()
