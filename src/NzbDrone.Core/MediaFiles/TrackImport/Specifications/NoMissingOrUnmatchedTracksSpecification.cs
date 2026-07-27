using System.Linq;
using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.TrackImport.Specifications
{
    public class NoMissingOrUnmatchedTracksSpecification : IImportDecisionEngineSpecification<LocalAlbumRelease>
    {
        private readonly Logger _logger;

        public NoMissingOrUnmatchedTracksSpecification(Logger logger)
        {
            _logger = logger;
        }

        public Decision IsSatisfiedBy(LocalAlbumRelease item, DownloadClientItem downloadClientItem)
        {
            if (item.NewDownload && item.TrackMapping.LocalExtra.Count > 0)
            {
                _logger.Debug("This release has track files that have not been matched. Skipping {0}", item);
                return Decision.Reject("Has unmatched tracks");
            }

            // Song mode: a grab carrying fewer tracks than the matched release is a
            // deliberate partial import — the rest of the monitored tracks arrive via
            // their own searches, and a single grab can still deliver several files.
            // Only a download claiming to be the whole release must cover the monitored
            // track set. Tracks that already have a file on disk never count as missing.
            var releaseTrackCount = item.AlbumRelease?.TrackCount ?? item.TrackCount;

            if (item.NewDownload && item.TrackCount >= releaseTrackCount && item.TrackMapping.MBExtra.Any(t => t.Monitored && t.TrackFileId == 0))
            {
                _logger.Debug("This release is missing monitored tracks. Skipping {0}", item);
                return Decision.Reject("Has missing monitored tracks");
            }

            return Decision.Accept();
        }
    }
}
