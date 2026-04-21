#!/usr/bin/env python3
import argparse
import csv
import math
from collections import defaultdict


def percentile(sorted_values, p):
    if not sorted_values:
        return 0.0
    if len(sorted_values) == 1:
        return float(sorted_values[0])
    rank = (len(sorted_values) - 1) * p
    low = math.floor(rank)
    high = math.ceil(rank)
    if low == high:
        return float(sorted_values[int(rank)])
    low_value = sorted_values[low]
    high_value = sorted_values[high]
    return float(low_value + (high_value - low_value) * (rank - low))


def main():
    parser = argparse.ArgumentParser(description="Summarize JMeter CSV results and enforce latency threshold.")
    parser.add_argument("--input", required=True, help="Path to JMeter CSV .jtl file")
    parser.add_argument("--output", required=True, help="Path to markdown summary output")
    parser.add_argument("--threshold-ms", type=float, default=5000.0, help="NFR latency threshold in milliseconds")
    args = parser.parse_args()

    per_label = defaultdict(list)
    success_per_label = defaultdict(int)
    total_per_label = defaultdict(int)

    with open(args.input, "r", encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        required = {"label", "elapsed", "success"}
        missing = required.difference(reader.fieldnames or [])
        if missing:
            raise ValueError(f"Missing required JTL columns: {', '.join(sorted(missing))}")

        for row in reader:
            label = row["label"].strip()
            if not label:
                continue
            elapsed = float(row["elapsed"])
            ok = row["success"].strip().lower() == "true"

            per_label[label].append(elapsed)
            total_per_label[label] += 1
            if ok:
                success_per_label[label] += 1

    lines = []
    lines.append("# JMeter Load Test Summary")
    lines.append("")
    lines.append(f"NFR target (NFR-P03): p95 latency < {args.threshold_ms:.0f} ms with zero failed requests.")
    lines.append("")
    lines.append("| Endpoint | Samples | Errors | Avg (ms) | p95 (ms) | Max (ms) | NFR-P03 |")
    lines.append("|---|---:|---:|---:|---:|---:|---|")

    overall_ok = True
    for label in sorted(per_label.keys()):
        samples = sorted(per_label[label])
        count = total_per_label[label]
        ok_count = success_per_label[label]
        err_count = count - ok_count

        avg_ms = sum(samples) / count if count else 0.0
        p95_ms = percentile(samples, 0.95)
        max_ms = max(samples) if samples else 0.0

        meets = (err_count == 0) and (p95_ms < args.threshold_ms)
        overall_ok = overall_ok and meets

        lines.append(
            f"| {label} | {count} | {err_count} | {avg_ms:.2f} | {p95_ms:.2f} | {max_ms:.2f} | {'PASS' if meets else 'FAIL'} |"
        )

    lines.append("")
    lines.append(f"Overall result: {'PASS' if overall_ok else 'FAIL'}")

    with open(args.output, "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines) + "\n")

    if not overall_ok:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
