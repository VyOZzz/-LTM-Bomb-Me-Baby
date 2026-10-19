"""Average experiments/summary.csv by sync mode and client count, then compare modes.

Example:
    python scripts/summarize_experiments.py
    python scripts/summarize_experiments.py --summary experiments/summary.csv --labels baseline optimized

Prints a Markdown table: mean of the repeated runs for each (mode, clients) pair,
plus the byte saving of optimized relative to baseline for the same client count.
"""
import argparse
import csv
from collections import defaultdict

METRICS = [
    ("recv_bytes_per_client_s", "Byte nhận/client/s"),
    ("state_update_avg_bytes", "Byte TB/STATE_UPDATE"),
    ("recv_msgs_per_client_s", "Message nhận/client/s"),
    ("rtt_mean_ms", "RTT TB (ms)"),
    ("rtt_p95_ms", "RTT p95 (ms)"),
    ("processing_mean_us", "processingMicros TB (µs)"),
]


def mean(values):
    return sum(values) / len(values) if values else None


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--summary", default="experiments/summary.csv")
    parser.add_argument("--labels", nargs="*", help="only use rows with these labels")
    args = parser.parse_args()

    with open(args.summary, newline="", encoding="utf-8") as f:
        rows = [r for r in csv.DictReader(f) if not args.labels or r["label"] in args.labels]

    groups = defaultdict(list)
    for r in rows:
        groups[(r["sync_mode"], int(r["clients"]))].append(r)

    header = ["Chế độ", "Client", "Số lượt", "Map", "Lỗi"] + [name for _, name in METRICS]
    print("| " + " | ".join(header) + " |")
    print("|" + " --- |" * len(header))
    averages = {}
    for (mode, clients), runs in sorted(groups.items(), key=lambda kv: (kv[0][1], kv[0][0])):
        avg = {key: mean([float(r[key]) for r in runs if r[key] != ""]) for key, _ in METRICS}
        averages[(mode, clients)] = avg
        maps = ", ".join(sorted({r["map_id"] for r in runs}))
        errors = sum(int(r["errors"] or 0) for r in runs)
        cells = [mode, clients, len(runs), maps, errors] + [
            "" if avg[key] is None else f"{avg[key]:.2f}" for key, _ in METRICS]
        print("| " + " | ".join(str(c) for c in cells) + " |")

    print()
    print("| Client | Tiết kiệm byte nhận/client/s | Tiết kiệm byte/STATE_UPDATE |")
    print("| --- | --- | --- |")
    for clients in sorted({c for _, c in averages}):
        base, opt = averages.get(("baseline", clients)), averages.get(("optimized", clients))
        if not base or not opt:
            continue
        saving = lambda key: (base[key] - opt[key]) / base[key] * 100
        print(f"| {clients} | {saving('recv_bytes_per_client_s'):.1f}% | {saving('state_update_avg_bytes'):.1f}% |")


if __name__ == "__main__":
    main()
