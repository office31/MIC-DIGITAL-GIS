using System.Text.Json;
using MICGeoVisionPro.Core.Models;

namespace MICGeoVisionPro.Core.Services;

/// <summary>Single JSON contract shared by local persistence and cloud synchronization.</summary>
public static class ProjectDocument
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize(Project project) => JsonSerializer.Serialize(project, Options);

    public static Project? Deserialize(string json) => JsonSerializer.Deserialize<Project>(json, Options);
}
