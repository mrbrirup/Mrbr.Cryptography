"""Summarize the isolated, recorded Shamir comparison runs without altering libraries."""
from pathlib import Path
import csv
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "benchmarks/results/2026-11-04-secretsharingdotnet"


def read_run(name):
    files = list((OUT / name / "results").glob("*-report-full.json"))
    if len(files) != 1:
        raise RuntimeError(f"Expected one completed report for {name}, found {len(files)}")
    report = json.loads(files[0].read_text(encoding="utf-8-sig"))
    if any(not b.get("Statistics") for b in report["Benchmarks"]):
        raise RuntimeError(f"Incomplete benchmark in {name}")
    return report


def write_csv(name, rows):
    with (OUT / name).open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def normalize(benchmark, run):
    stats, memory = benchmark["Statistics"], benchmark["Memory"]
    return dict(Run=run, Method=benchmark["Method"], Parameters=benchmark["Parameters"],
                MeanUs=stats["Mean"] / 1000, ErrorUs=stats["ConfidenceInterval"]["Margin"] / 1000,
                OperationsPerSecond=1e9 / stats["Mean"], ManagedBytesPerOperation=memory["BytesAllocatedPerOperation"],
                Gen0=memory["Gen0Collections"], Gen1=memory["Gen1Collections"], Gen2=memory["Gen2Collections"])


def overlap(a, b):
    return a["MeanUs"] - a["ErrorUs"] <= b["MeanUs"] + b["ErrorUs"] and b["MeanUs"] - b["ErrorUs"] <= a["MeanUs"] + a["ErrorUs"]


def source_records(name):
    records = []
    for log in (OUT / name).glob("*.log"):
        records += re.findall(r"SUBJECT target=(.*?); sha256=([A-F0-9]+); runtime=(.*?)\r?$", log.read_text(encoding="utf-8-sig"), re.M)
    return sorted(set(records))


def main():
    reports = {name: read_run(name) for name in ["net11", "net10-target", "netstandard-target", "runtime"]}
    expected = dict(net11=48, **{"net10-target": 12, "netstandard-target": 12, "runtime": 16})
    for name, report in reports.items():
        if len(report["Benchmarks"]) != expected[name]:
            raise RuntimeError(f"Unexpected case count for {name}")
    normalized = {name: [normalize(b, name) for b in report["Benchmarks"]] for name, report in reports.items()}
    for name, rows in normalized.items():
        write_csv(name + "-operations.csv", rows)

    head = normalized["net11"]
    comparisons = []
    for parameters in sorted({r["Parameters"] for r in head}):
        by_method = {r["Method"]: r for r in head if r["Parameters"] == parameters}
        for operation in ["Create", "Reconstruct"]:
            ours = by_method["Mrbr" + operation]
            for backend in ["Big", "Secure"]:
                other = by_method["Upstream" + backend + operation]
                comparisons.append(dict(Scenario=parameters.replace("Scenario=", ""), Operation=operation, Backend=backend,
                    MrbrMeanUs=ours["MeanUs"], MrbrErrorUs=ours["ErrorUs"], UpstreamMeanUs=other["MeanUs"], UpstreamErrorUs=other["ErrorUs"],
                    TimeRatioUpstreamToMrbr=other["MeanUs"] / ours["MeanUs"], ConfidenceIntervalsOverlap=overlap(ours, other),
                    MrbrManagedBytesPerOperation=ours["ManagedBytesPerOperation"], UpstreamManagedBytesPerOperation=other["ManagedBytesPerOperation"],
                    MrbrOperationsPerSecond=ours["OperationsPerSecond"], UpstreamOperationsPerSecond=other["OperationsPerSecond"]))
    write_csv("library-comparison.csv", comparisons)

    net11_index = {(r["Parameters"], r["Method"]): r for r in head}
    target_rows = []
    for name in ["net10-target", "netstandard-target"]:
        for row in normalized[name]:
            modern = net11_index[row["Parameters"], row["Method"]]
            target_rows.append(dict(OriginalTarget=name, Scenario=row["Parameters"].replace("Scenario=", ""), Method=row["Method"],
                OriginalTargetMeanUs=row["MeanUs"], OriginalTargetErrorUs=row["ErrorUs"], Net11TargetMeanUs=modern["MeanUs"], Net11TargetErrorUs=modern["ErrorUs"],
                OriginalToNet11TimeRatio=row["MeanUs"] / modern["MeanUs"], Net11TimeChangePercent=100 * (modern["MeanUs"] / row["MeanUs"] - 1),
                ConfidenceIntervalsOverlap=overlap(row, modern), OriginalManagedBytesPerOperation=row["ManagedBytesPerOperation"], Net11ManagedBytesPerOperation=modern["ManagedBytesPerOperation"]))
    write_csv("target-comparison.csv", target_rows)

    runtime_index = {}
    for b in reports["runtime"]["Benchmarks"]:
        display = b["DisplayInfo"]
        runtime = "Runtime10" if ": Runtime10(" in display else "Runtime11" if ": Runtime11(" in display else None
        if runtime is None:
            raise RuntimeError("Unrecognized runtime job: " + display)
        runtime_index[b["Parameters"], b["Method"], runtime] = normalize(b, runtime)
    runtime_rows = []
    for parameters, method in sorted({(k[0], k[1]) for k in runtime_index}):
        old, new = runtime_index[parameters, method, "Runtime10"], runtime_index[parameters, method, "Runtime11"]
        runtime_rows.append(dict(Parameters=parameters, Method=method, Runtime10MeanUs=old["MeanUs"], Runtime10ErrorUs=old["ErrorUs"],
            Runtime11MeanUs=new["MeanUs"], Runtime11ErrorUs=new["ErrorUs"], RuntimeSpeedup10To11=old["MeanUs"] / new["MeanUs"],
            Runtime11TimeChangePercent=100 * (new["MeanUs"] / old["MeanUs"] - 1), ConfidenceIntervalsOverlap=overlap(old, new),
            Runtime10ManagedBytesPerOperation=old["ManagedBytesPerOperation"], Runtime11ManagedBytesPerOperation=new["ManagedBytesPerOperation"]))
    write_csv("runtime-comparison.csv", runtime_rows)

    records = {name: source_records(name) for name in reports}
    frozen = OUT / "provenance/runtime-control/SecretSharingDotNet.dll"
    frozen_hash = hashlib.sha256(frozen.read_bytes()).hexdigest().upper()
    if len({r[1] for r in records["runtime"]}) != 1 or any(r[1] != frozen_hash for r in records["runtime"]):
        raise RuntimeError("Runtime experiment did not use the same subject binary")
    if any(r[0] != ".NETCoreApp,Version=v10.0" for r in records["runtime"]):
        raise RuntimeError("Runtime experiment retargeted the subject")
    ours = [r for r in head if r["Method"].startswith("Mrbr")]
    if len(ours) != 16 or any(r["ManagedBytesPerOperation"] != 0 for r in ours):
        raise RuntimeError("Mrbr allocation regression in comparison")
    summary = dict(CaseCounts={name: len(r["Benchmarks"]) for name, r in reports.items()},
                   MrbrZeroAllocationCases=len(ours), LoadedSubjects=records, FrozenRuntimeSubjectSHA256=frozen_hash,
                   HostEnvironments={name: r["HostEnvironmentInfo"] for name, r in reports.items()})
    (OUT / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
