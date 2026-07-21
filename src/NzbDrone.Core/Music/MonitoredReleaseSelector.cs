using System;
using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Music
{
    public static class MonitoredReleaseSelector
    {
        // Single source of truth for "which release should be the monitored one".
        // Files on disk win first (never orphan content we already have), then a
        // clean standard edition: Digital Media format, then [Worldwide] country,
        // then track count as a final tiebreak. Used both for the add-time pick
        // (SkyHookProxy, no files yet) and the refresh-time pick (RefreshAlbumService).
        public static AlbumRelease SelectPreferred(IEnumerable<AlbumRelease> releases, Func<AlbumRelease, int> fileCount)
        {
            return releases
                .OrderByDescending(x => fileCount(x))
                .ThenByDescending(x => x.Media != null && x.Media.Any(m => m.Format != null && m.Format.Equals("Digital Media", StringComparison.OrdinalIgnoreCase)))
                .ThenByDescending(x => x.Country != null && x.Country.Any(c => c != null && c.Equals("[Worldwide]", StringComparison.OrdinalIgnoreCase)))
                .ThenByDescending(x => x.TrackCount)
                .First();
        }
    }
}
