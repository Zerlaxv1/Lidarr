using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.TrackImport.Specifications;
using NzbDrone.Core.Music;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.TrackImport.Specifications
{
    [TestFixture]
    public class MoreTracksSpecificationFixture : CoreTest<MoreTracksSpecification>
    {
        private Album _album;
        private AlbumRelease _monitoredRelease;
        private AlbumRelease _otherRelease;

        [SetUp]
        public void Setup()
        {
            _monitoredRelease = new AlbumRelease { Id = 1, Monitored = true, TrackCount = 12 };
            _monitoredRelease.Tracks = new List<Track>
            {
                new Track { Id = 1, TrackFileId = 100 },
                new Track { Id = 2, TrackFileId = 101 },
                new Track { Id = 3, TrackFileId = 0 }
            };

            _otherRelease = new AlbumRelease { Id = 2, Monitored = false, TrackCount = 12 };

            _album = new Album { Id = 5 };
            _album.AlbumReleases = new List<AlbumRelease> { _monitoredRelease, _otherRelease };
            _monitoredRelease.Album = _album;
            _otherRelease.Album = _album;
        }

        private LocalAlbumRelease Local(AlbumRelease release, int trackCount)
        {
            var tracks = new List<LocalTrack>();

            for (var i = 0; i < trackCount; i++)
            {
                tracks.Add(new LocalTrack());
            }

            return new LocalAlbumRelease(tracks) { AlbumRelease = release };
        }

        [Test]
        public void should_accept_a_single_track_grab_from_another_release()
        {
            // Song mode grabs one track at a time. It carries fewer tracks than the album
            // already has on disk by design, which is not a reason to refuse it - it adds a
            // track rather than replacing the album.
            var result = Subject.IsSatisfiedBy(Local(_otherRelease, 1), null);

            result.Accepted.Should().BeTrue();
        }

        [Test]
        public void should_still_reject_a_smaller_full_release()
        {
            // A release that is complete in itself yet holds less than what we already have
            // on the monitored one is the case this specification exists for: one track that
            // is the whole release, against the two files already on disk.
            _otherRelease.TrackCount = 1;

            var result = Subject.IsSatisfiedBy(Local(_otherRelease, 1), null);

            result.Accepted.Should().BeFalse();
        }
    }
}
