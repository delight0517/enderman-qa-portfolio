# Contributing

Contributions to the static portfolio and dataset builder are welcome. Keep changes small and reviewable.

## Dataset and privacy

- Do not add raw journals, local paths, machine/user identifiers, credentials, or unreviewed media.
- Preserve the fixed public-field allowlist in `tools/build_public_dataset.py`.
- Reconcile totals and verdicts against a dated source workbook; never infer missing results.
- Keep full-scope, partial-scope, inconclusive, and not-run results distinct.
- Update the portfolio's checkpoint date and limitations when the dataset changes.

## Review checklist

1. Run the builder against a dated workbook and inspect the complete generated JSON diff.
2. Verify 100 unique IDs and totals match the evidence source.
3. Search output for local paths, tokens, run IDs, and private execution details.
4. Check the static page at desktop and mobile widths and keyboard-navigate its filters.
5. Explain evidence and coverage limitations in the change description.

The Enderman QA Workbench executable and its source are outside this repository. Do not submit decompiled app code or imply this portfolio is the app's open-source release.
