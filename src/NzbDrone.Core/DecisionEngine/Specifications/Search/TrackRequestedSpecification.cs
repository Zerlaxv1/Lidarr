using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Music;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications.Search
{
    // A track search is still an album search to every indexer that does not understand
    // TrackSearchCriteria, so they answer with the whole album - and nothing downstream
    // told those apart, because the album title matches either way. Song mode then grabs
    // twelve tracks for the one it asked for.
    public class TrackRequestedSpecification : IDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public TrackRequestedSpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public Decision IsSatisfiedBy(RemoteAlbum remoteAlbum, SearchCriteriaBase searchCriteria)
        {
            if (searchCriteria is not TrackSearchCriteria trackCriteria || trackCriteria.TrackTitle.IsNullOrWhiteSpace())
            {
                return Decision.Accept();
            }

            var releaseTitle = remoteAlbum.Release?.Title;

            if (releaseTitle.IsNullOrWhiteSpace())
            {
                return Decision.Accept();
            }

            var normalizedRelease = TrackService.NormalizeTrackTitleForMatch(releaseTitle);
            var normalizedTrack = TrackService.NormalizeTrackTitleForMatch(trackCriteria.TrackTitle);

            if (normalizedTrack.IsNullOrWhiteSpace() || normalizedRelease.Contains(normalizedTrack))
            {
                return Decision.Accept();
            }

            _logger.Debug("Release [{0}] does not name the searched track [{1}], skipping.", releaseTitle, trackCriteria.TrackTitle);

            return Decision.Reject("Doesn't contain the searched track: {0}", trackCriteria.TrackTitle);
        }
    }
}
