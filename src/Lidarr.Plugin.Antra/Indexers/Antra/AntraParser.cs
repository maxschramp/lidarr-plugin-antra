using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.Antra
{
    public class AntraParser : IParseIndexerResponse
    {
        public AntraIndexerSettings Settings { get; set; }

        public IList<ReleaseInfo> ParseResponse(IndexerResponse response)
        {
            var releases = new List<ReleaseInfo>();

            JObject json;

            try
            {
                json = JObject.Parse(response.Content);
            }
            catch
            {
                // /health bodies and non-JSON error pages end up here; no results.
                return releases;
            }

            if (json.Value<bool?>("ok") != true)
            {
                return releases;
            }

            var deezerUrl = json.Value<string>("deezer_url");
            var deezerId = json.Value<long?>("deezer_id");

            if (deezerUrl.IsNullOrWhiteSpace() || deezerId == null)
            {
                return releases;
            }

            var query = json["query"] as JObject;
            var artist = query?.Value<string>("artist") ?? string.Empty;
            var album = query?.Value<string>("album") ?? string.Empty;
            var year = query?.Value<int?>("year");

            var nbTracks = json.Value<int?>("deezer_nb_tracks");

            // Approximate FLAC size: ~45 MB per track average, album default otherwise.
            var size = nbTracks.HasValue && nbTracks.Value > 0
                ? nbTracks.Value * 45_000_000L
                : 400_000_000L;

            var title = year.HasValue && year.Value > 0
                ? $"{artist} - {album} ({year.Value}) [FLAC] [Antra]"
                : $"{artist} - {album} [FLAC] [Antra]";

            releases.Add(new ReleaseInfo
            {
                Guid = $"Antra-{deezerId.Value}-{nbTracks ?? 0}-FLAC",
                Title = title,
                Artist = artist,
                Album = album,
                Size = size,
                DownloadUrl = deezerUrl,
                InfoUrl = deezerUrl,
                PublishDate = year.HasValue && year.Value > 0
                    ? new DateTime(year.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    : DateTime.UtcNow,
                DownloadProtocol = nameof(AntraDownloadProtocol),
                Codec = "FLAC",
                Container = "Lossless"
            });

            return releases;
        }
    }
}
