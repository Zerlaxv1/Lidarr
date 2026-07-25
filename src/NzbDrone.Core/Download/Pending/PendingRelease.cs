using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Download.Pending
{
    public class PendingRelease : ModelBase
    {
        public int ArtistId { get; set; }
        public string Title { get; set; }
        public DateTime Added { get; set; }
        public ParsedAlbumInfo ParsedAlbumInfo { get; set; }
        public ReleaseInfo Release { get; set; }
        public PendingReleaseReason Reason { get; set; }
        public PendingReleaseAdditionalInfo AdditionalInfo { get; set; }

        // Not persisted
        public RemoteAlbum RemoteAlbum { get; set; }
    }

    public class PendingReleaseAdditionalInfo
    {
        public ReleaseSourceType ReleaseSource { get; set; }

        // Which tracks the grab was for, when it was for specific ones. RemoteAlbum is
        // rebuilt from the parsed info on read and would otherwise forget them, leaving
        // one track's grab free to discard another track's pending release.
        public List<int> TrackIds { get; set; }
    }
}
