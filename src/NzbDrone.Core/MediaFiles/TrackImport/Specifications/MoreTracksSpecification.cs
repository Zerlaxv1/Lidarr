using System.Linq;
using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.TrackImport.Specifications
{
    public class MoreTracksSpecification : IImportDecisionEngineSpecification<LocalAlbumRelease>
    {
        private readonly Logger _logger;

        public MoreTracksSpecification(Logger logger)
        {
            _logger = logger;
        }

        public Decision IsSatisfiedBy(LocalAlbumRelease item, DownloadClientItem downloadClientItem)
        {
            // A song-mode grab deliberately brings part of a release - one track, usually.
            // "Fewer tracks than what is already on disk" is then the normal case, not a
            // reason to refuse it: it adds a track instead of replacing the album. Same
            // partial test the other import specs use.
            if (item.TrackCount < (item.AlbumRelease?.TrackCount ?? item.TrackCount))
            {
                _logger.Trace("Accepting partial release {0}", item);
                return Decision.Accept();
            }

            var existingRelease = item.AlbumRelease.Album.Value.AlbumReleases.Value.Single(x => x.Monitored);
            var existingTrackCount = existingRelease.Tracks.Value.Count(x => x.HasFile);
            if (item.AlbumRelease.Id != existingRelease.Id &&
                item.TrackCount < existingTrackCount)
            {
                _logger.Debug($"This release has fewer tracks ({item.TrackCount}) than existing {existingRelease} ({existingTrackCount}). Skipping {item}");
                return Decision.Reject("Has fewer tracks than existing release");
            }

            _logger.Trace("Accepting release {0}", item);
            return Decision.Accept();
        }
    }
}
