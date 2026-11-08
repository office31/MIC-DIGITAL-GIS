# MIC GeoVision Pro — software

.NET 8 solution for the MIC GeoVision Pro desktop application.

## Projects

- `MICGeoVisionPro.Core/` — domain models (projects, boreholes, lithology logs, imported tables, layers) plus project, repository, and CSV import services.
- `MICGeoVisionPro.Desktop/` — WPF MVVM application for Windows.
- `MICGeoVisionPro.ConsoleDemo/` — headless walkthrough of the core workflow.
- `MICGeoVisionPro.sln` — solution file.

## Desktop features

- **Borehole view** — lithology log drawn against a depth axis in metres.
- **Map view** — boreholes placed by their X/Y coordinates with automatic fit and equal scaling; the selected borehole is highlighted.
- **Cross Section** — west-to-east profile with lithology columns, a surface line, and distance/elevation axes. Requires at least two boreholes with a total depth greater than zero.
- **Editing** — project name, borehole name/X/Y/elevation/depth, and lithology intervals (From/To/Lithology) in the LITHOLOGY LAYERS grid.
- **CSV import** — comma, semicolon, tab, or pipe delimiters; French decimal commas supported; a header row is required.
- **Export CSV** — one row per lithology interval, semicolon-separated, UTF-8 with BOM, French decimal separators.
- **Export PNG** — saves the active view as a PNG image.
- **Persistence** — saves to `%LOCALAPPDATA%\MICGeoVisionPro\project.db`. A sample B-01 borehole is seeded on first run.
- **Cloud account** — CLOUD ACCOUNT panel signs in to Neon Auth, lists the projects stored on the account, and uploads or downloads the complete project document.

## Project storage

The desktop app keeps a single SQLite database and exposes these options in the STORAGE panel:

- **Local folder** — the default location (`%LOCALAPPDATA%\MICGeoVisionPro`); **Open data folder** reveals it in Explorer.
- **Backup** — saves a copy of `project.db` to any folder, network share, or USB drive.
- **Restore** — checks that the selected file is a valid project database, replaces the current one, and reloads the project.
- **Cloud services** — upload the backup file to Google Drive, MediaFire, OneDrive, or Dropbox and download it on another machine. The app opens these sites but does not connect to their APIs; transfers are manual.

A backup contains the complete project, while CSV export contains only lithology intervals. Keep backups out of public shared folders.

## Cloud account synchronization

The CLOUD ACCOUNT panel links the desktop app to the same Neon Auth account used by the website:

- **Email and password** are sent only to the Neon Auth sign-in endpoint. The returned session token stays in memory; nothing is written to disk.
- **Refresh cloud projects** lists the projects owned by the account. Projects without a stored document are marked `(no document)`.
- **Upload** saves the current project locally first, then sends the complete JSON document (boreholes, logs, lithology intervals and imported tables) with `PUT /api/projects/{id}/document`. The account limit is 512 KB; remove large imported tables above that size.
- **Download** fetches `GET /api/projects/{id}/document`, replaces the local SQLite project after a confirmation, and refreshes every view. The most recent upload wins.
- The desktop app targets `https://mic-digital-gis-api.onrender.com` and the public Neon Auth URL by default. Set `MICGEOVISION_API_URL` and `MICGEOVISION_AUTH_URL` to test against another API or a local mock server.

The API must be version 1.3.0 or newer for the document routes. On a fresh deployment, the API creates the `payload` and `updated_at` columns automatically.

## Run locally

```bash
cd software
dotnet restore
dotnet run --project MICGeoVisionPro.Desktop/MICGeoVisionPro.Desktop.csproj
```

Requires the .NET 8 SDK. The console demo is still available:

```bash
dotnet run --project MICGeoVisionPro.ConsoleDemo/MICGeoVisionPro.ConsoleDemo.csproj
```

## Publish self-contained (win-x64)

```bash
cd software
dotnet publish MICGeoVisionPro.Desktop/MICGeoVisionPro.Desktop.csproj -p:PublishProfile=win-x64
```

The profile at `MICGeoVisionPro.Desktop/Properties/PublishProfiles/win-x64.pubxml` targets `win-x64`, bundles the .NET 8 Desktop runtime (`SelfContained=true`), keeps trimming disabled for WPF safety, and enables ReadyToRun. Output: `MICGeoVisionPro.Desktop/bin/Release/net8.0-windows/publish/win-x64/` (about 164 MB). Copy the whole folder to the target machine and run `MICGeoVisionPro.Desktop.exe`; no .NET installation is needed there.

## Remaining roadmap

- PDF and Excel exports
- Lithology color scheme editor
- Windows installer around the self-contained publish
