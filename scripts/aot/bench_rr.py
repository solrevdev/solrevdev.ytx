#!/usr/bin/env python3
"""Round-robin process benchmark.

Usage: bench_rr.py ROUNDS WARMUP label=/path/to/binary [label=/path ...] -- [args for every binary]

Every binary runs once per round, in turn, so network drift and thermal changes hit all of them
equally. Reports wall time, CPU time (user + sys) and peak RSS for each binary.

Set BENCH_STDIN to pipe that text to each process (for the stdin JSON path).
POSIX only (macOS and Linux): it uses os.wait4 for per-process resource usage.
"""
import os
import statistics
import subprocess
import sys
import time

# ru_maxrss is bytes on macOS but kilobytes on Linux.
RSS_TO_MB = 1 / 1048576 if sys.platform == "darwin" else 1 / 1024


def run_once(binary, args, stdin_data):
    start = time.perf_counter()
    p = subprocess.Popen(
        [binary] + args,
        stdin=subprocess.PIPE if stdin_data else subprocess.DEVNULL,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    if stdin_data:
        p.stdin.write(stdin_data.encode())
        p.stdin.close()
    _, status, ru = os.wait4(p.pid, 0)
    wall = (time.perf_counter() - start) * 1000
    cpu = (ru.ru_utime + ru.ru_stime) * 1000
    return wall, cpu, ru.ru_maxrss * RSS_TO_MB, os.waitstatus_to_exitcode(status)


def main():
    if "--" not in sys.argv or len(sys.argv) < 5:
        sys.exit(__doc__)
    rounds, warmup = int(sys.argv[1]), int(sys.argv[2])
    sep = sys.argv.index("--")
    variants = [v.split("=", 1) for v in sys.argv[3:sep]]
    args = sys.argv[sep + 1:]
    stdin_data = os.environ.get("BENCH_STDIN")

    for label, binary in variants:
        if not os.access(binary, os.X_OK):
            sys.exit(f"{label}: not an executable file: {binary}")

    for _ in range(warmup):
        for _, binary in variants:
            run_once(binary, args, stdin_data)

    results = {label: [] for label, _ in variants}
    for _ in range(rounds):
        for label, binary in variants:
            results[label].append(run_once(binary, args, stdin_data))

    print(f"args: {' '.join(args) or '(none)'}  stdin={stdin_data!r}  rounds={rounds} warmup={warmup}")
    for label, rs in results.items():
        wall = [r[0] for r in rs]
        cpu = [r[1] for r in rs]
        print(
            f"  {label:<22} med={statistics.median(wall):7.1f} mean={statistics.mean(wall):7.1f} "
            f"min={min(wall):7.1f} max={max(wall):7.1f} sd={statistics.pstdev(wall):6.1f} ms  "
            f"cpu med={statistics.median(cpu):7.1f} ms  peakRSS={max(r[2] for r in rs):6.1f} MB  "
            f"exit={sorted({r[3] for r in rs})}"
        )


if __name__ == "__main__":
    main()
