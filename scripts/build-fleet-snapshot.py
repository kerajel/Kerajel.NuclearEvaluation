"""Build the pinned 2024 fleet and state geometry assets from their source files."""

import argparse
import json
from collections import defaultdict
from io import BytesIO
from pathlib import Path
from zipfile import ZipFile

from openpyxl import load_workbook


def read_rows(source, sheet, header_row):
    workbook = load_workbook(source, read_only=True, data_only=True)
    try:
        rows = workbook[sheet].values
        for _ in range(header_row - 1):
            next(rows)
        headers = next(rows)
        for row in rows:
            yield dict(zip(headers, row))
    finally:
        workbook.close()


def number(value):
    if value is None or (isinstance(value, str) and value.strip() in ("", ".")):
        return None
    return float(value)


def build_fleet(inventory_path, generation_path):
    with ZipFile(inventory_path) as inventory:
        plants_source = BytesIO(inventory.read("2___Plant_Y2024.xlsx"))
        units_source = BytesIO(inventory.read("3_1_Generator_Y2024.xlsx"))
    plants = {
        row["Plant Code"]: row
        for row in read_rows(plants_source, "Plant", 2)
        if isinstance(row["Plant Code"], int)
    }
    units_by_plant = defaultdict(list)
    for row in read_rows(units_source, "Operable", 2):
        if row["Technology"] == "Nuclear" and row["Status"] == "OP":
            units_by_plant[row["Plant Code"]].append({
                "id": str(row["Generator ID"]).strip(),
                "netSummerCapacityMw": number(row["Summer Capacity (MW)"]),
            })

    output_by_unit = {}
    months = (
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    )
    for row in read_rows(generation_path, "2024 Generation", 6):
        plant_id = row["Plant ID"]
        if isinstance(plant_id, int):
            unit_key = (plant_id, str(row["Unit ID"]).strip())
            if unit_key in output_by_unit:
                raise ValueError(f"Duplicate generation data for plant/unit {unit_key}")
            output_by_unit[unit_key] = [number(row[month]) for month in months]

    fleet = []
    for plant_id, units in sorted(units_by_plant.items()):
        plant = plants[plant_id]
        units.sort(key=lambda unit: unit["id"])
        monthly_by_unit = [output_by_unit[plant_id, unit["id"]] for unit in units]
        monthly_totals = []
        for month in zip(*monthly_by_unit, strict=True):
            reported = [value for value in month if value is not None]
            monthly_totals.append(sum(reported) if reported else None)
        fleet.append({
            "id": plant_id,
            "name": plant["Plant Name"].strip(),
            "state": plant["State"].strip(),
            "latitude": number(plant["Latitude"]),
            "longitude": number(plant["Longitude"]),
            "units": units,
            "monthlyNetGenerationMwh": monthly_totals,
        })
    return fleet


def build_states(topology):
    scale = topology["transform"]["scale"]
    offset = topology["transform"]["translate"]
    arcs = []
    for delta_arc in topology["arcs"]:
        x = y = 0
        points = []
        for dx, dy in delta_arc:
            x += dx
            y += dy
            points.append([round(x * scale[0] + offset[0], 4), round(y * scale[1] + offset[1], 4)])
        arcs.append(points)

    def ring(indices):
        coordinates = []
        for index in indices:
            segment = arcs[index] if index >= 0 else arcs[~index][::-1]
            coordinates.extend(segment[1:] if coordinates else segment)
        return coordinates

    features = []
    for state in sorted(topology["objects"]["states"]["geometries"], key=lambda state: state["id"]):
        if state["id"] in {"02", "15"} or int(state["id"]) > 56:
            continue
        boundaries = state["arcs"]
        if state["type"] == "Polygon":
            coordinates = [ring(indices) for indices in boundaries]
        else:
            coordinates = [[ring(indices) for indices in polygon] for polygon in boundaries]
        features.append({
            "type": "Feature",
            "id": state["id"],
            "properties": state["properties"],
            "geometry": {"type": state["type"], "coordinates": coordinates},
        })
    return features


def write_collection(path, metadata, name, entries):
    with path.open("w", encoding="utf-8") as output:
        output.write(json.dumps(metadata)[:-1] + f', "{name}": [\n')
        output.write(",\n".join(json.dumps(entry, separators=(",", ":"), allow_nan=False) for entry in entries))
        output.write("\n]}\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source_dir", type=Path, help="Directory containing the three pinned input files")
    parser.add_argument("output_dir", type=Path)
    args = parser.parse_args()
    fleet = build_fleet(args.source_dir / "eia8602024.zip", args.source_dir / "usreact24.xlsx")
    states = build_states(json.loads((args.source_dir / "states-10m.json").read_text(encoding="utf-8")))
    args.output_dir.mkdir(parents=True, exist_ok=True)
    write_collection(args.output_dir / "fleet-2024.json", {"year": 2024}, "plants", fleet)
    write_collection(
        args.output_dir / "fleet-us-states.json", {"type": "FeatureCollection"}, "features", states,
    )


if __name__ == "__main__":
    main()
