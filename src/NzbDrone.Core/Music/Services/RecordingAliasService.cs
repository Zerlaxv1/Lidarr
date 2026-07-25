using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.Music
{
    public interface IProvideRecordingAliases
    {
        Dictionary<string, List<string>> GetAliases(int albumReleaseId);
    }

    // MusicBrainz stores a recording's transliterations as aliases - 悪魔の子 carries
    // "Akuma no Ko" - but the metadata server strips them from the track payload, so a
    // list that spells titles the other way round matches nothing. One browse request
    // returns every recording on a release with its aliases, which is cheap enough to do
    // as a last resort for an album whose titles did not match.
    public class RecordingAliasService : IProvideRecordingAliases
    {
        private const string BrowseUrl = "https://musicbrainz.org/ws/2/recording";

        private readonly IReleaseService _releaseService;
        private readonly IHttpClient _httpClient;
        private readonly ICached<Dictionary<string, List<string>>> _cache;
        private readonly Logger _logger;

        public RecordingAliasService(IReleaseService releaseService,
                                     IHttpClient httpClient,
                                     ICacheManager cacheManager,
                                     Logger logger)
        {
            _releaseService = releaseService;
            _httpClient = httpClient;
            _cache = cacheManager.GetCache<Dictionary<string, List<string>>>(GetType());
            _logger = logger;
        }

        public Dictionary<string, List<string>> GetAliases(int albumReleaseId)
        {
            var release = _releaseService.GetRelease(albumReleaseId);

            if (release?.ForeignReleaseId.IsNullOrWhiteSpace() != false)
            {
                return new Dictionary<string, List<string>>();
            }

            return _cache.Get(release.ForeignReleaseId, () => Fetch(release.ForeignReleaseId), TimeSpan.FromHours(24));
        }

        private Dictionary<string, List<string>> Fetch(string foreignReleaseId)
        {
            var result = new Dictionary<string, List<string>>();

            try
            {
                var request = new HttpRequestBuilder(BrowseUrl)
                    .AddQueryParam("release", foreignReleaseId)
                    .AddQueryParam("inc", "aliases")
                    .AddQueryParam("fmt", "json")
                    .AddQueryParam("limit", "100")
                    .WithRateLimit(1.0)
                    .Build();

                var response = _httpClient.Get<RecordingBrowseResource>(request);

                foreach (var recording in response?.Resource?.Recordings ?? new List<RecordingResource>())
                {
                    if (recording.Id.IsNullOrWhiteSpace() || recording.Aliases == null)
                    {
                        continue;
                    }

                    var names = recording.Aliases
                        .Select(a => a.Name)
                        .Where(n => n.IsNotNullOrWhiteSpace())
                        .ToList();

                    if (names.Any())
                    {
                        result[recording.Id] = names;
                    }
                }
            }
            catch (Exception ex)
            {
                // Aliases only ever add matches, so a MusicBrainz outage must not break the sync.
                _logger.Debug(ex, "Could not fetch recording aliases for release {0}", foreignReleaseId);
            }

            return result;
        }

        private class RecordingBrowseResource
        {
            [JsonProperty("recordings")]
            public List<RecordingResource> Recordings { get; set; }
        }

        private class RecordingResource
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("aliases")]
            public List<AliasResource> Aliases { get; set; }
        }

        private class AliasResource
        {
            [JsonProperty("name")]
            public string Name { get; set; }
        }
    }
}
