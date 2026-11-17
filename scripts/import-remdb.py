#!/usr/bin/env python3
"""Build the radiochemistry snapshot from the European Commission JRC REMdb CSV releases."""

import argparse
import csv
import hashlib
import io
import os
from pathlib import Path
import re
import shutil
import tempfile
from urllib.request import urlopen
from zipfile import ZipFile

import duckdb


DATASET_URL = "https://jeodpp.jrc.ec.europa.eu/ftp/jrc-opendata/REMDB/datasets/"
LICENCE_URL = "https://creativecommons.org/licenses/by/4.0/"
METHODS_URL = "https://essd.copernicus.org/articles/11/589/2019/"
ARCHIVES = (
    ("1984-2006/years_1984-2006.zip", range(1984, 2007)),
    ("2007-2011/years_2007-2011.zip", range(2007, 2012)),
    *((f"{year}/year_{year}.zip", range(year, year + 1)) for year in range(2012, 2024)),
)
HISTORICAL_HEADER = (
    "LOC_NAME COUNTRY_NAME LOC_LATITUDE LOC_LONGITUDE STY_DESCRIPTION STR_DESCRIPTION "
    "SAM_BEGINDATETIME SAM_ENDDATETIME LAB_NAME APT_DESCRIPTION NUC_CODE "
    "MEA_STANDARDACTIVITYVALUE MEU_STANDARDUNIT VAT_DESCRIPTION"
).split()
ANNUAL_HEADER = (
    "LOCATION_NAME COUNTRY_NAME LATITUDE LONGITUDE SAMPLE_TYPE SAMPLE_TREATMENT "
    "SAMPLE_BEGIN SAMPLE_END LABORATORY APPARATUS NUCLIDE EQUALS_OR_LESSTHAN "
    "ACTIVITYVALUE MEASURING_UNIT VALUE_TYPE"
).split()
STAGING_HEADER = (
    "site_name country latitude longitude medium treatment source_begin source_end "
    "laboratory apparatus nuclide qualifier activity unit value_type source_file_id source_row"
).split()
MAX_ARCHIVE_BYTES = 64 * 1024 * 1024
MAX_MEMBER_BYTES = 200 * 1024 * 1024
SPLIT_LABS_1986 = {
    "NATIONAL RESEARCH INSTITUTE OF RADIOBIOLOGY AND": ("RADIOHYGIENE", 6),
    "NUCLEAR POWER PLANT OF PAKS, ENVIRONMENT LABORATORY": ('(H)"', 5),
}


def archive_path(cache, relative_url):
    path = cache / Path(relative_url).name
    if not path.is_file():
        with tempfile.NamedTemporaryFile(dir=cache, delete=False) as output:
            temporary = Path(output.name)
            try:
                with urlopen(DATASET_URL + relative_url, timeout=60) as response:
                    if not response.geturl().startswith(DATASET_URL):
                        raise ValueError("REMdb archive download redirected to another source")
                    shutil.copyfileobj(io.BytesIO(response.read(MAX_ARCHIVE_BYTES + 1)), output)
                if output.tell() > MAX_ARCHIVE_BYTES:
                    raise ValueError(f"REMdb archive exceeds download limit: {relative_url}")
                output.close()
                temporary.replace(path)
            finally:
                temporary.unlink(missing_ok=True)
    if path.stat().st_size > MAX_ARCHIVE_BYTES:
        raise ValueError(f"REMdb archive exceeds download limit: {path}")
    return path


def read_member(archive, member):
    if member.file_size > MAX_MEMBER_BYTES:
        raise ValueError(f"REMdb archive member exceeds size limit: {member.filename}")
    contents = archive.read(member)
    try:
        decoded = contents.decode("utf-8-sig")
    except UnicodeDecodeError:
        decoded = contents.decode("cp1252")
    return csv.reader(io.StringIO(decoded, newline=""), strict=True)


def stage_source(connection, work, cache):
    staged = work / "measurements.csv"
    connection.execute("""
        create table source_files (
            id integer, year integer, archive_url varchar, archive_sha256 varchar,
            file_name varchar, row_count bigint
        )
    """)
    files = []
    with staged.open("w", encoding="utf-8", newline="") as output:
        writer = csv.writer(output)
        writer.writerow(STAGING_HEADER)
        for relative_url, expected_years in ARCHIVES:
            path = archive_path(cache, relative_url)
            with path.open("rb") as raw:
                digest = hashlib.file_digest(raw, "sha256").hexdigest()
            with ZipFile(path) as archive:
                members = {}
                for member in archive.infolist():
                    if not member.filename.lower().endswith(".csv"):
                        continue
                    match = re.fullmatch(r"year[ _](\d{4})\.csv", Path(member.filename).name)
                    if not match:
                        raise ValueError(f"Unrecognized REMdb release filename: {member.filename}")
                    year = int(match[1])
                    if year in members:
                        raise ValueError(f"Duplicate REMdb release year {year} in {path.name}")
                    members[year] = member
                if set(members) != set(expected_years):
                    raise ValueError(f"Unexpected REMdb release years in {path.name}: {sorted(members)}")

                for year, member in sorted(members.items()):
                    rows = read_member(archive, member)
                    header = next(rows)
                    historical = year < 2012
                    if header != (HISTORICAL_HEADER if historical else ANNUAL_HEADER):
                        raise ValueError(f"Changed REMdb source columns: {member.filename}: {header}")
                    count = 0
                    footer_count = None
                    records = enumerate(rows, start=2)
                    for source_row, row in records:
                        if not row:
                            continue
                        footer = re.fullmatch(r"([\d,]+) rows selected\.", ",".join(row).strip())
                        if not historical and footer:
                            if footer_count is not None:
                                raise ValueError(f"Repeated row count footer in {member.filename}")
                            footer_count = int(footer[1].replace(",", ""))
                            continue
                        if footer_count is not None:
                            raise ValueError(f"Unexpected records after row count footer in {member.filename}")
                        if len(row) != len(header):
                            raise ValueError(f"Wrong column count in {member.filename}, record {source_row}")
                        if year == 1986 and not any(row[9:]) and row[8].strip() in SPLIT_LABS_1986:
                            suffix, populated = SPLIT_LABS_1986[row[8].strip()]
                            _, continuation = next(records, (None, []))
                            if (
                                len(continuation) != len(header) or continuation[0] != suffix
                                or not all(continuation[:populated]) or any(continuation[populated:])
                            ):
                                raise ValueError(f"Invalid split REMdb record in {member.filename}, record {source_row}")
                            laboratory = " ".join((row[8].strip(), suffix.removesuffix('"')))
                            results = continuation[1:populated]
                            if populated == 5:
                                results.insert(0, "")
                            row = [*row[:8], laboratory, *results]
                        if historical:
                            row.insert(11, "")
                        if not row[11] and row[12].startswith("<"):
                            qualifier = "<=" if row[12].startswith("<=") else "<"
                            row[11], row[12] = qualifier, row[12][len(qualifier):].strip()
                        writer.writerow([*row, year, source_row])
                        count += 1
                    if footer_count is not None and count != footer_count:
                        raise ValueError(f"Row count footer in {member.filename} says {footer_count}, parsed {count}")
                    files.append((year, year, DATASET_URL + relative_url, digest, member.filename, count))
                    print(f"{year}: {count:,} results", flush=True)
    connection.executemany("insert into source_files values (?, ?, ?, ?, ?, ?)", files)
    return staged


def normalize(connection, staged):
    connection.execute("""
        create macro timestamp_value(val) as coalesce(
            try_cast(val as timestamp), try_strptime(val, ['%m/%d/%Y %H:%M', '%m/%d/%Y %H:%M:%S', '%m/%d/%Y'])
        );
        create macro numeric_value(val) as
            case when isfinite(try_cast(val as double)) then try_cast(val as double) end;
    """)
    connection.execute("""
        create temp table raw_results as
        select coalesce(trim(country), '') as country, coalesce(trim(site_name), '') as site_name,
               numeric_value(latitude) as latitude, numeric_value(longitude) as longitude,
               coalesce(trim(medium), '') as medium, coalesce(trim(treatment), '') as treatment,
               coalesce(cast(timestamp_value(source_begin) as varchar), trim(source_begin), '') as source_begin,
               coalesce(cast(timestamp_value(source_end) as varchar), trim(source_end), '') as source_end,
               cast(timestamp_value(source_begin) as date) as began_on,
               cast(timestamp_value(source_end) as date) as ended_on,
               trim(laboratory) as laboratory, trim(apparatus) as apparatus, trim(nuclide) as nuclide,
               trim(qualifier) as qualifier, numeric_value(activity) as activity, trim(unit) as unit,
               trim(value_type) as value_type, cast(source_file_id as integer) as source_file_id,
               cast(source_row as integer) as source_row,
               nullif(trim(activity), '') is not null and numeric_value(activity) is null as unparsed_activity
        from read_csv(?, all_varchar = true, header = true, delim = ',', quote = '"', escape = '"')
    """, [str(staged)])
    connection.execute("""
        create table sites as
        select cast(row_number() over (order by country, name, latitude, longitude) as integer) as id, *
        from (select distinct country, site_name as name, latitude, longitude from raw_results);

        create temp table site_results as
        select sites.id as site_id, raw_results.* exclude (country, site_name, latitude, longitude)
        from raw_results
        join sites on sites.country = raw_results.country and sites.name = raw_results.site_name
            and sites.latitude is not distinct from raw_results.latitude
            and sites.longitude is not distinct from raw_results.longitude;

        create table sampling_windows as
        select row_number() over (order by site_id, medium, treatment, source_begin, source_end) as id, *
        from (
            select distinct site_id, medium, treatment, began_on, ended_on, source_begin, source_end
            from site_results
        );

        create table measurements as
        select row_number() over (order by source_file_id, source_row) as id,
               sampling_windows.id as window_id, source_file_id, source_row,
               laboratory, apparatus, nuclide, activity, unit, qualifier, value_type
        from site_results
        join sampling_windows using (site_id, medium, treatment, source_begin, source_end)
        order by window_id, source_file_id, source_row;
    """)
    source_rows, count = connection.execute("""
        select (select sum(row_count) from source_files), (select count(*) from measurements)
    """).fetchone()
    if count != source_rows:
        raise ValueError(f"REMdb row count changed during import: expected {source_rows}, found {count}")
    return count


def snapshot(work, cache, destination):
    temporary_db = work / "snapshot.duckdb"
    with duckdb.connect(str(temporary_db), config={"threads": 4, "memory_limit": "2GB"}) as connection:
        staged = stage_source(connection, work, cache)
        count = normalize(connection, staged)
        metadata = {
            "dataset": "European Commission, Joint Research Centre, Radioactivity Environmental Monitoring Database (REMdb)",
            "source_url": DATASET_URL,
            "licence_url": LICENCE_URL,
            "methods_url": METHODS_URL,
            "release_years": "1984–2023",
            "sampling_windows": "Grouped by published site and coordinates, medium, treatment, and sampling timestamps.",
            "historical_qualifiers": "1984–2011 CSV files do not provide a separate qualifier column.",
            "source_repair": "The 1986 export has records split across two rows. The importer rejoins these with a recognized continuation and cites the first row.",
        }
        connection.execute("create table snapshot_metadata (key varchar, value varchar)")
        connection.executemany("insert into snapshot_metadata values (?, ?)", metadata.items())
        audit = connection.execute("""
            select count_if(unparsed_activity), count_if(activity < 0),
                   count_if(began_on is null and source_begin <> ''),
                   count_if(ended_on is null and source_end <> ''),
                   count_if(ended_on < began_on),
                   count_if(abs(latitude) > 90 or abs(longitude) > 180)
            from raw_results
        """).fetchone()
        print(f"Imported {count:,} results", flush=True)
        print(f"Audit (unparsed activity, negative values, unparsed start dates, "
              f"unparsed end dates, end before start, out-of-range coordinates): {audit}", flush=True)
        connection.execute("checkpoint")
    os.replace(temporary_db, destination)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive-cache", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args()
    cache = arguments.archive_cache.resolve()
    destination = arguments.output.resolve()
    cache.mkdir(parents=True, exist_ok=True)
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="remdb-", dir=destination.parent) as directory:
        snapshot(Path(directory), cache, destination)


if __name__ == "__main__":
    main()
