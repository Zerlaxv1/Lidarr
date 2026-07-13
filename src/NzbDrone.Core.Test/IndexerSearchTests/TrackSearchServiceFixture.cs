using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Music;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerSearchTests
{
    [TestFixture]
    public class TrackSearchServiceFixture : CoreTest<AlbumSearchService>
    {
        [Test]
        public void should_album_search_once_per_distinct_album_of_the_tracks()
        {
            Mocker.GetMock<IAlbumService>().Setup(s => s.FindAlbumByTrackId(11)).Returns(new Album { Id = 100 });
            Mocker.GetMock<IAlbumService>().Setup(s => s.FindAlbumByTrackId(12)).Returns(new Album { Id = 100 });

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.AlbumSearch(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                  .Returns(Task.FromResult(new List<DownloadDecision>()));

            Mocker.GetMock<IProcessDownloadDecisions>()
                  .Setup(s => s.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                  .Returns(Task.FromResult(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>())));

            Subject.Execute(new TrackSearchCommand { TrackIds = new List<int> { 11, 12 }, Trigger = CommandTrigger.Manual });

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(s => s.AlbumSearch(100, false, true, false), Times.Once());
        }
    }
}
