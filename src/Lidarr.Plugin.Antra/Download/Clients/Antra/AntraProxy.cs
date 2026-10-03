using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.Clients.Antra;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Plugin.Antra
{
    public interface IAntraProxy
    {
        List<DownloadClientItem> GetQueue(AntraSettings settings);

        Task<string> Download(RemoteAlbum remoteAlbum, AntraSettings settings);

        void RemoveFromQueue(string downloadId, AntraSettings settings);

        void TestConnection(AntraSettings settings);
    }

    public class AntraProxy : IAntraProxy
    {
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public AntraProxy(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public List<DownloadClientItem> GetQueue(AntraSettings settings)
        {
            var response = _httpClient.Execute(Request(settings, "/jobs?limit=100"));
            EnsureSuccess(response);

            var jobs = JObject.Parse(response.Content)["jobs"] as JArray ?? new JArray();

            return jobs
                .OfType<JObject>()
                .Select(ToDownloadClientItem)
                .Where(item => item != null)
                .ToList();
        }

        public Task<string> Download(RemoteAlbum remoteAlbum, AntraSettings settings)
        {
            var release = remoteAlbum.Release;
            var album = remoteAlbum.Albums?.FirstOrDefault();
            var artistName = remoteAlbum.Artist?.Name ?? album?.Artist?.Value?.Name ?? release.Artist;
            var albumTitle = album?.Title ?? release.Album;
            var year = album?.ReleaseDate?.Year;

            var body = new JObject
            {
                ["artist"] = artistName,
                ["album"] = albumTitle,
                ["deezer_url"] = release.DownloadUrl,
                ["format"] = settings.Format.IsNullOrWhiteSpace() ? null : settings.Format,
                ["sources"] = settings.Sources.IsNullOrWhiteSpace() ? null : settings.Sources
            };

            if (year.HasValue)
            {
                body["year"] = year.Value;
            }

            var response = Post(settings, "/download", body.ToString());
            EnsureSuccess(response);

            var jobId = JObject.Parse(response.Content).Value<string>("job_id");

            if (jobId.IsNullOrWhiteSpace())
            {
                throw new DownloadClientException("Antra bridge did not return a job id for the download request");
            }

            _logger.Info("Queued Antra bridge job {0} for '{1} - {2}'", jobId, artistName, albumTitle);
            return Task.FromResult(jobId);
        }

        public void RemoveFromQueue(string downloadId, AntraSettings settings)
        {
            try
            {
                var request = Request(settings, $"/jobs/{downloadId}");
                request.Method = HttpMethod.Delete;
                _httpClient.Execute(request);
            }
            catch (Exception ex)
            {
                // The job may already be finished (409) or gone (404); removal is best-effort.
                _logger.Debug(ex, "Antra bridge cancel failed for job {0}", downloadId);
            }
        }

        public void TestConnection(AntraSettings settings)
        {
            var response = _httpClient.Execute(Request(settings, "/health"));
            EnsureSuccess(response);

            var json = JObject.Parse(response.Content);

            if (json["engine"]?.Value<bool?>("import_ok") == false)
            {
                throw new DownloadClientException(
                    "Antra bridge is running but the engine failed to load: " +
                    (json["engine"]?.Value<string>("error") ?? "unknown error"));
            }
        }

        private static DownloadClientItem ToDownloadClientItem(JObject job)
        {
            var id = job.Value<string>("id");

            if (id.IsNullOrWhiteSpace())
            {
                return null;
            }

            var request = job["request"] as JObject;
            var artist = request?.Value<string>("artist") ?? "";
            var album = request?.Value<string>("album") ?? "";
            var title = artist.IsNullOrWhiteSpace() ? album : $"{artist} - {album}";

            var item = new DownloadClientItem
            {
                DownloadId = id,
                Title = title.IsNullOrWhiteSpace() ? $"Antra job {id.Substring(0, Math.Min(8, id.Length))}" : $"{title} [Antra]",
                CanMoveFiles = true,
                CanBeRemoved = true,
                Status = MapStatus(job.Value<string>("status")),
                Message = job.Value<string>("error")
            };

            var files = (job["result"] as JObject)?["files"] as JArray;
            var firstPath = files?
                .OfType<JObject>()
                .Select(f => f.Value<string>("file_path"))
                .FirstOrDefault(p => !p.IsNullOrWhiteSpace());

            if (!firstPath.IsNullOrWhiteSpace())
            {
                var directory = System.IO.Path.GetDirectoryName(firstPath);

                if (!directory.IsNullOrWhiteSpace())
                {
                    item.OutputPath = new OsPath(directory);
                }
            }

            return item;
        }

        private static DownloadItemStatus MapStatus(string status)
        {
            switch (status)
            {
                case "queued":
                    return DownloadItemStatus.Queued;
                case "running":
                    return DownloadItemStatus.Downloading;
                case "completed":
                    return DownloadItemStatus.Completed;
                case "failed":
                case "cancelled":
                case "rate_limited":
                    return DownloadItemStatus.Failed;
                default:
                    return DownloadItemStatus.Queued;
            }
        }

        private static HttpRequest Request(AntraSettings settings, string path)
        {
            var request = new HttpRequest((settings.BridgeUrl ?? "").TrimEnd('/') + path);
            request.Headers.Accept = HttpAccept.Json.Value;
            request.RequestTimeout = TimeSpan.FromSeconds(Math.Max(10, settings.TimeoutSeconds));

            if (!settings.BridgeApiToken.IsNullOrWhiteSpace())
            {
                request.Headers["X-Bridge-Token"] = settings.BridgeApiToken;
            }

            return request;
        }

        private HttpResponse Post(AntraSettings settings, string path, string json)
        {
            var request = Request(settings, path);
            request.Method = HttpMethod.Post;
            request.Headers.ContentType = "application/json";
            request.SetContent(json);

            try
            {
                return _httpClient.Execute(request);
            }
            catch (HttpException ex)
            {
                throw new DownloadClientUnavailableException(
                    $"Antra bridge at {settings.BridgeUrl} is unreachable: {ex.Message}", ex);
            }
        }

        private static void EnsureSuccess(HttpResponse response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new DownloadClientAuthenticationException("Antra bridge rejected the API token");
            }

            if (response.HasHttpError)
            {
                var content = (response.Content ?? "").Trim();
                throw new DownloadClientException(
                    $"Antra bridge returned {(int)response.StatusCode}: {(content.Length > 200 ? content.Substring(0, 200) : content)}");
            }
        }
    }
}
