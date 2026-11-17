#!/usr/bin/env python3
"""Build a local DuckDB snapshot from EPA's historical RadNet station ZIPs."""

import argparse
import csv
import hashlib
import io
import os
import re
import tempfile
import time
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass
from datetime import datetime, timezone
from html.parser import HTMLParser
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import urljoin, urlparse
from urllib.request import urlopen
from zipfile import ZipFile

INDEX = "https://www.epa.gov/radnet/radnet-csv-file-downloads"
LICENSE = "https://edg.epa.gov/EPA_Data_License.html"
MAX_ARCHIVE_BYTES = 20 * 1024 * 1024
CHANNELS = tuple(f"r{number:02}" for number in range(2, 10))
MEASUREMENTS = ("exposure_mr_h", "dose_nsv_h", *CHANNELS)
SCHEMA = """
CREATE TABLE stations (
    id INTEGER PRIMARY KEY, state VARCHAR NOT NULL, name VARCHAR NOT NULL,
    latitude DOUBLE, longitude DOUBLE
);
CREATE TABLE source_files (
    id INTEGER PRIMARY KEY, station_id INTEGER NOT NULL, year INTEGER NOT NULL,
    archive_url VARCHAR NOT NULL, file_name VARCHAR NOT NULL, row_count BIGINT NOT NULL,
    archive_sha256 VARCHAR NOT NULL, source_rows BIGINT NOT NULL,
    rejected_rows BIGINT NOT NULL, duplicate_rows BIGINT NOT NULL
);
CREATE TABLE observations (
    id BIGINT NOT NULL, source_file_id INTEGER NOT NULL, source_row INTEGER NOT NULL,
    observed_at TIMESTAMP NOT NULL, status VARCHAR NOT NULL,
    exposure_mr_h DOUBLE, dose_nsv_h DOUBLE,
    r02 DOUBLE, r03 DOUBLE, r04 DOUBLE, r05 DOUBLE,
    r06 DOUBLE, r07 DOUBLE, r08 DOUBLE, r09 DOUBLE
);
CREATE TABLE snapshot_metadata (key VARCHAR PRIMARY KEY, value VARCHAR NOT NULL);
"""


@dataclass(frozen=True)
class Archive:
    state: str
    station: str
    url: str


class ArchiveIndex(HTMLParser):
    def __init__(self):
        super().__init__()
        self.archives = {}
        self.href = None
        self.label = []
        self.station = None

    def handle_starttag(self, tag, attrs):
        if tag == "a":
            self.href = dict(attrs).get("href")
            self.label = []

    def handle_data(self, data):
        if self.href:
            self.label.append(data)

    def handle_endtag(self, tag):
        if tag != "a" or not self.href:
            return

        label = " ".join("".join(self.label).split())
        url = urljoin(INDEX, self.href)
        path = urlparse(url)
        station_label = re.fullmatch(r"(.+), ([A-Z]{2})", label)
        if station_label and path.netloc == "www.epa.gov" and path.path.startswith("/radnet/"):
            self.station = (station_label[2], station_label[1])
        if (
            path.scheme == "https"
            and path.netloc == "www.epa.gov"
            and path.path.startswith("/system/files/other-files/")
            and path.path.endswith(".zip")
        ):
            if self.station is None:
                raise ValueError(f"Missing station name for {url}")
            self.archives[url] = Archive(*self.station, url)
        self.href = None


def download(url, destination):
    if destination.is_file():
        return destination

    temporary = destination.with_suffix(destination.suffix + ".part")
    try:
        for attempt in range(3):
            try:
                with urlopen(url, timeout=45) as response, temporary.open("wb") as output:
                    if urlparse(response.url).hostname != "www.epa.gov":
                        raise ValueError(f"Unexpected download host for {url}")
                    size = 0
                    while chunk := response.read(64 * 1024):
                        size += len(chunk)
                        if size > MAX_ARCHIVE_BYTES:
                            raise ValueError(f"Download exceeds size limit: {url}")
                        output.write(chunk)
                temporary.replace(destination)
                return destination
            except HTTPError as error:
                if error.code not in (429, 500, 502, 503, 504) or attempt == 2:
                    raise
            except (URLError, TimeoutError):
                if attempt == 2:
                    raise
            time.sleep(2 ** attempt)
    finally:
        temporary.unlink(missing_ok=True)


def discover(cache):
    index = ArchiveIndex()
    index.feed(download(INDEX, cache / "archive-index.html").read_text(encoding="utf-8"))
    archives = sorted(index.archives.values(), key=lambda a: (a.state, a.station, a.url))
    if not archives:
        raise ValueError("No historical station archives found in the EPA index")
    return archives


def fetch_archives(cache, archives):
    def fetch(archive):
        digest = hashlib.sha256(archive.url.encode()).hexdigest()[:12]
        name = Path(urlparse(archive.url).path).name
        return download(archive.url, cache / f"{digest}-{name}")

    with ThreadPoolExecutor(max_workers=8) as pool:
        paths = list(pool.map(fetch, archives))
    return list(zip(archives, paths, strict=True))


def annual_files(archive, first_year, last_year):
    by_year = {}
    for member in sorted(archive.infolist(), key=lambda info: (len(info.filename), info.filename)):
        match = re.search(r"(20\d{2})(?: \(\d+\))?\.csv$", member.filename, re.IGNORECASE)
        if not match or not first_year <= (year := int(match[1])) <= last_year:
            continue
        if year in by_year:
            if member.CRC != by_year[year].CRC or member.file_size != by_year[year].file_size:
                raise ValueError(f"Conflicting annual files: {member.filename}, {by_year[year].filename}")
        else:
            by_year[year] = member
    return sorted(by_year.items())


def normalized_column(header, *names):
    for name in names:
        if name in header:
            return '"' + name.replace('"', '""') + '"'
    return "NULL"


def read_annual_csv(connection, csv_path):
    with csv_path.open(encoding="utf-8-sig", newline="") as stream:
        header = next(csv.reader(stream))
    if "SAMPLE COLLECTION TIME" not in header or "LOCATION_NAME" not in header:
        raise ValueError("Missing RadNet collection time or location column")

    columns = {
        "exposure_mr_h": normalized_column(header, "EXPOSURE RATE (mR/h)", "EXPOSURE RATE (mR/hr)"),
        "dose_nsv_h": normalized_column(header, "DOSE EQUIVALENT RATE (nSv/h)"),
        **{
            name: normalized_column(header, f"GAMMA COUNT RATE {name.upper()} (CPM)")
            for name in CHANNELS
        },
    }
    if all(column == "NULL" for column in columns.values()):
        raise ValueError("Missing RadNet measurement columns")
    measurement_select = ", ".join(
        f"CASE WHEN isfinite(try_cast({column} AS DOUBLE)) THEN try_cast({column} AS DOUBLE) END AS {name}"
        for name, column in columns.items()
    )
    status = normalized_column(header, "STATUS")
    connection.execute(
        f"""
        CREATE TEMP TABLE annual_rows AS
        SELECT cast(_source_row AS INTEGER) AS source_row,
               trim("LOCATION_NAME") AS source_location,
               coalesce(
                   try_strptime(trim("SAMPLE COLLECTION TIME"),
                      ['%m/%d/%Y %H:%M:%S', '%m/%d/%Y %H:%M']),
                   try_cast(trim("SAMPLE COLLECTION TIME") AS TIMESTAMP)
               ) AS observed_at,
               coalesce(upper(trim({status})), '') AS status,
               {measurement_select}
          FROM read_csv(?, header=true, all_varchar=true, delim=',')
        """,
        [str(csv_path)],
    )


def append_file(connection, archive, member, csv_path, station_id, year, url, checksum, next_id):
    with (
        io.TextIOWrapper(archive.open(member), encoding="utf-8-sig", newline="") as source,
        csv_path.open("w", encoding="utf-8", newline="") as extracted,
    ):
        reader = csv.reader(source, strict=True)
        writer = csv.writer(extracted)
        header = next(reader)
        writer.writerow([*header, "_source_row"])
        for row_number, row in enumerate(reader, 2):
            if row:
                if len(row) != len(header):
                    raise ValueError(f"Unexpected column count in source record {row_number}")
                writer.writerow([*row, row_number])
    read_annual_csv(connection, csv_path)
    station_labels = connection.execute(
        "SELECT DISTINCT source_location FROM annual_rows WHERE source_location <> ''"
    ).fetchall()
    if len(station_labels) > 1:
        raise ValueError(f"Multiple reporting locations in one annual file: {member.filename}")

    has_values = "coalesce(" + ", ".join(MEASUREMENTS) + ") IS NOT NULL"
    retained = f"year(observed_at) = ? AND {has_values}"
    total, accepted, unique = connection.execute(
        f"""
        SELECT count(*), count(*) FILTER (WHERE {retained}),
               count(DISTINCT observed_at) FILTER (WHERE {retained})
          FROM annual_rows
        """,
        [year, year],
    ).fetchone()
    duplicates = accepted - unique
    rejected = total - accepted
    file_id = station_id * 10000 + year
    recorded = ", ".join(MEASUREMENTS)
    completeness = " + ".join(f"cast({field} IS NOT NULL AS INTEGER)" for field in MEASUREMENTS)
    connection.execute(
        f"""
        INSERT INTO observations
        SELECT ? + row_number() OVER (ORDER BY observed_at), ?, source_row, observed_at, status,
               {recorded}
          FROM (
              SELECT * FROM annual_rows WHERE {retained}
              QUALIFY row_number() OVER (
                  PARTITION BY observed_at
                  ORDER BY (status = 'APPROVED') DESC, ({completeness}) DESC, source_row
              ) = 1
          )
          ORDER BY observed_at
        """,
        [next_id, file_id, year],
    )
    connection.execute(
        "INSERT INTO source_files VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
        [file_id, station_id, year, url, member.filename, unique, checksum, total, rejected, duplicates],
    )
    connection.execute("DROP TABLE annual_rows")
    return unique


def build_snapshot(output, archive_paths, first_year, last_year):
    import duckdb

    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="radnet-build-", dir=output.parent) as work:
        work = Path(work)
        database = work / "radnet.duckdb"
        csv_path = work / "annual.csv"
        with duckdb.connect(str(database), config={"threads": "4"}) as connection:
            connection.execute(SCHEMA)
            observations = 0
            for station_id, (archive, path) in enumerate(archive_paths, 1):
                with ZipFile(path) as source, path.open("rb") as raw:
                    files = annual_files(source, first_year, last_year)
                    if not files:
                        continue
                    checksum = hashlib.file_digest(raw, "sha256").hexdigest()
                    connection.execute(
                        "INSERT INTO stations (id, state, name) VALUES (?, ?, ?)",
                        [station_id, archive.state, archive.station],
                    )
                    for year, member in files:
                        try:
                            observations += append_file(
                                connection, source, member, csv_path, station_id, year,
                                archive.url, checksum, observations,
                            )
                        except Exception as error:
                            raise RuntimeError(f"Could not import {archive.url} / {member.filename}") from error
                if station_id % 20 == 0:
                    print(f"Imported {station_id} stations, {observations:,} observations", flush=True)

            if not observations:
                raise ValueError("Selected years have no usable observations")
            connection.executemany(
                "INSERT INTO snapshot_metadata VALUES (?, ?)",
                {
                    "schema_version": "1",
                    "source_url": INDEX,
                    "license_url": LICENSE,
                    "imported_at": datetime.now(timezone.utc).isoformat(),
                    "first_year": str(first_year),
                    "last_year": str(last_year),
                }.items(),
            )
            connection.execute("CHECKPOINT")
            for row in connection.execute(
                """
                SELECT year, count(*), sum(row_count), sum(rejected_rows), sum(duplicate_rows)
                  FROM source_files GROUP BY year ORDER BY year
                """
            ).fetchall():
                print(f"{row[0]}: {row[1]} files, {row[2]:,} observations, {row[3]:,} rejected, {row[4]:,} duplicates")
        os.replace(database, output)
    print(f"Wrote {observations:,} observations to {output} ({output.stat().st_size:,} bytes)")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True, help="Destination .duckdb file outside the source tree")
    parser.add_argument("--cache", type=Path, required=True, help="Directory for the downloaded station ZIPs")
    parser.add_argument("--first-year", type=int, default=2016)
    parser.add_argument("--last-year", type=int, default=2025)
    args = parser.parse_args()
    if args.first_year > args.last_year:
        parser.error("--first-year must be at or before --last-year")
    args.cache.mkdir(parents=True, exist_ok=True)
    archives = discover(args.cache)
    print(f"Found {len(archives)} EPA station archives", flush=True)
    build_snapshot(args.output, fetch_archives(args.cache, archives), args.first_year, args.last_year)


if __name__ == "__main__":
    main()
