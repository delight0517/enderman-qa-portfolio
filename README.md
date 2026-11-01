# Enderman QA Portfolio — 100 TC Case Study

A static, mobile-first hiring portfolio for a Minecraft Java Enderman QA case study. It presents project context, the QA approach, the complete 100-case catalog, current verdicts, bounded coverage and known limitations. It excludes raw journals, machine paths, run/entity identifiers and unreviewed screenshots.

## Portfolio summary

- **Project:** Turn game behavior questions into 100 test cases with preconditions, expected behavior, observations and pass criteria.
- **My contribution:** Structured the case catalog and verdict criteria; reconciled recorded runs into full, partial, inconclusive and not-run coverage; reviewed evidence limits; prepared a privacy-scrubbed public portfolio.
- **Current checkpoint (2026-10-31):** 18 full-scope Pass, 78 partial-scope Pass, 3 inconclusive, 1 not run; 99 of 100 cases dispatched at least once. The first 96 are verdicts labeled Pass, split by scope as 18 full + 78 partial. The other 4 need follow-up. This does **not** mean 99 cases fully passed. The tests were server-side and do not verify rendered client behavior; no validated game screenshots/video are available. The QA project is still in progress.
- **Tool/source status:** The Windows Workbench executable was used to organize the project, but its canonical source and redistribution license have not been recovered. This repository is only the separately authored static portfolio and sanitized dataset builder; it is not the Workbench source or a substitute app.

## Reproduce the public dataset

The builder reads the `Test Cases` worksheet from a dated Workbench Excel export and emits only a fixed, sanitized allowlist of public fields. It does not publish raw execution journals or local identifiers.

```powershell
python tools/build_public_dataset.py --workbook .\Enderman-QA-100-latest-YYYYMMDD.xlsx --output .\dist\cases.json
```

Review the generated JSON and verify its totals and privacy before publishing. The site is a static snapshot: it does not connect to the local Workbench and must be rebuilt when the source workbook changes.

## License boundary

The portfolio source and builder authored in this repository are licensed under MIT (see `LICENSE`). The QA case content and dataset are separately licensed under CC BY 4.0 (see `DATA-LICENSE.md`). Minecraft, the Workbench application, its binaries, and third-party dependencies are not covered by these licenses. No Workbench source or binary is included.

For hiring review, start with the public case study, then inspect the dataset builder and privacy allowlist. The live page is linked from the repository's GitHub description when available.

## Data source

The current static snapshot was generated from the dated workbook named in dist/cases.json; only its SHA-256 and reconciliation date are published. The workbook itself is private project data and is not included.

## Build

Run `python tools/build_public_dataset.py --workbook <latest-workbook-YYYYMMDD.xlsx> --output dist/cases.json` from this folder. The output is static JSON; no backend, credentials or browser storage are needed. The workbook filename must contain its reconciliation date as `YYYYMMDD`.
