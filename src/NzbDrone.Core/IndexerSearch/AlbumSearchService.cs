using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Music;
using NzbDrone.Core.Queue;

namespace NzbDrone.Core.IndexerSearch
{
    public class AlbumSearchService : IExecute<AlbumSearchCommand>,
                               IExecute<TrackSearchCommand>,
                               IExecute<MissingAlbumSearchCommand>,
                               IExecute<CutoffUnmetAlbumSearchCommand>
    {
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IAlbumService _albumService;
        private readonly ITrackService _trackService;
        private readonly IAlbumCutoffService _albumCutoffService;
        private readonly IQueueService _queueService;
        private readonly IProcessDownloadDecisions _processDownloadDecisions;
        private readonly Logger _logger;

        public AlbumSearchService(ISearchForReleases nzbSearchService,
            IAlbumService albumService,
            ITrackService trackService,
            IAlbumCutoffService albumCutoffService,
            IQueueService queueService,
            IProcessDownloadDecisions processDownloadDecisions,
            Logger logger)
        {
            _releaseSearchService = nzbSearchService;
            _albumService = albumService;
            _trackService = trackService;
            _albumCutoffService = albumCutoffService;
            _queueService = queueService;
            _processDownloadDecisions = processDownloadDecisions;
            _logger = logger;
        }

        private async Task SearchForBulkAlbums(List<Album> albums, bool userInvokedSearch)
        {
            _logger.ProgressInfo("Performing missing search for {0} albums", albums.Count);
            var downloadedCount = 0;

            foreach (var album in albums.OrderBy(a => a.LastSearchTime ?? DateTime.MinValue))
            {
                List<DownloadDecision> decisions;

                try
                {
                    decisions = await SearchAlbumOrItsMonitoredTracks(album, userInvokedSearch, false);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unable to search for album: [{0}]", album);
                    continue;
                }

                var processed = await _processDownloadDecisions.ProcessDecisions(decisions);

                downloadedCount += processed.Grabbed.Count;
            }

            _logger.ProgressInfo("Completed search for {0} albums. {1} reports downloaded.", albums.Count, downloadedCount);
        }

        private bool IsPartiallyMonitored(Album album)
        {
            return _trackService.GetTracksByAlbum(album.Id).Any(t => !t.Monitored);
        }

        // An album whose tracks were picked individually must be searched track by track,
        // whatever brought it here: searching it whole grabs twelve tracks for the one that
        // is actually wanted.
        private async Task<List<DownloadDecision>> SearchAlbumOrItsMonitoredTracks(Album album, bool userInvokedSearch, bool missingOnly)
        {
            var tracks = _trackService.GetTracksByAlbum(album.Id);

            if (tracks.Any(t => !t.Monitored))
            {
                var decisions = new List<DownloadDecision>();

                foreach (var track in tracks.Where(t => t.Monitored && (!missingOnly || !t.HasFile)))
                {
                    decisions.AddRange(await _releaseSearchService.TrackSearch(track.Id, userInvokedSearch, false));
                }

                return decisions;
            }

            return await _releaseSearchService.AlbumSearch(album.Id, false, userInvokedSearch, false);
        }

        private async Task SearchForMissingAlbums(List<Album> albums, bool userInvokedSearch)
        {
            _logger.ProgressInfo("Performing missing search for {0} albums", albums.Count);
            var downloadedCount = 0;

            foreach (var album in albums.OrderBy(a => a.LastSearchTime ?? DateTime.MinValue))
            {
                List<DownloadDecision> decisions;

                try
                {
                    decisions = await SearchAlbumOrItsMonitoredTracks(album, userInvokedSearch, true);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unable to search for album: [{0}]", album);
                    continue;
                }

                var processed = await _processDownloadDecisions.ProcessDecisions(decisions);

                downloadedCount += processed.Grabbed.Count;
            }

            _logger.ProgressInfo("Completed search for {0} albums. {1} reports downloaded.", albums.Count, downloadedCount);
        }

        public void Execute(AlbumSearchCommand message)
        {
            foreach (var albumId in message.AlbumIds)
            {
                var album = _albumService.GetAlbum(albumId);

                // Through the shared path, so an album whose tracks were picked
                // individually is searched track by track: a failed single-track grab used
                // to come back here as a whole-album search and import twelve files.
                var decisions = SearchAlbumOrItsMonitoredTracks(album, message.Trigger == CommandTrigger.Manual, false).GetAwaiter().GetResult();
                var processed = _processDownloadDecisions.ProcessDecisions(decisions).GetAwaiter().GetResult();

                _logger.ProgressInfo("Album search completed. {0} reports downloaded.", processed.Grabbed.Count);
            }
        }

        public void Execute(TrackSearchCommand message)
        {
            var downloadedCount = 0;

            foreach (var trackId in message.TrackIds)
            {
                var decisions = _releaseSearchService.TrackSearch(trackId, message.Trigger == CommandTrigger.Manual, false).GetAwaiter().GetResult();
                var processed = _processDownloadDecisions.ProcessDecisions(decisions).GetAwaiter().GetResult();

                downloadedCount += processed.Grabbed.Count;
            }

            _logger.ProgressInfo("Track search completed. {0} reports downloaded.", downloadedCount);
        }

        public void Execute(MissingAlbumSearchCommand message)
        {
            List<Album> albums;

            if (message.ArtistId.HasValue)
            {
                var artistId = message.ArtistId.Value;

                var pagingSpec = new PagingSpec<Album>
                {
                    Page = 1,
                    PageSize = 100000,
                    SortDirection = SortDirection.Ascending,
                    SortKey = "Id"
                };

                pagingSpec.FilterExpressions.Add(v => v.Monitored == true && v.Artist.Value.Monitored == true);

                albums = _albumService.AlbumsWithoutFiles(pagingSpec).Records.Where(e => e.ArtistId.Equals(artistId)).ToList();
            }
            else
            {
                var pagingSpec = new PagingSpec<Album>
                {
                    Page = 1,
                    PageSize = 100000,
                    SortDirection = SortDirection.Ascending,
                    SortKey = "Id"
                };

                pagingSpec.FilterExpressions.Add(v => v.Monitored == true && v.Artist.Value.Monitored == true);

                albums = _albumService.AlbumsWithoutFiles(pagingSpec).Records.ToList();
            }

            var queue = _queueService.GetQueue().Where(q => q.Album != null).Select(q => q.Album.Id).ToList();

            // Skipping a whole album because one of its tracks is downloading would leave
            // that album's other monitored tracks unsearched for the round. The queue
            // specification already rejects the track that really is in flight, so a
            // partially monitored album keeps its turn. Only queued albums pay the lookup.
            var missing = albums.Where(e => !queue.Contains(e.Id) || IsPartiallyMonitored(e)).ToList();

            SearchForMissingAlbums(missing, message.Trigger == CommandTrigger.Manual).GetAwaiter().GetResult();
        }

        public void Execute(CutoffUnmetAlbumSearchCommand message)
        {
            var pagingSpec = new PagingSpec<Album>
            {
                Page = 1,
                PageSize = 100000,
                SortDirection = SortDirection.Ascending,
                SortKey = "Id"
            };

            pagingSpec.FilterExpressions.Add(v => v.Monitored == true && v.Artist.Value.Monitored == true);

            var albums = _albumCutoffService.AlbumsWhereCutoffUnmet(pagingSpec).Records.ToList();
            var queue = _queueService.GetQueue().Where(q => q.Album != null).Select(q => q.Album.Id);
            var cutoffUnmet = albums.Where(e => !queue.Contains(e.Id)).ToList();

            SearchForBulkAlbums(cutoffUnmet, message.Trigger == CommandTrigger.Manual).GetAwaiter().GetResult();
        }
    }
}
