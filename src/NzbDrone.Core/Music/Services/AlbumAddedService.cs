using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Music.Events;

namespace NzbDrone.Core.Music
{
    public interface IAlbumAddedService
    {
        void SearchForRecentlyAdded(int artistId);
    }

    public class AlbumAddedService : IHandle<AlbumInfoRefreshedEvent>, IAlbumAddedService
    {
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly IAlbumService _albumService;
        private readonly ITrackService _trackService;
        private readonly Logger _logger;
        private readonly ICached<List<int>> _addedAlbumsCache;

        public AlbumAddedService(ICacheManager cacheManager,
                                   IManageCommandQueue commandQueueManager,
                                   IAlbumService albumService,
                                   ITrackService trackService,
                                   Logger logger)
        {
            _commandQueueManager = commandQueueManager;
            _albumService = albumService;
            _trackService = trackService;
            _logger = logger;
            _addedAlbumsCache = cacheManager.GetCache<List<int>>(GetType());
        }

        public void SearchForRecentlyAdded(int artistId)
        {
            var allAlbums = _albumService.GetAlbumsByArtist(artistId);
            var songModeAlbums = allAlbums.Where(x => x.AddOptions.MonitorTrackTitles.Any()).ToList();
            var toSearch = allAlbums.Except(songModeAlbums).Where(x => x.AddOptions.SearchForNewAlbum).ToList();

            if (toSearch.Any())
            {
                _logger.Trace("Found {0} albums flagged for search on add", toSearch.Count);
                toSearch.ForEach(x => x.AddOptions.SearchForNewAlbum = false);

                _albumService.SetAddOptions(toSearch);
            }

            var recentlyAddedIds = _addedAlbumsCache.Find(artistId.ToString());
            if (recentlyAddedIds != null)
            {
                _logger.Trace("Found and monitored {0} albums by artist [{1}] during metadata refresh.", recentlyAddedIds.Count, artistId);
                toSearch.AddRange(allAlbums.Except(songModeAlbums).Where(x => recentlyAddedIds.Contains(x.Id)));
            }

            if (toSearch.Any())
            {
                _logger.Debug("Searching for {0} monitored albums for artist [{1}] added during metadata refresh.", toSearch.Count, artistId);
                _commandQueueManager.Push(new AlbumSearchCommand(toSearch.Select(e => e.Id).ToList()));
            }

            _addedAlbumsCache.Remove(artistId.ToString());

            ApplyPendingTrackMonitoring(songModeAlbums);
        }

        private void ApplyPendingTrackMonitoring(List<Album> albums)
        {
            if (albums.Empty())
            {
                return;
            }

            var tracksToSearch = new List<int>();

            foreach (var album in albums)
            {
                var titles = album.AddOptions.MonitorTrackTitles;
                var searchTitles = album.AddOptions.SearchTrackTitles;
                _logger.Debug("Applying song-mode track monitoring for album [{0}], {1} title(s)", album.Id, titles.Count);

                // Snapshot into a new list: titles is the live AddOptions.MonitorTrackTitles
                // reference, which gets cleared below in this same iteration. Passing it
                // directly would hand the callee (and any test double recording the call)
                // a reference that no longer reflects what was actually requested.
                var matched = _trackService.SetMonitoredByTitle(album.Id, titles.ToList());

                if (matched.Empty())
                {
                    _logger.Warn("Song-mode track monitoring for album [{0}] matched no tracks for titles: {1}", album.Id, string.Join(", ", titles));
                }
                else if (searchTitles.Any())
                {
                    tracksToSearch.AddRange(TrackService.GetTrackIdsMatchingTitles(matched, searchTitles));
                }

                album.AddOptions.SearchForNewAlbum = false;
                titles.Clear();
                searchTitles.Clear();
            }

            _albumService.SetAddOptions(albums);

            if (tracksToSearch.Any())
            {
                _commandQueueManager.Push(new TrackSearchCommand(tracksToSearch));
            }
        }

        public void Handle(AlbumInfoRefreshedEvent message)
        {
            if (message.Artist.AddOptions == null)
            {
                if (!message.Artist.Monitored)
                {
                    _logger.Debug("Artist is not monitored");
                    return;
                }

                if (message.Added.Empty())
                {
                    _logger.Debug("No new albums, skipping search");
                    return;
                }

                if (message.Added.None(a => a.ReleaseDate.HasValue))
                {
                    _logger.Debug("No new albums have an release date");
                    return;
                }

                var previouslyReleased = message.Added.Where(a => a.ReleaseDate.HasValue && a.ReleaseDate.Value.Before(DateTime.UtcNow.AddDays(1)) && a.Monitored).ToList();

                if (previouslyReleased.Empty())
                {
                    _logger.Debug("Newly added albums all release in the future");
                    return;
                }

                _addedAlbumsCache.Set(message.Artist.Id.ToString(), previouslyReleased.Select(e => e.Id).ToList());
            }
        }
    }
}
