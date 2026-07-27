using System.Collections.Generic;
using System.Linq;
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
        private LocalAlbumRelease Given(int localTrackCount, List<Track> mbExtra, int? releaseTrackCount = null)
        {
            return new LocalAlbumRelease
            {
                NewDownload = true,
                LocalTracks = Enumerable.Range(0, localTrackCount).Select(x => new LocalTrack()).ToList(),
                AlbumRelease = releaseTrackCount.HasValue ? new AlbumRelease { TrackCount = releaseTrackCount.Value } : null,
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
            var item = Given(2, new List<Track> { new Track { Monitored = false } });
            Subject.IsSatisfiedBy(item, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_full_release_download_when_a_monitored_track_is_missing()
        {
            var item = Given(2, new List<Track> { new Track { Monitored = true }, new Track { Monitored = false } }, releaseTrackCount: 2);
            Subject.IsSatisfiedBy(item, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_accept_single_file_download_even_when_other_monitored_tracks_are_missing()
        {
            // song mode: a single-file download is a deliberate track grab; the other
            // monitored tracks of the album get their own searches
            var item = Given(1, new List<Track> { new Track { Monitored = true } }, releaseTrackCount: 12);
            Subject.IsSatisfiedBy(item, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_partial_grab_of_several_files_even_when_other_monitored_tracks_are_missing()
        {
            // one song-mode grab can hand us more than one file (a source that only
            // serves whole-album archives, several tracks searched at once) and is
            // still partial: fewer files than the matched release actually has
            var item = Given(3, new List<Track> { new Track { Monitored = true } }, releaseTrackCount: 12);
            Subject.IsSatisfiedBy(item, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_multi_file_download_when_missing_monitored_tracks_already_have_files()
        {
            var item = Given(2, new List<Track> { new Track { Monitored = true, TrackFileId = 1 } }, releaseTrackCount: 2);
            Subject.IsSatisfiedBy(item, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_nothing_missing()
        {
            var item = Given(2, new List<Track>());
            Subject.IsSatisfiedBy(item, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_not_a_new_download()
        {
            var item = Given(2, new List<Track> { new Track { Monitored = true } });
            item.NewDownload = false;
            Subject.IsSatisfiedBy(item, null).Accepted.Should().BeTrue();
        }
    }
}
