using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.MetadataSource.MusicBrainz
{
    public interface IResolveAlbumByAlias
    {
        string FindReleaseGroupId(string album, string artist);
    }

    // A list that writes names in another script than MusicBrainz stores them ("Saiaku
    // Saiai" for the Japanese title) resolves through neither the id mapping nor the
    // metadata server's name search, and the album is dropped. MusicBrainz keeps the
    // transliteration as an alias, so search there instead - but an alias alone is not
    // enough: alias:"Oddloop" also matches a cover by an unrelated artist, and
    // alias:"frederic" matches Chopin. Only accept a release group when the artist
    // matched too.
    public class MusicBrainzAlbumResolver : IResolveAlbumByAlias
    {
        private const string ArtistUrl = "https://musicbrainz.org/ws/2/artist";
        private const string ReleaseGroupUrl = "https://musicbrainz.org/ws/2/release-group";
        private const int MinimumScore = 90;
        private const string NoMatch = "";

        private readonly IHttpClient _httpClient;
        private readonly ICached<string> _cache;
        private readonly Logger _logger;

        public MusicBrainzAlbumResolver(IHttpClient httpClient, ICacheManager cacheManager, Logger logger)
        {
            _httpClient = httpClient;
            _cache = cacheManager.GetCache<string>(GetType());
            _logger = logger;
        }

        public string FindReleaseGroupId(string album, string artist)
        {
            if (album.IsNullOrWhiteSpace() || artist.IsNullOrWhiteSpace())
            {
                return null;
            }

            var key = $"{artist} {album}";
            var cached = _cache.Find(key);

            if (cached != null)
            {
                return cached.Length == 0 ? null : cached;
            }

            var resolved = Resolve(album, artist, out var answered);

            // MusicBrainz throttles with a 503, and that is not an answer - caching it
            // would suppress this album for the next 24 hours over one rate limit.
            if (answered)
            {
                _cache.Set(key, resolved ?? NoMatch, TimeSpan.FromHours(24));
            }

            return resolved;
        }

        private string Resolve(string album, string artist, out bool answered)
        {
            answered = true;

            try
            {
                foreach (var artistId in FindArtistIds(artist))
                {
                    var releaseGroupId = FindReleaseGroup(album, artistId);

                    if (releaseGroupId.IsNotNullOrWhiteSpace())
                    {
                        _logger.Debug("Resolved [{0}] by [{1}] to release group {2} through MusicBrainz aliases", album, artist, releaseGroupId);
                        return releaseGroupId;
                    }
                }
            }
            catch (Exception ex)
            {
                // This only ever adds matches, so an outage must not break the sync.
                answered = false;
                _logger.Warn(ex, "Could not reach MusicBrainz to resolve [{0}] by [{1}]", album, artist);
            }

            return null;
        }

        private List<string> FindArtistIds(string artist)
        {
            var response = Search<ArtistSearchResource>(ArtistUrl, $"alias:\"{Escape(artist)}\" OR artist:\"{Escape(artist)}\"");

            return response?.Artists?
                .Where(a => a.Score >= MinimumScore && a.Id.IsNotNullOrWhiteSpace())
                .Select(a => a.Id)
                .Take(3)
                .ToList() ?? new List<string>();
        }

        private string FindReleaseGroup(string album, string artistId)
        {
            var query = $"arid:{artistId} AND (releasegroup:\"{Escape(album)}\" OR alias:\"{Escape(album)}\")";
            var response = Search<ReleaseGroupSearchResource>(ReleaseGroupUrl, query);

            return response?.ReleaseGroups?
                .Where(r => r.Score >= MinimumScore)
                .Select(r => r.Id)
                .FirstOrDefault();
        }

        private T Search<T>(string url, string query)
            where T : new()
        {
            var request = new HttpRequestBuilder(url)
                .AddQueryParam("query", query)
                .AddQueryParam("fmt", "json")
                .AddQueryParam("limit", "5")
                .WithRateLimit(1.5)
                .Build();

            return _httpClient.Get<T>(request).Resource;
        }

        // Lucene syntax: a quote or backslash inside the term would break the query.
        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private class ArtistSearchResource
        {
            [JsonProperty("artists")]
            public List<SearchHit> Artists { get; set; }
        }

        private class ReleaseGroupSearchResource
        {
            [JsonProperty("release-groups")]
            public List<SearchHit> ReleaseGroups { get; set; }
        }

        private class SearchHit
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("score")]
            public int Score { get; set; }
        }
    }
}
