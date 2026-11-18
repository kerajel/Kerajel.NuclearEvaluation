#!/usr/bin/env python3
"""Export one CSV per annual source in the EPA RadNet snapshot."""

import argparse
import os
import tempfile
from pathlib import Path

import duckdb

MIN_OBSERVATIONS = 10_000_000

OBSERVATIONS = """
    SELECT o.id, o.source_file_id, o.source_row, o.observed_at, o.status,
           o.exposure_mr_h, o.dose_nsv_h, o.r02, o.r03, o.r04, o.r05,
           o.r06, o.r07, o.r08, o.r09, s.id AS station_id, s.name AS station_name,
           s.state, f.year, f.file_name
    FROM observations o
    JOIN source_files f ON f.id = o.source_file_id
    JOIN stations s ON s.id = f.station_id
"""


def export_annual_files(connection, sources, output):
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="radnet-export-", dir=output.parent) as work:
        partitions = Path(work) / "partitions"
        staged = Path(work) / "annual"
        staged.mkdir()
        count = connection.execute(
            f"""
                COPY ({OBSERVATIONS}) TO ? (
                    FORMAT CSV, HEADER, PARTITION_BY (source_file_id), WRITE_PARTITION_COLUMNS true
                )
            """,
            [str(partitions)],
        ).fetchone()[0]
        expected = sum(rows for _, rows in sources)
        if count != expected:
            raise ValueError(f"Exported {count:,} rows from {expected:,} observations")

        for source_id, rows in sources:
            target = staged / f"{source_id}.csv"
            partition = partitions / f"source_file_id={source_id}"
            files = list(partition.glob("*.csv"))
            if rows == 0 and not files:
                connection.execute(
                    f"COPY ({OBSERVATIONS} LIMIT 0) TO ? (FORMAT CSV, HEADER)", [str(target)]
                )
            elif rows > 0 and len(files) == 1:
                files[0].rename(target)
                partition.rmdir()
            else:
                raise ValueError(f"Expected one CSV for source {source_id} with {rows:,} observations")

        partitions.rmdir()
        size = sum(path.stat().st_size for path in staged.iterdir())
        os.rename(staged, output)
        return count, size


def main():
    parser = argparse.ArgumentParser(
        description=__doc__,
        epilog="Source: https://www.epa.gov/radnet/radnet-csv-file-downloads",
    )
    parser.add_argument("--snapshot", type=Path, required=True, help="Input radnet.duckdb snapshot")
    parser.add_argument(
        "--output", type=Path, required=True, help="New destination directory outside the repository"
    )
    args = parser.parse_args()

    snapshot, output = args.snapshot.resolve(), args.output.resolve()
    if not snapshot.is_file():
        parser.error(f"Snapshot does not exist: {snapshot}")
    if output.exists():
        parser.error(f"The output directory must be new: {output}")

    with duckdb.connect(str(snapshot), read_only=True, config={"threads": 1}) as connection:
        sources = connection.execute("SELECT id, row_count FROM source_files ORDER BY id").fetchall()
        expected = sum(rows for _, rows in sources)
        if expected < MIN_OBSERVATIONS:
            parser.error(f"The snapshot has {expected:,} observations. At least {MIN_OBSERVATIONS:,} are required")

        count, size = export_annual_files(connection, sources, output)

    print(f"Exported {count:,} EPA RadNet observations in {len(sources):,} files to {output} ({size:,} bytes)")


if __name__ == "__main__":
    main()
