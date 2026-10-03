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
            // RSS is not supported. GetRecentRequests doubles as the indexer
            // "Test" probe, and Lidarr's test only passes when the probe
            // returns a real result, so it resolves a well-known album through
            // the bridge (mirrors TrevTV's rickroll probe pattern).
            var chain = new IndexerPageableRequestChain();
            chain.AddTier(new[] { RecentProbeRequest() });
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

        private IndexerRequest RecentProbeRequest()
        {
            var body = new JObject
            {
                ["artist"] = "Daft Punk",
                ["album"] = "Random Access Memories",
                ["year"] = 2013
            };

            var request = new IndexerRequest($"{BaseUrl}/resolve", HttpAccept.Json);
            request.HttpRequest.Method = System.Net.Http.HttpMethod.Post;
            request.HttpRequest.Headers.ContentType = "application/json";
            request.HttpRequest.RequestTimeout = TimeSpan.FromSeconds(Math.Max(10, Settings.TimeoutSeconds));
            ApplyToken(request.HttpRequest);
            request.HttpRequest.SetContent(body.ToString(Formatting.None));
            return request;
        }

        private IndexerRequest ResolveRequest(AlbumSearchCriteria criteria)
        {
            var artist = criteria.Artist?.Name ?? string.Empty;
            var albumTitle = criteria.AlbumTitle ?? string.Empty;
            var tracklists = ExtractTracklists(criteria);

            var body = new JObject
            {
                ["artist"] = artist,
                ["album"] = albumTitle
            };

            if (criteria.AlbumYear > 0)
            {
                body["year"] = criteria.AlbumYear;
            }

            if (tracklists.Count > 0)
            {
                body["tracks"] = JArray.FromObject(tracklists[0]);

                if (tracklists.Count > 1)
                {
                    var alternates = new JArray();

                    foreach (var tracklist in tracklists.Skip(1))
                    {
                        alternates.Add(JArray.FromObject(tracklist));
                    }

                    body["alternates"] = alternates;
                }
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
        /// The expected tracklists for the album being searched, read from the
        /// album's releases. Lidarr stores one row per release (original,
        /// deluxe, anniversary, ...) and any of them is a legitimate target,
        /// so up to six unique tracklists travel with the search: the
        /// monitored release first, then the rest. The bridge validates its
        /// Deezer edition against any of them, which is what stops it from
        /// grabbing an edition with a different track count.
        /// </summary>
        private const int MaxTracklists = 6;

        private static List<List<JObject>> ExtractTracklists(AlbumSearchCriteria criteria)
        {
            var result = new List<List<JObject>>();
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

            var ordered = releases
                .OrderByDescending(r => r.Monitored)
                .ToList();

            var seenCounts = new HashSet<int>();

            foreach (var release in ordered)
            {
                if (result.Count >= MaxTracklists)
                {
                    break;
                }

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

                if (!seenCounts.Add(releaseTracks.Count))
                {
                    continue;
                }

                var tracklist = new List<JObject>();

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

                    tracklist.Add(entry);
                }

                result.Add(tracklist);
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
