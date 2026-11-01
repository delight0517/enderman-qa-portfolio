# Enderman QA Portfolio Viewer for Windows

This is a small, independent Windows desktop viewer for the public Enderman QA 100-case portfolio. It is **not** the Enderman QA Workbench and contains no Workbench source or binary. The viewer reads the sanitized snapshot included in `data/cases.json`; it does not start Minecraft, connect to Jira, access Workbench data folders, or send network requests.

## Download and run

The Windows x64 self-contained preview ZIP can be downloaded from the repository's GitHub Releases page. Extract the ZIP, then run `EndermanQaPortfolioViewer.exe`. It includes the .NET 10 and Windows Desktop runtime so a separate .NET installation is not required. Windows SmartScreen may show a warning because this community release is not code-signed; verify the published SHA-256 before running it.

The release is a static snapshot dated 2026-10-31: 100 planned cases, 99 dispatched at least once, 18 full-scope Pass, 78 partial-scope Pass, 3 inconclusive and 1 not run. QA is incomplete. Server-side observations do not validate full client gameplay; no game screenshots or videos are included.

## Build from source

Requires the .NET 10 SDK and Windows Desktop targeting pack:

```powershell
dotnet build .\windows-app\EndermanQaPortfolioViewer.csproj -c Release
dotnet publish .\windows-app\EndermanQaPortfolioViewer.csproj -c Release -r win-x64 --self-contained true
```

The app code is MIT-licensed by the repository's `LICENSE`. The case data is CC BY 4.0 under `DATA-LICENSE.md`. The published package includes the .NET runtime MIT license and third-party notices in `THIRD-PARTY-NOTICES.txt`. Minecraft, the original Workbench and unrelated dependencies are not included.

## Verification mode

Run the built application with `--verify` to execute an offscreen, noninteractive module/data check. It loads the bundled snapshot and synthetic UI state, then exits with status 0 on success.
