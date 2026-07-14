using System.Collections.Generic;
using System.Threading.Tasks;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Music;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerSearchTests
{
    [TestFixture]
    public class ReleaseSearchServiceFixture : CoreTest<ReleaseSearchService>
    {
        private Music.Artist _artist;
        private Album _album;
        private Track _track;
        private Mock<IIndexer> _indexer;
        private AlbumSearchCriteria _captured;

        [SetUp]
        public void Setup()
        {
            _artist = Builder<Music.Artist>.CreateNew()
                .With(a => a.Id = 5)
                .With(a => a.Name = "System of a Down")
                .With(a => a.Tags = new HashSet<int>())
                .Build();

            _album = Builder<Album>.CreateNew()
                .With(a => a.Id = 100)
                .With(a => a.ArtistId = 5)
                .With(a => a.Title = "Mezmerize")
                .With(a => a.Disambiguation = null)
                .Build();

            _track = Builder<Track>.CreateNew()
                .With(t => t.Id = 11)
                .With(t => t.Title = "B.Y.O.B.")
                .Build();

            Mocker.GetMock<ITrackService>().Setup(s => s.GetTrack(11)).Returns(_track);
            Mocker.GetMock<IAlbumService>().Setup(s => s.FindAlbumByTrackId(11)).Returns(_album);
            Mocker.GetMock<IArtistService>().Setup(s => s.GetArtist(5)).Returns(_artist);

            _indexer = new Mock<IIndexer>();
            _indexer.SetupGet(x => x.Definition).Returns(new IndexerDefinition { Tags = new HashSet<int>() });
            _indexer.Setup(x => x.Fetch(It.IsAny<AlbumSearchCriteria>()))
                    .Callback<AlbumSearchCriteria>(c => _captured = c)
                    .ReturnsAsync(new List<ReleaseInfo>());

            Mocker.GetMock<IIndexerFactory>()
                  .Setup(x => x.AutomaticSearchEnabled(true))
                  .Returns(new List<IIndexer> { _indexer.Object });

            Mocker.GetMock<IMakeDownloadDecision>()
                  .Setup(x => x.GetSearchDecision(It.IsAny<List<ReleaseInfo>>(), It.IsAny<SearchCriteriaBase>()))
                  .Returns(new List<DownloadDecision>());
        }

        [Test]
        public void track_search_should_dispatch_track_criteria()
        {
            Subject.TrackSearch(11, true, false).GetAwaiter().GetResult();

            _captured.Should().BeOfType<TrackSearchCriteria>();

            var criteria = (TrackSearchCriteria)_captured;
            criteria.AlbumTitle.Should().Be("Mezmerize");
            criteria.TrackTitle.Should().Be("B.Y.O.B.");
            criteria.Artist.Id.Should().Be(5);
            criteria.Tracks.Should().HaveCount(1);
            criteria.Tracks[0].Id.Should().Be(11);
        }
    }
}
