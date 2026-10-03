using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Indexers.Antra
{
    public class AntraRequestGenerator : IIndexerRequestGenerator
    {
        public AntraIndexerSettings Settings { get; set; }

        private string BaseUrl => (Settings.BaseUrl ?? "").TrimEnd('/');

        public IndexerPageableRequestChain GetRecentRequests()
        {
            // RSS is not supported; this nominal request only exists so the
            // indexer health test has something harmless to hit (bridge /health).
            var chain = new IndexerPageableRequestChain();
            chain.AddTier(new[] { HealthRequest() });
            return chain;
        }

        public IndexerPageableRequestChain GetSearchRequests(AlbumSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();
            chain.AddTier(new[] { ResolveRequest(searchCriteria) });
            return chain;
        }

        public IndexerPageableRequestChain GetSearchRequests(ArtistSearchCriteria searchCriteria)
        {
            // The bridge is album-level only; artist searches yield nothing.
            return new IndexerPageableRequestChain();
        }

        private IndexerRequest HealthRequest()
        {
            var request = new IndexerRequest($"{BaseUrl}/health", HttpAccept.Json);
            request.HttpRequest.RequestTimeout = TimeSpan.FromSeconds(30);
            ApplyToken(request.HttpRequest);
            return request;
        }

        private IndexerRequest ResolveRequest(AlbumSearchCriteria criteria)
        {
            var artist = criteria.Artist?.Name ?? string.Empty;
            var albumTitle = criteria.AlbumTitle ?? string.Empty;
            var tracks = ExtractTracks(criteria);

            var body = new JObject
            {
                ["artist"] = artist,
                ["album"] = albumTitle
            };

            if (criteria.AlbumYear > 0)
            {
                body["year"] = criteria.AlbumYear;
            }

            if (tracks.Count > 0)
            {
                body["tracks"] = JArray.FromObject(tracks);
            }

            var request = new IndexerRequest($"{BaseUrl}/resolve", HttpAccept.Json);
            request.HttpRequest.Method = System.Net.Http.HttpMethod.Post;
            request.HttpRequest.Headers.ContentType = "application/json";
            request.HttpRequest.RequestTimeout = TimeSpan.FromSeconds(Math.Max(10, Settings.TimeoutSeconds));
            ApplyToken(request.HttpRequest);
            request.HttpRequest.SetContent(body.ToString(Formatting.None));
            return request;
        }

        /// <summary>
        /// The expected tracklist for the album being searched, read from the
        /// first monitored (or any-release-ok) album release that carries
        /// tracks. The bridge validates its Deezer edition against this list,
        /// which is what stops it from grabbing a deluxe/remaster with a
        /// different track count.
        /// </summary>
        private static List<JObject> ExtractTracks(AlbumSearchCriteria criteria)
        {
            var result = new List<JObject>();
            var album = criteria.Albums?.FirstOrDefault();

            if (album == null)
            {
                return result;
            }

            List<AlbumRelease> releases = null;

            try
            {
                releases = album.AlbumReleases?.Value;
            }
            catch
            {
                return result;
            }

            if (releases == null || releases.Count == 0)
            {
                return result;
            }

            var eligible = releases.Where(r => r.Monitored || album.AnyReleaseOk).ToList();

            if (eligible.Count == 0)
            {
                eligible = releases;
            }

            foreach (var release in eligible)
            {
                List<Track> releaseTracks = null;

                try
                {
                    releaseTracks = release.Tracks?.Value;
                }
                catch
                {
                    continue;
                }

                if (releaseTracks == null || releaseTracks.Count == 0)
                {
                    continue;
                }

                foreach (var track in releaseTracks
                    .OrderBy(t => t.MediumNumber)
                    .ThenBy(t => t.AbsoluteTrackNumber))
                {
                    var entry = new JObject
                    {
                        ["title"] = track.Title
                    };

                    if (track.Duration > 0)
                    {
                        entry["duration_s"] = Math.Round(track.Duration / 1000.0, 1);
                    }

                    result.Add(entry);
                }

                break;
            }

            return result;
        }

        private void ApplyToken(HttpRequest request)
        {
            if (!string.IsNullOrWhiteSpace(Settings.BridgeApiToken))
            {
                request.Headers["X-Bridge-Token"] = Settings.BridgeApiToken;
            }
        }
    }
}
