using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.TrackImport.Identification;
using NzbDrone.Core.MediaFiles.TrackImport.Specifications;
using NzbDrone.Core.Music;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.TrackImport.Specifications
{
    [TestFixture]
    public class CloseAlbumMatchSpecificationFixture : CoreTest<CloseAlbumMatchSpecification>
    {
        // A single-track song-mode grab: one local track against a 13-track release, with
        // the inherent missing-track penalty and a label the source didn't tag. Without the
        // partial-grab exclusion this album distance sits above the 0.20 threshold.
        private static LocalAlbumRelease GivenSingleTrackGrab(double trackDist)
        {
            var albumDistance = new Distance();
            albumDistance.Add("artist", 0.0);
            albumDistance.Add("album", 0.0);
            albumDistance.Add("label", 1.0);
            for (var i = 0; i < 12; i++)
            {
                albumDistance.Add("missing_tracks", 1.0);
            }

            albumDistance.Add("tracks", trackDist);

            var trackDistance = new Distance();
            trackDistance.Add("track_title", trackDist);

            return new LocalAlbumRelease
            {
                NewDownload = true,
                Distance = albumDistance,
                AlbumRelease = new AlbumRelease { TrackCount = 13 },
                LocalTracks = new List<LocalTrack> { new LocalTrack { Distance = trackDistance } }
            };
        }

        [Test]
        public void should_accept_single_track_grab_despite_missing_tracks_and_label()
        {
            var release = GivenSingleTrackGrab(trackDist: 0.05);

            Subject.IsSatisfiedBy(release, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_single_track_grab_when_the_present_track_matches_poorly()
        {
            // Album-completeness is excluded for a partial grab, but the track actually
            // present must still match closely (worst-track threshold 0.40).
            var release = GivenSingleTrackGrab(trackDist: 0.6);

            Subject.IsSatisfiedBy(release, null).Accepted.Should().BeFalse();
        }
    }
}
