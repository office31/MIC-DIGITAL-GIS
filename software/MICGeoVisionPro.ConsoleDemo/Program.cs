using MICGeoVisionPro.Core.Models;
using MICGeoVisionPro.Core.Services;

var projectService = new ProjectService();
var importService = new DataImportService();

var project = projectService.CreateProject("MIC GIS Demo", "First working geological project");

var borehole = projectService.AddBorehole(
    project,
    "B-01",
    500.00,
    600.00,
    120.5,
    76.0);

var lithologyIntervals = new List<LithologyInterval>
{
    new() { StartDepth = 0, EndDepth = 12, Lithology = "Topsoil", Description = "Surface material" },
    new() { StartDepth = 12, EndDepth = 28, Lithology = "Clay", Description = "Silty clay" },
    new() { StartDepth = 28, EndDepth = 45, Lithology = "Sand", Description = "Fine sand" },
    new() { StartDepth = 45, EndDepth = 76, Lithology = "Limestone", Description = "Hard limestone" }
};

projectService.AddLithologyLog(borehole, "Lithology Log", lithologyIntervals);

var csvPath = Path.Combine(AppContext.BaseDirectory, "sample-data.csv");
File.WriteAllText(csvPath,
    "Well,Depth,Value\nB-01,0,0\nB-01,12,12\nB-01,28,28\nB-01,76,76\n");

var table = importService.ImportCsv(csvPath);

Console.WriteLine($"Project: {project.Name}");
Console.WriteLine($"Borehole: {borehole.Name} | X={borehole.X} Y={borehole.Y} Elev={borehole.Elevation} Depth={borehole.TotalDepth}");
Console.WriteLine($"Logs: {borehole.Logs.Count}");
Console.WriteLine($"Imported rows: {table.Rows.Count}");
Console.WriteLine("Demo complete.");
