using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine.Specifications.Search;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class TrackRequestedSpecificationFixture : CoreTest<TrackRequestedSpecification>
    {
        private RemoteAlbum GivenRelease(string title)
        {
            return new RemoteAlbum
            {
                Release = new ReleaseInfo { Title = title }
            };
        }

        [Test]
        public void should_accept_a_release_naming_the_searched_track()
        {
            var criteria = new TrackSearchCriteria { TrackTitle = "Muscle Museum" };
            var release = GivenRelease("Muse - Showbiz (1999) [FLAC 16bit] [Track: Muscle Museum] [WEB]");

            Subject.IsSatisfiedBy(release, criteria).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_a_whole_album_returned_for_a_track_search()
        {
            var criteria = new TrackSearchCriteria { TrackTitle = "Muscle Museum" };
            var release = GivenRelease("Muse - Showbiz (1999) [FLAC 16bit] [WEB]");

            Subject.IsSatisfiedBy(release, criteria).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_ignore_punctuation_and_case()
        {
            var criteria = new TrackSearchCriteria { TrackTitle = "B.Y.O.B." };
            var release = GivenRelease("System of a Down - Mezmerize [Track: byob]");

            Subject.IsSatisfiedBy(release, criteria).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_not_apply_to_an_album_search()
        {
            var criteria = new AlbumSearchCriteria { AlbumTitle = "Showbiz" };
            var release = GivenRelease("Muse - Showbiz (1999) [FLAC 16bit] [WEB]");

            Subject.IsSatisfiedBy(release, criteria).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_not_apply_to_rss()
        {
            var release = GivenRelease("Muse - Showbiz (1999) [FLAC 16bit] [WEB]");

            Subject.IsSatisfiedBy(release, null).Accepted.Should().BeTrue();
        }
    }
}
