using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.ImportLists.Exclusions;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Music;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ImportListTests
{
    public class ImportListSyncServiceFixture : CoreTest<ImportListSyncService>
    {
        private List<ImportListItemInfo> _importListReports;

        [SetUp]
        public void SetUp()
        {
            var importListItem1 = new ImportListItemInfo
            {
                Artist = "Linkin Park"
            };

            _importListReports = new List<ImportListItemInfo> { importListItem1 };

            var mockImportList = new Mock<IImportList>();

            Mocker.GetMock<ISearchForNewArtist>()
                .Setup(v => v.SearchForNewArtist(It.IsAny<string>()))
                .Returns(new List<Artist>());

            Mocker.GetMock<ISearchForNewAlbum>()
                .Setup(v => v.SearchForNewAlbum(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new List<Album>());

            Mocker.GetMock<IImportListFactory>()
                .Setup(v => v.All())
                .Returns(new List<ImportListDefinition> { new () { ShouldMonitor = ImportListMonitorType.SpecificAlbum } });

            Mocker.GetMock<IImportListFactory>()
                .Setup(v => v.AutomaticAddEnabled(It.IsAny<bool>()))
                .Returns(new List<IImportList> { mockImportList.Object });

            Mocker.GetMock<IFetchAndParseImportList>()
                .Setup(v => v.Fetch())
                .Returns(_importListReports);

            Mocker.GetMock<IImportListExclusionService>()
                .Setup(v => v.All())
                .Returns(new List<ImportListExclusion>());

            Mocker.GetMock<IAddArtistService>()
                .Setup(v => v.AddArtists(It.IsAny<List<Artist>>(), false, true))
                .Returns((List<Artist> artists, bool doRefresh, bool ignoreErrors) => artists);

            Mocker.GetMock<IAddAlbumService>()
                .Setup(v => v.AddAlbums(It.IsAny<List<Album>>(), false, true))
                .Returns((List<Album> albums, bool doRefresh, bool ignoreErrors) => albums);
        }

        private void WithAlbum()
        {
            _importListReports.First().Album = "Meteora";
        }

        private void WithArtistId()
        {
            _importListReports.First().ArtistMusicBrainzId = "f59c5520-5f46-4d2c-b2c4-822eabf53419";
        }

        private void WithAlbumId()
        {
            _importListReports.First().AlbumMusicBrainzId = "09474d62-17dd-3a4f-98fb-04c65f38a479";
        }

        private void WithSecondBook()
        {
            var importListItem2 = new ImportListItemInfo
            {
                Artist = "Linkin Park",
                ArtistMusicBrainzId = "f59c5520-5f46-4d2c-b2c4-822eabf53419",
                Album = "Meteora 2",
                AlbumMusicBrainzId = "madeup"
            };
            _importListReports.Add(importListItem2);
        }

        private void WithExistingArtist(bool monitored)
        {
            Mocker.GetMock<IArtistService>()
                .Setup(v => v.FindById(_importListReports.First().ArtistMusicBrainzId))
                .Returns(new Artist { Id = 1, ForeignArtistId = _importListReports.First().ArtistMusicBrainzId, Monitored = monitored });
        }

        private void WithExistingAlbum(bool monitored)
        {
            var album = Builder<Album>.CreateNew()
                .With(x => x.Id = 1)
                .With(x => x.ForeignAlbumId = _importListReports.First().AlbumMusicBrainzId)
                .With(x => x.Monitored = monitored)
                .Build();

            var artist = Builder<Artist>.CreateNew()
                .With(x => x.Monitored = monitored)
                .With(x => x.ForeignArtistId = _importListReports.First().ArtistMusicBrainzId)
                .With(x => x.Albums = new List<Album> { album })
                .Build();

            album.Artist = artist;

            Mocker.GetMock<IAlbumService>()
                .Setup(v => v.FindById(_importListReports.First().AlbumMusicBrainzId))
                .Returns(album);
        }

        private void WithExcludedArtist()
        {
            Mocker.GetMock<IImportListExclusionService>()
                .Setup(v => v.All())
                .Returns(new List<ImportListExclusion>
                {
                    new ImportListExclusion
                    {
                        ForeignId = "f59c5520-5f46-4d2c-b2c4-822eabf53419"
                    }
                });
        }

        private void WithExcludedAlbum()
        {
            Mocker.GetMock<IImportListExclusionService>()
                .Setup(v => v.All())
                .Returns(new List<ImportListExclusion>
                {
                    new ImportListExclusion
                    {
                        ForeignId = "09474d62-17dd-3a4f-98fb-04c65f38a479"
                    }
                });
        }

        private void WithListSettings(ImportListMonitorType monitor = ImportListMonitorType.EntireArtist, bool shouldMonitorExisting = false, bool shouldSearch = true)
        {
            Mocker.GetMock<IImportListFactory>()
                .Setup(v => v.All())
                .Returns(new List<ImportListDefinition> { new () { ShouldMonitor = monitor, ShouldMonitorExisting = shouldMonitorExisting, ShouldSearch = shouldSearch } });
        }

        [Test]
        public void should_search_if_artist_title_and_no_artist_id()
        {
            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<ISearchForNewArtist>()
                .Verify(v => v.SearchForNewArtist(It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_not_search_if_artist_title_and_artist_id()
        {
            WithArtistId();
            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<ISearchForNewArtist>()
                .Verify(v => v.SearchForNewArtist(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_search_if_album_title_and_no_album_id()
        {
            WithAlbum();
            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<ISearchForNewAlbum>()
                .Verify(v => v.SearchForNewAlbum(It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_not_search_if_album_title_and_album_id()
        {
            WithArtistId();
            WithAlbumId();
            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<ISearchForNewAlbum>()
                .Verify(v => v.SearchForNewAlbum(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_not_search_if_all_info()
        {
            WithArtistId();
            WithAlbum();
            WithAlbumId();
            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<ISearchForNewArtist>()
                .Verify(v => v.SearchForNewArtist(It.IsAny<string>()), Times.Never());

            Mocker.GetMock<ISearchForNewAlbum>()
                .Verify(v => v.SearchForNewAlbum(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_not_add_if_existing_artist()
        {
            WithArtistId();
            WithExistingArtist(false);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAddArtistService>()
                .Verify(v => v.AddArtists(It.Is<List<Artist>>(t => t.Count == 0), false, It.IsAny<bool>()));
        }

        [Test]
        public void should_not_add_if_existing_album()
        {
            WithAlbumId();
            WithExistingAlbum(false);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAddArtistService>()
                .Verify(v => v.AddArtists(It.Is<List<Artist>>(t => t.Count == 0), false, It.IsAny<bool>()));
        }

        [Test]
        public void should_add_if_existing_artist_but_new_album()
        {
            WithAlbumId();
            WithArtistId();
            WithExistingArtist(false);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAddAlbumService>()
                .Verify(v => v.AddAlbums(It.Is<List<Album>>(t => t.Count == 1), false, It.IsAny<bool>()));
        }

        [TestCase(ImportListMonitorType.None, false)]
        [TestCase(ImportListMonitorType.SpecificAlbum, true)]
        [TestCase(ImportListMonitorType.EntireArtist, true)]
        public void should_add_if_not_existing_artist(ImportListMonitorType monitor, bool expectedArtistMonitored)
        {
            WithArtistId();
            WithListSettings(monitor);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAddArtistService>()
                .Verify(v => v.AddArtists(It.Is<List<Artist>>(t => t.Count == 1 && t.First().Monitored == expectedArtistMonitored), false, It.IsAny<bool>()));
        }

        [TestCase(ImportListMonitorType.None, false)]
        [TestCase(ImportListMonitorType.SpecificAlbum, true)]
        [TestCase(ImportListMonitorType.EntireArtist, true)]
        public void should_add_if_not_existing_album(ImportListMonitorType monitor, bool expectedAlbumMonitored)
        {
            WithAlbumId();
            WithArtistId();
            WithListSettings(monitor);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAddAlbumService>()
                .Verify(v => v.AddAlbums(It.Is<List<Album>>(t => t.Count == 1 && t.First().Monitored == expectedAlbumMonitored), false, It.IsAny<bool>()));
        }

        [Test]
        public void should_not_add_artist_if_excluded_artist()
        {
            WithArtistId();
            WithExcludedArtist();

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAddArtistService>()
                .Verify(v => v.AddArtists(It.Is<List<Artist>>(t => t.Count == 0), false, It.IsAny<bool>()));
        }

        [Test]
        public void should_not_add_album_if_excluded_album()
        {
            WithAlbumId();
            WithArtistId();
            WithExcludedAlbum();

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAddAlbumService>()
                .Verify(v => v.AddAlbums(It.Is<List<Album>>(t => t.Count == 0), false, It.IsAny<bool>()));
        }

        [Test]
        public void should_not_add_album_if_excluded_artist()
        {
            WithAlbumId();
            WithArtistId();
            WithExcludedArtist();

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAddAlbumService>()
                .Verify(v => v.AddAlbums(It.Is<List<Album>>(t => t.Count == 0), false, It.IsAny<bool>()));
        }

        [TestCase(ImportListMonitorType.None, 0, false)]
        [TestCase(ImportListMonitorType.SpecificAlbum, 2, true)]
        [TestCase(ImportListMonitorType.EntireArtist, 0, true)]
        public void should_add_two_albums(ImportListMonitorType monitor, int expectedAlbumsMonitored, bool expectedArtistMonitored)
        {
            WithAlbum();
            WithAlbumId();
            WithSecondBook();
            WithArtistId();
            WithListSettings(monitor);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAddAlbumService>()
                .Verify(v => v.AddAlbums(It.Is<List<Album>>(t => t.Count == 2), false, true));
            Mocker.GetMock<IAddArtistService>()
                .Verify(v => v.AddArtists(It.Is<List<Artist>>(t => t.Count == 1 &&
                                                                   t.First().AddOptions.AlbumsToMonitor.Count == expectedAlbumsMonitored &&
                                                                   t.First().Monitored == expectedArtistMonitored),
                    false,
                    true));
        }

        [TestCase(ImportListMonitorType.SpecificAlbum)]
        [TestCase(ImportListMonitorType.EntireArtist)]
        public void should_monitor_existing_unmonitored_album(ImportListMonitorType monitorType)
        {
            WithAlbumId();
            WithArtistId();
            WithExistingAlbum(false);

            WithListSettings(monitorType, true);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAlbumService>()
                .Verify(v => v.SetAlbumMonitored(1, true));
        }

        [TestCase(ImportListMonitorType.SpecificAlbum)]
        [TestCase(ImportListMonitorType.EntireArtist)]
        public void should_not_monitor_existing_monitored_album(ImportListMonitorType monitorType)
        {
            WithAlbumId();
            WithArtistId();
            WithExistingAlbum(true);

            WithListSettings(monitorType, true);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAlbumService>()
                .Verify(v => v.SetAlbumMonitored(1, true), Times.Never);
        }

        [TestCase(ImportListMonitorType.SpecificAlbum, false)]
        [TestCase(ImportListMonitorType.EntireArtist, false)]
        [TestCase(ImportListMonitorType.None, false)]
        [TestCase(ImportListMonitorType.None, true)]

        public void should_not_monitor_existing_unmonitored_album(ImportListMonitorType monitorType, bool shouldMonitorExisting)
        {
            WithAlbumId();
            WithArtistId();
            WithExistingAlbum(false);

            WithListSettings(monitorType, shouldMonitorExisting);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAlbumService>()
                .Verify(v => v.SetAlbumMonitored(1, true), Times.Never);
        }

        [Test]
        public void should_search_specific_existing_unmonitored_album()
        {
            WithAlbumId();
            WithArtistId();
            WithExistingAlbum(false);

            WithListSettings(ImportListMonitorType.SpecificAlbum, true, true);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(v => v.Push<Command>(It.Is<AlbumSearchCommand>(x => x.AlbumIds.Count == 1 && x.AlbumIds.Contains(1)), CommandPriority.Normal, CommandTrigger.Unspecified));
        }

        [Test]
        public void should_not_search_specific_existing_unmonitored_album()
        {
            WithAlbumId();
            WithArtistId();
            WithExistingAlbum(false);

            WithListSettings(ImportListMonitorType.SpecificAlbum, true, false);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(v => v.Push<Command>(It.Is<AlbumSearchCommand>(x => x.AlbumIds.Count == 1 && x.AlbumIds.Contains(1)), CommandPriority.Normal, CommandTrigger.Unspecified), Times.Never);
        }

        [Test]
        public void should_search_all_artist_albums()
        {
            WithAlbumId();
            WithArtistId();
            WithExistingAlbum(false);

            WithListSettings(ImportListMonitorType.EntireArtist, true, true);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(v => v.Push<Command>(It.Is<MissingAlbumSearchCommand>(x => x.ArtistId == 1), CommandPriority.Normal, CommandTrigger.Unspecified));
        }

        [Test]
        public void should_not_search_all_artist_albums()
        {
            WithAlbumId();
            WithArtistId();
            WithExistingAlbum(false);

            WithListSettings(ImportListMonitorType.EntireArtist, true, false);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(v => v.Push<Command>(It.Is<MissingAlbumSearchCommand>(x => x.ArtistId == 1), CommandPriority.Normal, CommandTrigger.Unspecified), Times.Never);
        }

        [TestCase(ImportListMonitorType.SpecificAlbum)]
        [TestCase(ImportListMonitorType.EntireArtist)]
        [TestCase(ImportListMonitorType.None)]

        public void should_monitor_existing_unmonitored_artist(ImportListMonitorType monitorType)
        {
            WithArtistId();
            WithExistingArtist(false);

            WithListSettings(monitorType, true);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IArtistService>()
                .Verify(v => v.UpdateArtist(It.Is<Artist>(a => a.Monitored), true));
        }

        [TestCase(ImportListMonitorType.SpecificAlbum)]
        [TestCase(ImportListMonitorType.EntireArtist)]
        [TestCase(ImportListMonitorType.None)]

        public void should_not_monitor_existing_monitored_artist(ImportListMonitorType monitorType)
        {
            WithArtistId();
            WithExistingArtist(true);

            WithListSettings(monitorType, true);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IArtistService>()
                .Verify(v => v.UpdateArtist(It.IsAny<Artist>(), It.IsAny<bool>()), Times.Never);
        }

        [TestCase(ImportListMonitorType.SpecificAlbum)]
        [TestCase(ImportListMonitorType.EntireArtist)]
        [TestCase(ImportListMonitorType.None)]

        public void should_not_monitor_existing_unmonitored_artist(ImportListMonitorType monitorType)
        {
            WithArtistId();
            WithExistingArtist(false);

            WithListSettings(monitorType, false);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IArtistService>()
                .Verify(v => v.UpdateArtist(It.IsAny<Artist>(), It.IsAny<bool>()), Times.Never);
        }

        [Test]
        public void should_search_unmonitored_artist()
        {
            WithArtistId();
            WithExistingArtist(false);

            WithListSettings(ImportListMonitorType.EntireArtist, true, true);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(v => v.Push<Command>(It.Is<MissingAlbumSearchCommand>(x => x.ArtistId == 1), CommandPriority.Normal, CommandTrigger.Unspecified));
        }

        [Test]
        public void should_not_search_unmonitored_artist()
        {
            WithArtistId();
            WithExistingArtist(false);

            WithListSettings(ImportListMonitorType.EntireArtist, true, false);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(v => v.Push<Command>(It.IsAny<MissingAlbumSearchCommand>(), CommandPriority.Normal, CommandTrigger.Unspecified), Times.Never);
        }

        [Test]
        public void should_not_fetch_if_no_lists_are_enabled()
        {
            Mocker.GetMock<IImportListFactory>()
                .Setup(v => v.AutomaticAddEnabled(It.IsAny<bool>()))
                .Returns(new List<IImportList>());

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IFetchAndParseImportList>()
                .Verify(v => v.Fetch(), Times.Never);
        }

        [Test]
        public void should_not_process_if_no_items_are_returned()
        {
            Mocker.GetMock<IFetchAndParseImportList>()
                .Setup(v => v.Fetch())
                .Returns(new List<ImportListItemInfo>());

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IImportListExclusionService>()
                .Verify(v => v.All(), Times.Never);
        }

        private void WithSameAlbumSecondTrack(string trackTitle)
        {
            var importListItem2 = new ImportListItemInfo
            {
                Artist = "Linkin Park",
                ArtistMusicBrainzId = "f59c5520-5f46-4d2c-b2c4-822eabf53419",
                Album = "Meteora",
                AlbumMusicBrainzId = "09474d62-17dd-3a4f-98fb-04c65f38a479",
                TrackTitle = trackTitle
            };
            _importListReports.Add(importListItem2);
        }

        [Test]
        public void should_monitor_only_matching_track_for_existing_album()
        {
            WithAlbumId();
            WithArtistId();
            WithExistingAlbum(true);
            _importListReports.First().TrackTitle = "Track Two";

            WithListSettings(ImportListMonitorType.SpecificTrack, true, false);

            var matchedTrack = Builder<Track>.CreateNew().With(t => t.Id = 55).Build();

            Mocker.GetMock<ITrackService>()
                .Setup(v => v.SetMonitoredByTitle(1, It.Is<List<string>>(l => l.Single() == "Track Two")))
                .Returns(new List<Track> { matchedTrack });

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<ITrackService>()
                .Verify(v => v.SetMonitoredByTitle(1, It.Is<List<string>>(l => l.Single() == "Track Two")), Times.Once());
        }

        [Test]
        public void should_not_monitor_track_for_existing_album_when_should_monitor_existing_is_false()
        {
            WithAlbumId();
            WithArtistId();
            WithExistingAlbum(true);
            _importListReports.First().TrackTitle = "Track Two";

            WithListSettings(ImportListMonitorType.SpecificTrack, false, false);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<ITrackService>()
                .Verify(v => v.SetMonitoredByTitle(It.IsAny<int>(), It.IsAny<List<string>>()), Times.Never());
        }

        [Test]
        public void should_accumulate_track_titles_for_new_album_across_multiple_playlist_items()
        {
            WithArtistId();
            WithAlbumId();
            WithAlbum();
            _importListReports.First().TrackTitle = "Track One";
            WithSameAlbumSecondTrack("Track Two");

            WithListSettings(ImportListMonitorType.SpecificTrack, false, false);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAddAlbumService>()
                .Verify(v => v.AddAlbums(It.Is<List<Album>>(t => t.Count == 1 &&
                    t.First().AddOptions.MonitorTrackTitles.Count == 2 &&
                    t.First().AddOptions.MonitorTrackTitles.Contains("Track One") &&
                    t.First().AddOptions.MonitorTrackTitles.Contains("Track Two")),
                    false, true));
        }

        [Test]
        public void should_leave_album_monitored_true_for_song_mode_specific_album()
        {
            WithArtistId();
            WithAlbumId();
            WithAlbum();
            _importListReports.First().TrackTitle = "Track One";

            WithListSettings(ImportListMonitorType.SpecificTrack, false, false);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAddAlbumService>()
                .Verify(v => v.AddAlbums(It.Is<List<Album>>(t => t.Count == 1 && t.First().Monitored == true), false, true));
        }

        [Test]
        public void should_search_matched_track_for_existing_album_when_should_search()
        {
            WithAlbumId();
            WithArtistId();
            WithExistingAlbum(true);
            _importListReports.First().TrackTitle = "Track Two";

            WithListSettings(ImportListMonitorType.SpecificTrack, true, true);

            var matchedTrack = Builder<Track>.CreateNew().With(t => t.Id = 55).With(t => t.Title = "Track Two").Build();

            Mocker.GetMock<ITrackService>()
                .Setup(v => v.SetMonitoredByTitle(1, It.IsAny<List<string>>()))
                .Returns(new List<Track> { matchedTrack });

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(v => v.Push<Command>(It.Is<TrackSearchCommand>(x => x.TrackIds.Single() == 55), CommandPriority.Normal, CommandTrigger.Unspecified));
        }

        [Test]
        public void should_not_search_matched_track_for_existing_album_when_should_search_is_false()
        {
            WithAlbumId();
            WithArtistId();
            WithExistingAlbum(true);
            _importListReports.First().TrackTitle = "Track Two";

            WithListSettings(ImportListMonitorType.SpecificTrack, true, false);

            var matchedTrack = Builder<Track>.CreateNew().With(t => t.Id = 55).Build();

            Mocker.GetMock<ITrackService>()
                .Setup(v => v.SetMonitoredByTitle(1, It.IsAny<List<string>>()))
                .Returns(new List<Track> { matchedTrack });

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IManageCommandQueue>()
                .Verify(v => v.Push<Command>(It.IsAny<TrackSearchCommand>(), CommandPriority.Normal, CommandTrigger.Unspecified), Times.Never);
        }

        [Test]
        public void should_flag_new_song_mode_album_for_search_regardless_of_new_artist()
        {
            WithArtistId();
            WithAlbumId();
            WithAlbum();
            _importListReports.First().TrackTitle = "Track One";

            WithListSettings(ImportListMonitorType.SpecificTrack, false, true);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<IAddAlbumService>()
                .Verify(v => v.AddAlbums(It.Is<List<Album>>(t => t.Count == 1 && t.First().AddOptions.SearchForNewAlbum == true), false, true));
        }

        [Test]
        public void should_monitor_by_track_title_only_under_specific_track()
        {
            WithAlbumId();
            WithArtistId();
            WithExistingAlbum(true);
            _importListReports.First().TrackTitle = "Track Two";

            WithListSettings(ImportListMonitorType.SpecificTrack, true, false);

            Mocker.GetMock<ITrackService>()
                .Setup(v => v.SetMonitoredByTitle(1, It.IsAny<List<string>>()))
                .Returns(new List<Track>());

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<ITrackService>()
                .Verify(v => v.SetMonitoredByTitle(1, It.IsAny<List<string>>()), Times.Once());
        }

        [Test]
        public void should_not_monitor_by_track_title_under_specific_album()
        {
            WithAlbumId();
            WithArtistId();
            WithExistingAlbum(true);
            _importListReports.First().TrackTitle = "Track Two";

            WithListSettings(ImportListMonitorType.SpecificAlbum, true, false);

            Subject.Execute(new ImportListSyncCommand());

            Mocker.GetMock<ITrackService>()
                .Verify(v => v.SetMonitoredByTitle(It.IsAny<int>(), It.IsAny<List<string>>()), Times.Never());
        }

        [Test]
        public void should_search_only_tracks_from_lists_with_should_search_when_multiple_lists_touch_same_album()
        {
            WithAlbumId();
            WithArtistId();
            WithExistingAlbum(true);

            // List A: ShouldSearch=true with "Song From List A"
            var listA = new ImportListDefinition { Id = 1, ShouldMonitor = ImportListMonitorType.SpecificTrack, ShouldMonitorExisting = true, ShouldSearch = true };
            var item1 = _importListReports.First();
            item1.ImportListId = 1;
            item1.TrackTitle = "Song From List A";

            // List B: ShouldSearch=false with "Song From List B"
            var item2 = new ImportListItemInfo
            {
                Artist = "Test Artist",
                ArtistMusicBrainzId = _importListReports.First().ArtistMusicBrainzId,
                Album = _importListReports.First().Album,
                AlbumMusicBrainzId = _importListReports.First().AlbumMusicBrainzId,
                ImportListId = 2,
                TrackTitle = "Song From List B"
            };
            _importListReports.Add(item2);

            var listB = new ImportListDefinition { Id = 2, ShouldMonitor = ImportListMonitorType.SpecificTrack, ShouldMonitorExisting = true, ShouldSearch = false };

            Mocker.GetMock<IImportListFactory>()
                .Setup(v => v.All())
                .Returns(new List<ImportListDefinition> { listA, listB });

            var trackA = Builder<Track>.CreateNew().With(t => t.Id = 100).With(t => t.Title = "Song From List A").Build();
            var trackB = Builder<Track>.CreateNew().With(t => t.Id = 101).With(t => t.Title = "Song From List B").Build();

            Mocker.GetMock<ITrackService>()
                .Setup(v => v.SetMonitoredByTitle(1, It.IsAny<List<string>>()))
                .Returns(new List<Track> { trackA, trackB });

            Subject.Execute(new ImportListSyncCommand());

            // Only the track from List A (with ShouldSearch=true) should be in the search command
            Mocker.GetMock<IManageCommandQueue>()
                .Verify(v => v.Push<Command>(It.Is<TrackSearchCommand>(x => x.TrackIds.Count == 1 && x.TrackIds.Contains(100) && !x.TrackIds.Contains(101)), CommandPriority.Normal, CommandTrigger.Unspecified));
        }
    }
}
