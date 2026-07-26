using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.TrackRepositoryTests
{
    [TestFixture]
    public class SetMonitoredFixture : DbTest<TrackService, Track>
    {
        private TrackRepository _trackRepo;

        [SetUp]
        public void Setup()
        {
            _trackRepo = Mocker.Resolve<TrackRepository>();
        }

        [Test]
        public void should_set_monitored_flag_and_persist()
        {
            var tracks = new List<Track>
            {
                new Track { ForeignTrackId = "t1", ForeignRecordingId = "r1", Title = "T1", TrackNumber = "1", AlbumReleaseId = 1, ArtistMetadataId = 1, Monitored = true },
                new Track { ForeignTrackId = "t2", ForeignRecordingId = "r2", Title = "T2", TrackNumber = "2", AlbumReleaseId = 1, ArtistMetadataId = 1, Monitored = true }
            };

            _trackRepo.InsertMany(tracks);

            _trackRepo.SetMonitored(new[] { tracks[0].Id }, false);

            _trackRepo.Get(tracks[0].Id).Monitored.Should().BeFalse();
            _trackRepo.Get(tracks[1].Id).Monitored.Should().BeTrue();
        }
    }
}
