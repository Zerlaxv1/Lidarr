using System.Collections.Generic;
using System.Threading.Tasks;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Music;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerSearchTests
{
    [TestFixture]
    public class MissingAlbumSearchServiceFixture : CoreTest<AlbumSearchService>
    {
        [SetUp]
        public void Setup()
        {
            var fullyMonitored = Builder<Album>.CreateNew().With(a => a.Id = 1).Build();
            var partiallyMonitored = Builder<Album>.CreateNew().With(a => a.Id = 2).Build();

            Mocker.GetMock<IAlbumService>()
                  .Setup(s => s.AlbumsWithoutFiles(It.IsAny<PagingSpec<Album>>()))
                  .Returns<PagingSpec<Album>>(spec =>
                  {
                      spec.Records = new List<Album> { fullyMonitored, partiallyMonitored };
                      return spec;
                  });

            Mocker.GetMock<ITrackService>()
                  .Setup(s => s.GetTracksByAlbum(1))
                  .Returns(Builder<Track>.CreateListOfSize(2)
                      .All()
                      .With(t => t.Monitored = true)
                      .With(t => t.TrackFileId = 0)
                      .BuildList());

            Mocker.GetMock<ITrackService>()
                  .Setup(s => s.GetTracksByAlbum(2))
                  .Returns(Builder<Track>.CreateListOfSize(3)
                      .All()
                      .With(t => t.TrackFileId = 0)
                      .TheFirst(1)
                      .With(t => t.Id = 21)
                      .With(t => t.Monitored = true)
                      .TheNext(2)
                      .With(t => t.Monitored = false)
                      .BuildList());

            Mocker.GetMock<IQueueService>()
                  .Setup(s => s.GetQueue())
                  .Returns(new List<Queue.Queue>());

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.AlbumSearch(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                  .Returns(Task.FromResult(new List<DownloadDecision>()));

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.TrackSearch(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()))
                  .Returns(Task.FromResult(new List<DownloadDecision>()));

            Mocker.GetMock<IProcessDownloadDecisions>()
                  .Setup(s => s.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                  .Returns(Task.FromResult(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>())));
        }

        [Test]
        public void should_album_search_fully_monitored_albums_and_track_search_partial_ones()
        {
            Subject.Execute(new MissingAlbumSearchCommand());

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(s => s.AlbumSearch(1, false, false, false), Times.Once());

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(s => s.TrackSearch(21, false, false), Times.Once());

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(s => s.AlbumSearch(2, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(s => s.TrackSearch(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Once());
        }
    }
}
