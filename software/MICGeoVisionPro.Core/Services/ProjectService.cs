namespace MICGeoVisionPro.Core.Services;

using MICGeoVisionPro.Core.Models;

public class ProjectService
{
    public Project CreateProject(string name, string? description = null)
    {
        return new Project
        {
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
    }

    public Borehole AddBorehole(Project project, string name, double x, double y, double elevation, double totalDepth)
    {
        var borehole = new Borehole
        {
            Name = name,
            X = x,
            Y = y,
            Elevation = elevation,
            TotalDepth = totalDepth
        };

        project.Boreholes.Add(borehole);
        return borehole;
    }

    public Log AddLithologyLog(Borehole borehole, string name, List<LithologyInterval> intervals)
    {
        var log = new Log
        {
            Name = name,
            Type = LogType.Lithology
        };

        foreach (var interval in intervals)
        {
            log.Points.Add(new LogPoint
            {
                Depth = interval.StartDepth,
                EndDepth = interval.EndDepth,
                Label = interval.Lithology,
                Value = interval.EndDepth - interval.StartDepth
            });
        }

        borehole.Logs.Add(log);
        return log;
    }
}
