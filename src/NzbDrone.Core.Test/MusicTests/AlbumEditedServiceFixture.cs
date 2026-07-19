using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class AlbumEditedServiceFixture : CoreTest<AlbumEditedService>
    {
        private Album BuildAlbum(int releaseId, bool monitored, bool anyReleaseOk)
        {
            var release = new AlbumRelease { Id = releaseId, Monitored = monitored };
            var artist = new Artist { Path = @"C:\Music\Muse".AsOsAgnostic() };

            return new Album
            {
                Id = 1,
                AnyReleaseOk = anyReleaseOk,
                AlbumReleases = new List<AlbumRelease> { release },
                Artist = artist
            };
        }

        [Test]
        public void should_relink_tracks_when_monitored_release_changes()
        {
            var oldAlbum = BuildAlbum(1, true, false);
            var newAlbum = BuildAlbum(2, true, false);

            Subject.Handle(new AlbumEditedEvent(newAlbum, oldAlbum));

            Mocker.GetMock<ITrackService>()
                .Verify(s => s.RelinkTrackFilesToRelease(
                    It.Is<AlbumRelease>(r => r.Id == 2),
                    It.Is<List<AlbumRelease>>(l => l.Single().Id == 1)), Times.Once());
        }

        [Test]
        public void should_not_relink_when_monitored_release_unchanged()
        {
            var oldAlbum = BuildAlbum(1, true, false);
            var newAlbum = BuildAlbum(1, true, false);

            Subject.Handle(new AlbumEditedEvent(newAlbum, oldAlbum));

            Mocker.GetMock<ITrackService>()
                .Verify(s => s.RelinkTrackFilesToRelease(It.IsAny<AlbumRelease>(), It.IsAny<List<AlbumRelease>>()), Times.Never());
        }

        [Test]
        public void should_push_rescan_as_secondary_net_when_release_changes()
        {
            var oldAlbum = BuildAlbum(1, true, false);
            var newAlbum = BuildAlbum(2, true, false);

            Subject.Handle(new AlbumEditedEvent(newAlbum, oldAlbum));

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(s => s.Push(It.IsAny<RescanFoldersCommand>(), CommandPriority.Normal, CommandTrigger.Unspecified), Times.Once());
        }

        [Test]
        public void should_not_zero_out_track_file_ids()
        {
            var oldAlbum = BuildAlbum(1, true, false);
            var newAlbum = BuildAlbum(2, true, false);

            Subject.Handle(new AlbumEditedEvent(newAlbum, oldAlbum));

            Mocker.GetMock<ITrackService>()
                .Verify(s => s.SetFileIds(It.IsAny<List<Track>>()), Times.Never());
        }
    }
}
