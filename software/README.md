# MIC GeoVision Pro — software MVP

This folder contains the first software architecture for the MIC GeoVision Pro project.

## What is included

- .NET 8 solution scaffold
- Core domain models for projects, boreholes, logs, tables, and layers
- Project service for creating projects and boreholes
- Data import service for CSV files
- Console demo to run the first MVP flow

## Structure

- `MICGeoVisionPro.sln`
- `MICGeoVisionPro.Core/` — domain logic and models
- `MICGeoVisionPro.ConsoleDemo/` — demo app to validate the workflow

## Run locally

```bash
cd software

dotnet restore

dotnet build

dotnet run --project MICGeoVisionPro.ConsoleDemo/MICGeoVisionPro.ConsoleDemo.csproj
```

## MVP workflow model

1. Create Project
2. Add Borehole
3. Add Lithology Log
4. Import CSV data
5. Analyse table values and logs
6. Extend into Map View and Cross Section View later

## Recommended next development steps

- Add WPF UI shell for Borehole View
- Add SQLite persistence layer
- Add Map view with coordinate layers
- Add Cross Section editor
- Add export to PDF / image / Excel
- Add scheme editor for lithology colors

## Product vision

MIC GeoVision Pro is intended to become a professional geological and geospatial software similar to the logic behind borehole and log visualization tools.
