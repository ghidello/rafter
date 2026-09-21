"""Run a process test with a deadline independent of .NET and optional macOS stacks."""

import argparse
import json
import math
import os
from pathlib import Path
import signal
import subprocess
import sys
import time
import uuid


def read_stages(path, printed):
    if not path.exists():
        return printed, None
    lines = path.read_text(encoding="utf-8").splitlines(keepends=True)
    process_id = None
    for index, line in enumerate(lines):
        if not line.endswith("\n"):
            break
        stage = json.loads(line)
        process_id = stage["processId"]
        if index >= printed:
            print(f"Test stage: {line.rstrip()}", flush=True)
            printed = index + 1
    return printed, process_id


def capture_stacks(directory, process_id, launcher_id, deadline):
    if sys.platform != "darwin":
        print("Native stack sampling is available only on macOS.", flush=True)
        return
    remaining = deadline - time.monotonic()
    if remaining <= 0:
        return
    snapshot = subprocess.run(
        ["/bin/ps", "-axo", "pid,ppid,pgid,state,comm"],
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
        timeout=min(5, remaining), check=False,
    )
    (directory / "processes.txt").write_text(snapshot.stdout, encoding="utf-8")
    if process_id is None:
        print("The test has not reported its PID; saved the process table only.", flush=True)
        return
    parents = {}
    for line in snapshot.stdout.splitlines()[1:]:
        fields = line.split(maxsplit=4)
        if len(fields) == 5 and fields[0].isdigit() and fields[1].isdigit():
            parents[int(fields[0])] = int(fields[1])
    ancestor = process_id
    visited = set()
    while ancestor != launcher_id and ancestor in parents and ancestor not in visited:
        visited.add(ancestor)
        ancestor = parents[ancestor]
    if ancestor != launcher_id:
        print("The reported PID is no longer in the owned test tree; skipping stack sampling.", flush=True)
        return
    remaining = deadline - time.monotonic()
    if remaining <= 0:
        return
    print(f"Sampling test PID {process_id} into {directory}", flush=True)
    subprocess.run(
        ["/usr/bin/sample", str(process_id), "2", "-file", str(directory / "sample.txt")],
        timeout=min(8, remaining), check=False,
    )


def terminate_tree(process):
    # The POSIX group was created by this wrapper, so it cannot include the Actions runner.
    if os.name == "nt":
        if process.poll() is not None:
            return
        subprocess.run(
            ["taskkill", "/PID", str(process.pid), "/T", "/F"], timeout=10, check=False,
        )
    else:
        # Descendants can retain the group after its leader exits or the test host crashes.
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
    process.wait(timeout=5)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--timeout", type=float, default=60)
    parser.add_argument("--sample-after", type=float, default=12)
    parser.add_argument("command", nargs=argparse.REMAINDER)
    arguments = parser.parse_args()
    command = arguments.command
    if command and command[0] == "--":
        command = command[1:]
    if (not command or not math.isfinite(arguments.timeout) or not math.isfinite(arguments.sample_after)
            or not 0 < arguments.sample_after < arguments.timeout):
        parser.error("Provide a command and 0 < sample-after < timeout.")

    directory = Path("artifacts/test-results/process-tree") / uuid.uuid4().hex
    directory.mkdir(parents=True)
    trace = directory.resolve() / "stages.jsonl"
    environment = os.environ.copy()
    environment["RAFTER_PROCESS_TREE_TRACE"] = str(trace)
    print(f"Process-test diagnostics: {directory}", flush=True)
    process = subprocess.Popen(command, env=environment, start_new_session=os.name != "nt")
    started = time.monotonic()
    deadline = started + arguments.timeout
    sampled = False
    printed = 0
    try:
        while process.poll() is None:
            printed, process_id = read_stages(trace, printed)
            elapsed = time.monotonic() - started
            if elapsed >= arguments.timeout:
                print("Independent process-test deadline exceeded; terminating the owned process tree.", flush=True)
                return 124
            if not sampled and elapsed >= arguments.sample_after:
                sampled = True
                try:
                    capture_stacks(directory, process_id, process.pid, deadline)
                except (OSError, subprocess.TimeoutExpired) as error:
                    print(f"Stack capture failed: {error}", flush=True)
            time.sleep(0.1)
        read_stages(trace, printed)
        return process.returncode if process.returncode >= 0 else 128 - process.returncode
    finally:
        terminate_tree(process)


def cancel(_signal, _frame):
    raise KeyboardInterrupt


if __name__ == "__main__":
    signal.signal(signal.SIGTERM, cancel)
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        sys.exit(130)
