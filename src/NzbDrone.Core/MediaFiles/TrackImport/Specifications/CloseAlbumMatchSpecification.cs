using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.TrackImport.Specifications
{
    public class CloseAlbumMatchSpecification : IImportDecisionEngineSpecification<LocalAlbumRelease>
    {
        private const double _trackThreshold = 0.40;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public CloseAlbumMatchSpecification(IConfigService configService, Logger logger)
        {
            _configService = configService;
            _logger = logger;
        }

        // The user configures a minimum match score (a percentage, 80 by default) because
        // that is what the rejection message already reports; internally this stays a
        // distance threshold, so an 80% minimum score is a maximum distance of 0.20.
        // Subtracting the percentages rather than 1.0 - (score / 100.0) keeps the default
        // exactly 0.20 instead of a hair under it.
        private double AlbumThreshold => (100 - Math.Clamp(_configService.MinimumAlbumMatchScore, 0, 100)) / 100.0;

        public Decision IsSatisfiedBy(LocalAlbumRelease item, DownloadClientItem downloadClientItem)
        {
            var albumThreshold = AlbumThreshold;
            double dist;
            string reasons;

            // strict when a new download
            if (item.NewDownload)
            {
                // A song-mode / single-track grab carries fewer tracks than the full
                // release and cannot satisfy album-completeness attributes (missing /
                // unmatched tracks) or album-level tags a single-track source rarely sets
                // (label, media format). For a partial grab, judge on artist / album /
                // year plus the quality of the track(s) actually present (the worst-track
                // check below) rather than on completeness — the same reasoning the
                // existing-files branch already applies for the track-count penalties.
                var releaseTrackCount = item.AlbumRelease?.TrackCount ?? item.TrackCount;
                dist = item.TrackCount < releaseTrackCount
                    ? item.Distance.NormalizedDistanceExcluding(new List<string> { "missing_tracks", "unmatched_tracks", "label", "media_format" })
                    : item.Distance.NormalizedDistance();
                reasons = item.Distance.Reasons;
                if (dist > albumThreshold)
                {
                    _logger.Debug($"Album match is not close enough: {dist} vs {albumThreshold} {reasons}. Skipping {item}");
                    return Decision.Reject($"Album match is not close enough: {1 - dist:P1} vs {1 - albumThreshold:P0} {reasons}");
                }

                var worstTrackMatch = item.LocalTracks.Where(x => x.Distance != null).MaxBy(x => x.Distance.NormalizedDistance());
                if (worstTrackMatch == null)
                {
                    _logger.Debug($"No tracks matched");
                    return Decision.Reject("No tracks matched");
                }
                else
                {
                    var maxTrackDist = worstTrackMatch.Distance.NormalizedDistance();
                    var trackReasons = worstTrackMatch.Distance.Reasons;
                    if (maxTrackDist > _trackThreshold)
                    {
                        _logger.Debug($"Worst track match: {maxTrackDist} vs {_trackThreshold} {trackReasons}. Skipping {item}");
                        return Decision.Reject($"Worst track match: {1 - maxTrackDist:P1} vs {1 - _trackThreshold:P0} {trackReasons}");
                    }
                }
            }

            // otherwise importing existing files in library
            else
            {
                // get album distance ignoring whether tracks are missing
                dist = item.Distance.NormalizedDistanceExcluding(new List<string> { "missing_tracks", "unmatched_tracks" });
                reasons = item.Distance.Reasons;
                if (dist > albumThreshold)
                {
                    _logger.Debug($"Album match is not close enough: {dist} vs {albumThreshold} {reasons}. Skipping {item}");
                    return Decision.Reject($"Album match is not close enough: {1 - dist:P1} vs {1 - albumThreshold:P0} {reasons}");
                }
            }

            _logger.Debug($"Accepting release {item}: dist {dist} vs {albumThreshold} {reasons}");
            return Decision.Accept();
        }
    }
}
