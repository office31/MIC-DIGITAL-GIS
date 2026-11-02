# MIC GeoVision Pro / MIC GIS

## Description
MIC GeoVision Pro is a professional geospatial and geological software project inspired by advanced well-log, borehole, cross-section, and mapping workflows.

## Project goal
Build a modern GIS / geoscience tool for:
- boreholes and well data
- lithology and log analysis
- cross-sections and map views
- geospatial data exploration
- export and reporting

## Stack for the first deployment
- Website static: Render Static Site
- API: Node.js + Express on Render
- Source code: GitHub
- Optional future backend: PostgreSQL / GIS layer / .NET desktop app

## Repository structure
- `api/` : Node.js API for Render
- `website/` : static landing page for the product
- `render.yaml` : deployment config for Render
- `.github/FUNDING.yml` : GitHub sponsor button configuration

## Local development

### API
```bash
cd api
npm install
npm start
```

Then open:
- http://localhost:10000/
- http://localhost:10000/health
- http://localhost:10000/api/status

### Website
Open `website/index.html` in a browser, or serve it locally with any static server.

## Render deployment
1. Push this repository to GitHub.
2. Log in to Render.
3. Create a new Web Service for `api/` using the `render.yaml` configuration.
4. Create a Static Site for `website/`.
5. Make sure the canonical URL matches your final hosted domain.

## Sponsor button
GitHub sponsor support is configured through `.github/FUNDING.yml`.

## Recommended product name
- MIC GeoVision Pro
- Alternative: MIC GeoStrata

## Notes
This repository is a foundational deployment scaffold. The next step is to add the desktop application, geospatial data models, and a user interface for logs, boreholes, and maps.
