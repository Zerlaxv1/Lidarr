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
    public class NoMissingOrUnmatchedTracksSpecificationFixture : CoreTest<NoMissingOrUnmatchedTracksSpecification>
    {
        private LocalAlbumRelease Given(List<Track> mbExtra)
        {
            return new LocalAlbumRelease
            {
                NewDownload = true,
                TrackMapping = new TrackMapping
                {
                    LocalExtra = new List<LocalTrack>(),
                    MBExtra = mbExtra
                }
            };
        }

        [Test]
        public void should_accept_when_only_unmonitored_tracks_are_missing()
        {
            var item = Given(new List<Track> { new Track { Monitored = false } });
            Subject.IsSatisfiedBy(item, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_when_a_monitored_track_is_missing()
        {
            var item = Given(new List<Track> { new Track { Monitored = true }, new Track { Monitored = false } });
            Subject.IsSatisfiedBy(item, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_accept_when_nothing_missing()
        {
            var item = Given(new List<Track>());
            Subject.IsSatisfiedBy(item, null).Accepted.Should().BeTrue();
        }
    }
}
