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

            // Song mode: a single-file download is a deliberate track grab — other
            // monitored tracks arrive via their own searches, so only album-shaped
            // (multi-file) downloads must cover the monitored track set. Tracks that
            // already have a file on disk never count as missing.
            if (item.NewDownload && item.LocalTracks.Count > 1 && item.TrackMapping.MBExtra.Any(t => t.Monitored && t.TrackFileId == 0))
            {
                _logger.Debug("This release is missing monitored tracks. Skipping {0}", item);
                return Decision.Reject("Has missing monitored tracks");
            }

            return Decision.Accept();
        }
    }
}
