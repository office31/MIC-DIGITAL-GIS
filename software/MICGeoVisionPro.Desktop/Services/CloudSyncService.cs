using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MICGeoVisionPro.Desktop.Services
{
    public sealed class CloudSyncException : Exception
    {
        public CloudSyncException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }

    public sealed class CloudProject
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAt { get; init; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset? UpdatedAt { get; init; }

        [JsonPropertyName("has_document")]
        public bool HasDocument { get; init; }

        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                var stamp = UpdatedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                var suffix = HasDocument ? string.Empty : " (no document)";
                return string.IsNullOrEmpty(stamp) ? $"{Name}{suffix}" : $"{Name} · {stamp}{suffix}";
            }
        }
    }

    /// <summary>
    /// Talks to the Neon Auth account server and the MIC GIS API.
    /// Credentials are only exchanged for a session token kept in memory.
    /// </summary>
    public sealed class CloudSyncService : IDisposable
    {
        public const string DefaultApiBaseUrl = "https://mic-digital-gis-api.onrender.com";
        public const string DefaultAuthBaseUrl =
            "https://ep-floral-water-beuafuqk.neonauth.c-9.us-east-2.aws.neon.tech/neondb/auth";

        public const int MaxDocumentBytes = 512 * 1024;

        private const string TrustedOrigin = "https://mic-geovision.onrender.com";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private CookieContainer _cookies = new();
        private readonly HttpClient _httpClient;
        private string? _token;
        private string? _signedInEmail;

        public CloudSyncService()
        {
            ApiBaseUrl = ResolveBaseUrl("MICGEOVISION_API_URL", DefaultApiBaseUrl);
            AuthBaseUrl = ResolveBaseUrl("MICGEOVISION_AUTH_URL", DefaultAuthBaseUrl);

            var handler = new HttpClientHandler
            {
                CookieContainer = _cookies,
                UseCookies = true
            };

            _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        }

        public string ApiBaseUrl { get; }

        public string AuthBaseUrl { get; }

        public bool IsSignedIn => !string.IsNullOrWhiteSpace(_token);

        public string? SignedInEmail => _signedInEmail;

        public async Task SignInAsync(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new CloudSyncException("Enter the email address of your account.");
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new CloudSyncException("Enter your password.");
            }

            var normalizedEmail = email.Trim();
            var body = JsonSerializer.Serialize(new { email = normalizedEmail, password });

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{AuthBaseUrl}/sign-in/email");
            request.Headers.TryAddWithoutValidation("Origin", TrustedOrigin);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await SendAsync(request, "sign-in");
            var text = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new CloudSyncException(BuildErrorMessage(response.StatusCode, text));
            }

            // The sign-in body carries the opaque session token; the API only accepts the JWT
            // that /token derives from the session cookie.
            var token = await FetchTokenAsync() ?? ReadToken(text);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new CloudSyncException("Sign-in succeeded but the account server returned no session token.");
            }

            _token = token;
            _signedInEmail = normalizedEmail;
        }

        public async Task SignOutAsync()
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{AuthBaseUrl}/sign-out");
                request.Headers.TryAddWithoutValidation("Origin", TrustedOrigin);
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                using var response = await _httpClient.SendAsync(request);
            }
            catch
            {
                // Signing out locally is what matters; the server call is best effort.
            }
            finally
            {
                ResetSession();
            }
        }

        public async Task<IReadOnlyList<CloudProject>> GetProjectsAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/api/projects");
            Authorize(request);
            using var response = await SendAsync(request, "project list");
            var text = await response.Content.ReadAsStringAsync();

            EnsureSuccess(response.StatusCode, text, "The account project list could not be loaded.");

            try
            {
                using var document = JsonDocument.Parse(text);
                if (!document.RootElement.TryGetProperty("projects", out var projects)
                    || projects.ValueKind != JsonValueKind.Array)
                {
                    throw new CloudSyncException("The server returned an unexpected project list.");
                }

                var result = new List<CloudProject>();
                foreach (var item in projects.EnumerateArray())
                {
                    var project = item.Deserialize<CloudProject>(JsonOptions);
                    if (project is not null && !string.IsNullOrWhiteSpace(project.Id))
                    {
                        result.Add(project);
                    }
                }

                return result;
            }
            catch (JsonException)
            {
                throw new CloudSyncException("The server returned an unreadable project list.");
            }
        }

        public async Task UploadProjectDocumentAsync(
            Guid projectId,
            string name,
            string? description,
            string documentJson)
        {
            var byteCount = Encoding.UTF8.GetByteCount(documentJson);
            if (byteCount > MaxDocumentBytes)
            {
                throw new CloudSyncException(
                    $"This project is {byteCount / 1024} KB and the account limit is 512 KB. " +
                    "Remove large imported CSV tables before uploading.");
            }

            string body;
            using (var payload = JsonDocument.Parse(documentJson))
            {
                body = JsonSerializer.Serialize(
                    new { name, description, payload = payload.RootElement },
                    JsonOptions);
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Put,
                $"{ApiBaseUrl}/api/projects/{projectId:D}/document");
            Authorize(request);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await SendAsync(request, "project upload");
            var text = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                throw new CloudSyncException("This project id already belongs to another account.");
            }

            EnsureSuccess(response.StatusCode, text, "The project could not be uploaded.");
        }

        public async Task<string> DownloadProjectDocumentAsync(string projectId)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{ApiBaseUrl}/api/projects/{Uri.EscapeDataString(projectId)}/document");
            Authorize(request);

            using var response = await SendAsync(request, "project download");
            var text = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new CloudSyncException("This cloud project has no saved document yet.");
            }

            EnsureSuccess(response.StatusCode, text, "The project could not be downloaded.");

            try
            {
                using var document = JsonDocument.Parse(text);
                if (!document.RootElement.TryGetProperty("project", out var project)
                    || !project.TryGetProperty("payload", out var payload)
                    || payload.ValueKind != JsonValueKind.Object)
                {
                    throw new CloudSyncException("This cloud project does not contain a usable document.");
                }

                return payload.GetRawText();
            }
            catch (JsonException)
            {
                throw new CloudSyncException("The server returned an unreadable project document.");
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        private async Task<string?> FetchTokenAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{AuthBaseUrl}/token");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("Origin", TrustedOrigin);

            using var response = await SendAsync(request, "session token");
            var text = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new CloudSyncException(
                    "The account server did not confirm the session. Sign in again.");
            }

            return ReadToken(text);
        }

        private void Authorize(HttpRequestMessage request)
        {
            if (string.IsNullOrWhiteSpace(_token))
            {
                throw new CloudSyncException("Sign in to your account first.");
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string operation)
        {
            try
            {
                return await _httpClient.SendAsync(request);
            }
            catch (TaskCanceledException exception)
            {
                throw new CloudSyncException(
                    $"The {operation} request timed out. Check the internet connection and try again.",
                    exception);
            }
            catch (HttpRequestException exception)
            {
                throw new CloudSyncException(
                    $"Unable to reach the server during the {operation} request: {exception.Message}",
                    exception);
            }
        }

        private void EnsureSuccess(HttpStatusCode statusCode, string text, string fallback)
        {
            if (statusCode == HttpStatusCode.Unauthorized)
            {
                ResetSession();
                throw new CloudSyncException("Your session has expired. Sign in again.");
            }

            if (statusCode == HttpStatusCode.RequestEntityTooLarge)
            {
                throw new CloudSyncException("The project is larger than the 512 KB account limit.");
            }

            if ((int)statusCode is < 200 or > 299)
            {
                throw new CloudSyncException($"{fallback} {BuildErrorMessage(statusCode, text)}");
            }
        }

        private void ResetSession()
        {
            _token = null;
            _signedInEmail = null;
            _cookies = new CookieContainer();
        }

        private static string? ReadToken(string text)
        {
            try
            {
                using var document = JsonDocument.Parse(text);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("token", out var token)
                    && token.ValueKind == JsonValueKind.String)
                {
                    var value = token.GetString();
                    return string.IsNullOrWhiteSpace(value) ? null : value;
                }
            }
            catch (JsonException)
            {
                // Not JSON: no token to read.
            }

            return null;
        }

        private static string BuildErrorMessage(HttpStatusCode statusCode, string text)
        {
            var detail = ExtractDetail(text);
            var suffix = $"(HTTP {(int)statusCode})";
            return string.IsNullOrWhiteSpace(detail) ? $"The server rejected the request {suffix}." : $"{detail} {suffix}";
        }

        private static string? ExtractDetail(string text)
        {
            try
            {
                using var document = JsonDocument.Parse(text);
                if (document.RootElement.ValueKind != JsonValueKind.Object) return null;

                foreach (var property in new[] { "error", "message", "detail" })
                {
                    if (document.RootElement.TryGetProperty(property, out var value)
                        && value.ValueKind == JsonValueKind.String)
                    {
                        var detail = value.GetString();
                        if (!string.IsNullOrWhiteSpace(detail)) return detail.Trim();
                    }
                }
            }
            catch (JsonException)
            {
                // Plain-text error body.
            }

            return null;
        }

        private static string ResolveBaseUrl(string variable, string fallback)
        {
            var value = Environment.GetEnvironmentVariable(variable);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().TrimEnd('/');
        }
    }
}
