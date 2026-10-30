# Enderman QA Public Site

A static, mobile-first public explorer for the Enderman QA Workbench 100-case project. The page presents the complete case catalog, current verdicts, bounded coverage and known limitations. It excludes raw journals, machine paths, run/entity identifiers and unreviewed screenshots.

## Data source

`tools/build_public_dataset.py` reads the current Workbench Excel export and writes only sanitized, user-facing fields into `dist/cases.json`. The current result summary is 96 Pass (18 full-scope, 78 partial-scope), 3 Inconclusive, 1 NotRun; 99/100 cases were dispatched at least once. This is not a claim of full interactive gameplay completion. No client screenshot/video is currently available.

## Build

Run `python tools/build_public_dataset.py --workbook <latest-workbook.xlsx> --output dist/cases.json` from this folder. The output is static JSON; no backend, credentials or browser storage are needed.
