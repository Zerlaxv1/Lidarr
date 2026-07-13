using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.AlbumServiceTests
{
    [TestFixture]
    public class SetMonitoredCascadeFixture : CoreTest<AlbumService>
    {
        [Test]
        public void should_cascade_monitored_to_tracks_of_album()
        {
            var tracks = new List<Track>
            {
                new Track { Id = 10 },
                new Track { Id = 11 },
                new Track { Id = 12 }
            };

            Mocker.GetMock<IAlbumRepository>()
                  .Setup(s => s.Get(It.IsAny<IEnumerable<int>>()))
                  .Returns(new List<Album>());

            Mocker.GetMock<ITrackService>()
                  .Setup(s => s.GetTracksByAlbum(5))
                  .Returns(tracks);

            Subject.SetMonitored(new List<int> { 5 }, false);

            Mocker.GetMock<ITrackService>()
                  .Verify(v => v.SetMonitored(
                      It.Is<IEnumerable<int>>(x => x.SequenceEqual(new[] { 10, 11, 12 })),
                      false),
                      Times.Once());
        }
    }
}
