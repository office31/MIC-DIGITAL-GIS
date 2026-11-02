const express = require("express");
const app = express();
const port = process.env.PORT || 10000;

app.get("/", (req, res) => {
  res.json({
    name: "MIC GeoVision Pro",
    status: "online",
    message: "API SIG et géospatiale de MIC GIS",
    features: [
      "Boreholes",
      "Cross Sections",
      "Map View",
      "Lithology Logs",
      "Data Import / Export"
    ]
  });
});

app.get("/health", (req, res) => {
  res.json({ ok: true, service: "micgis-api", status: "healthy" });
});

app.get("/api/status", (req, res) => {
  res.json({
    project: "MIC GeoVision Pro",
    mode: "Render deployment",
    version: "1.0.0",
    environment: process.env.NODE_ENV || "development"
  });
});

app.listen(port, () => {
  console.log(`MIC GIS API running on port ${port}`);
});
