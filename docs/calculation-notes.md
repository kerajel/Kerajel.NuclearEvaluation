# Calculation model

Nuclear Evaluation applies isotope decay correction at a project's selected date and groups
measurements into consistent histogram bins. Calculations use the stored measurements and
reference dates; raw values remain available independently of the selected correction date.

## Decay correction

Each isotope is calculated independently from its amount or activity at the reference date
using a fixed half-life:

```text
corrected = raw × exp(−ln(2) × elapsedYears / halfLifeYears)
elapsedYears = (targetDate − referenceDate) / (365.25 days)
```

Moving the target date forward decreases the value. Moving it before the reference date
increases it, and matching dates leave it unchanged. The formula follows the exponential decay
relation described in the
[IAEA training material](https://www-pub.iaea.org/MTCD/Publications/PDF/TCS-39_web.pdf).

| Measurement | Reference date |
|---|---|
| Particle | Analysis date |
| APM | Sample's sampling date |

Without a target date, the application returns the raw value. Missing measurements remain null.
Absolute error fields use the same decay factor as their corresponding isotope values.

### Half-life constants

| Isotope | Half-life in years |
|---|---:|
| U234 | 245,500 |
| U235 | 703,800,000 |
| U236 | 23,420,000 |
| U238 | 4,468,000,000 |

## Histogram bins

Bins are `n.m.` (not measured), `< 1`, `[1, 2)`, …, `[8, 9)`, and `≥ 9`.
Every isotope with matching records uses the same ordered bins, including zero-count bins.
The compact UI labels `1-2`, `2-3`, etc. denote lower-inclusive, upper-exclusive intervals.
Charts aggregate all matching rows, independently of the grid's page size.

## Example dataset

The shared workspace contains 100,000 generated series and 33,333 projects, with related samples,
subsamples, and isotope measurements. Dependent row counts are determined from parent IDs.
Sample and subsample external codes are sequential within each parent, and event dates follow
the sampling, screening, and analysis sequence.

The dataset is populated during initialization and refreshed by scheduled sandbox resets.
