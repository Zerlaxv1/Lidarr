using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerSearchTests
{
    [TestFixture]
    public class TrackSearchServiceFixture : CoreTest<AlbumSearchService>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.TrackSearch(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()))
                  .Returns(Task.FromResult(new List<DownloadDecision>()));

            Mocker.GetMock<IProcessDownloadDecisions>()
                  .Setup(s => s.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                  .Returns(Task.FromResult(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>())));
        }

        [Test]
        public void should_track_search_each_requested_track()
        {
            Subject.Execute(new TrackSearchCommand { TrackIds = new List<int> { 11, 12 }, Trigger = CommandTrigger.Manual });

            Mocker.GetMock<ISearchForReleases>().Verify(s => s.TrackSearch(11, true, false), Times.Once());
            Mocker.GetMock<ISearchForReleases>().Verify(s => s.TrackSearch(12, true, false), Times.Once());
        }
    }
}
