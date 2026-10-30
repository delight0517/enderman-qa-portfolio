# Enderman QA Public Site

A static, mobile-first public explorer for the Enderman QA Workbench 100-case project. The page presents the complete case catalog, current verdicts, bounded coverage and known limitations. It excludes raw journals, machine paths, run/entity identifiers and unreviewed screenshots.

## Data source

`tools/build_public_dataset.py` reads a Workbench Excel export and writes only sanitized, user-facing fields into `dist/cases.json`. It derives the reconciliation date from the dated workbook filename and includes that workbook's SHA-256 so a published snapshot can be traced without exposing the workbook. The current snapshot uses `Enderman-QA-100-latest-20261031.xlsx`: 96 Pass labels (18 full-scope, 78 partial-scope), 3 Inconclusive, 1 NotRun; 99/100 cases were dispatched at least once. This is not a claim of full interactive gameplay completion. No client screenshot/video is currently available.

The published Site is a static snapshot. It does not read the local app or synchronize automatically; rebuild and publish the sanitized dataset when the workbook changes. The page displays the data date, source filename and source hash to make freshness visible.

## Build

Run `python tools/build_public_dataset.py --workbook <latest-workbook-YYYYMMDD.xlsx> --output dist/cases.json` from this folder. The output is static JSON; no backend, credentials or browser storage are needed. The workbook filename must contain its reconciliation date as `YYYYMMDD`.
