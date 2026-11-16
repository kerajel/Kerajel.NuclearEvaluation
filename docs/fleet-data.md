# Fleet Explorer data

Fleet Explorer ships a read-only 2024 snapshot with the client. It covers 94 EIA-860 nuclear units
across 54 plants with an operating status at year-end. The site loads the snapshot and state
boundaries from its own static assets.

- [EIA-860 2024 archive](https://www.eia.gov/electricity/data/eia860/archive/xls/eia8602024.zip)
  supplies plant IDs, location, and net summer capacity in MW. The inventory's operable sheet also
  contains out-of-service units. The snapshot selects the `OP` status.
- [EIA 2024 capacity and generation workbook](https://www.eia.gov/nuclear/generation/archive/xls/usreact24.xlsx)
  supplies final monthly net generation in MWh, sourced from EIA-923 and EIA-860. The importer
  matches plant and unit IDs and sums reported values for each plant and month. An unreported unit
  does not contribute to the total. Months with no reported values stay blank. Negative reported
  values are preserved.
- [US Atlas 3.0.1 state boundaries](https://cdn.jsdelivr.net/npm/us-atlas@3.0.1/states-10m.json)
  redistribute Census cartographic boundaries. The importer converts the contiguous states and DC
  to GeoJSON. The [upstream license](../src/NuclearEvaluation.Client/wwwroot/data/fleet-map-license.txt)
  ships alongside the asset.

To reproduce both assets, download those three files into a local scratch directory and run the
importer with Python and `openpyxl` installed.

```sh
python3 scripts/build-fleet-snapshot.py /path/to/downloads src/NuclearEvaluation.Client/wwwroot/data
```

| Source file | SHA-256 |
| --- | --- |
| `eia8602024.zip` | `0aaae04812cd4ab87a3e346bdf93848a3cc15053fd4dc2a4cf82d2aeac95f12b` |
| `usreact24.xlsx` | `b35cd5cfebec9c998deede12a7829aa76d5803b0037309c69c1805d8c775fe22` |
| `states-10m.json` | `d76b391ccfa8bff601d51e3e3da5d43a89fa46cd5caca72ce731b383be5596d0` |
