using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Music
{
    public interface ITrackService
    {
        Track GetTrack(int id);
        List<Track> GetTracks(IEnumerable<int> ids);
        List<Track> GetTracksByArtist(int artistId);
        List<Track> GetTracksByAlbum(int albumId);
        List<Track> GetTracksByRelease(int albumReleaseId);
        List<Track> GetTracksByReleases(List<int> albumReleaseIds);
        List<Track> GetTracksForRefresh(int albumReleaseId, List<string> foreignTrackIds);
        List<Track> TracksWithFiles(int artistId);
        List<Track> TracksWithoutFiles(int albumId);
        PagingSpec<Track> TracksWithoutFilesPaged(PagingSpec<Track> pagingSpec, bool monitored);
        List<Track> GetTracksByFileId(int trackFileId);
        List<Track> GetTracksByFileId(IEnumerable<int> trackFileIds);
        void UpdateTrack(Track track);
        void InsertMany(List<Track> tracks);
        void UpdateMany(List<Track> tracks);
        void DeleteMany(List<Track> tracks);
        void SetFileIds(List<Track> tracks);
        void SetMonitored(IEnumerable<int> ids, bool monitored);
        List<Track> RelinkTrackFilesToRelease(AlbumRelease newRelease, List<AlbumRelease> oldReleases);
        List<Track> SearchTracksByTitle(string title, int limit);
        List<Track> SetMonitoredByTitle(int albumId, IEnumerable<string> titles);
    }

    public class TrackService : ITrackService,
                                IHandle<ReleaseDeletedEvent>,
                                IHandle<TrackFileDeletedEvent>
    {
        private readonly ITrackRepository _trackRepository;
        private readonly Logger _logger;

        public TrackService(ITrackRepository trackRepository,
                            Logger logger)
        {
            _trackRepository = trackRepository;
            _logger = logger;
        }

        public Track GetTrack(int id)
        {
            return _trackRepository.Get(id);
        }

        public List<Track> GetTracks(IEnumerable<int> ids)
        {
            return _trackRepository.Get(ids).ToList();
        }

        public List<Track> GetTracksByArtist(int artistId)
        {
            _logger.Debug("Getting Tracks for ArtistId {0}", artistId);
            return _trackRepository.GetTracks(artistId).ToList();
        }

        public List<Track> GetTracksByAlbum(int albumId)
        {
            return _trackRepository.GetTracksByAlbum(albumId);
        }

        public List<Track> GetTracksByRelease(int albumReleaseId)
        {
            return _trackRepository.GetTracksByRelease(albumReleaseId);
        }

        public List<Track> SearchTracksByTitle(string title, int limit)
        {
            return _trackRepository.SearchTracksByTitle(title, limit);
        }

        public List<Track> GetTracksByReleases(List<int> albumReleaseIds)
        {
            return _trackRepository.GetTracksByReleases(albumReleaseIds);
        }

        public List<Track> GetTracksForRefresh(int albumReleaseId, List<string> foreignTrackIds)
        {
            return _trackRepository.GetTracksForRefresh(albumReleaseId, foreignTrackIds);
        }

        public List<Track> TracksWithFiles(int artistId)
        {
            return _trackRepository.TracksWithFiles(artistId);
        }

        public List<Track> TracksWithoutFiles(int albumId)
        {
            return _trackRepository.TracksWithoutFiles(albumId);
        }

        public PagingSpec<Track> TracksWithoutFilesPaged(PagingSpec<Track> pagingSpec, bool monitored)
        {
            return _trackRepository.TracksWithoutFilesPaged(pagingSpec, monitored);
        }

        public List<Track> GetTracksByFileId(int trackFileId)
        {
            return _trackRepository.GetTracksByFileId(trackFileId);
        }

        public List<Track> GetTracksByFileId(IEnumerable<int> trackFileIds)
        {
            return _trackRepository.GetTracksByFileId(trackFileIds);
        }

        public void UpdateTrack(Track track)
        {
            _trackRepository.Update(track);
        }

        public void InsertMany(List<Track> tracks)
        {
            _trackRepository.InsertMany(tracks);
        }

        public void UpdateMany(List<Track> tracks)
        {
            _trackRepository.UpdateMany(tracks);
        }

        public void DeleteMany(List<Track> tracks)
        {
            _trackRepository.DeleteMany(tracks);
        }

        public void SetFileIds(List<Track> tracks)
        {
            _trackRepository.SetFileId(tracks);
        }

        public void SetMonitored(IEnumerable<int> ids, bool monitored)
        {
            _trackRepository.SetMonitored(ids, monitored);
        }

        public List<Track> RelinkTrackFilesToRelease(AlbumRelease newRelease, List<AlbumRelease> oldReleases)
        {
            var newTracks = _trackRepository.GetTracksByRelease(newRelease.Id);
            var oldTracks = oldReleases.SelectMany(r => _trackRepository.GetTracksByRelease(r.Id))
                .Where(t => t.HasFile || t.Monitored)
                .ToList();

            var byRecording = oldTracks
                .Where(t => t.ForeignRecordingId.IsNotNullOrWhiteSpace())
                .ToLookup(t => t.ForeignRecordingId);

            var changed = new List<Track>();
            var consumed = new HashSet<int>();

            foreach (var newTrack in newTracks)
            {
                var match = byRecording[newTrack.ForeignRecordingId].FirstOrDefault(t => !consumed.Contains(t.Id));

                // Fallback when recording ids don't line up (e.g. different medium layout):
                // a normalized-title match is safer than absolute track number here, since
                // absolute track number does not survive a medium-count change (e.g. a
                // 2x-vinyl 20-track release vs. a single-disc 13-track digital release).
                // Each old track may only be consumed once so duplicate titles (e.g. two
                // "Interlude" tracks) pair up with distinct files instead of both new
                // tracks grabbing the same old track's file.
                match ??= oldTracks.FirstOrDefault(t =>
                    !consumed.Contains(t.Id) &&
                    t.Title.CleanTrackTitle().Equals(newTrack.Title.CleanTrackTitle(), StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    continue;
                }

                consumed.Add(match.Id);
                newTrack.TrackFileId = match.TrackFileId;
                newTrack.Monitored = match.Monitored;
                changed.Add(newTrack);
            }

            if (changed.Any())
            {
                _trackRepository.UpdateMany(changed);
            }

            return changed;
        }

        public List<Track> SetMonitoredByTitle(int albumId, IEnumerable<string> titles)
        {
            var normalizedTitles = new HashSet<string>(titles.Select(NormalizeTrackTitleForMatch));
            var tracks = _trackRepository.GetTracksByAlbum(albumId);
            var matched = new List<Track>();

            foreach (var track in tracks)
            {
                var shouldMonitor = normalizedTitles.Contains(NormalizeTrackTitleForMatch(track.Title));
                track.Monitored = shouldMonitor;

                if (shouldMonitor)
                {
                    matched.Add(track);
                }
            }

            _trackRepository.UpdateMany(tracks);

            return matched;
        }

        // NormalizeTitle collapses delimiters (., -, _, etc.) to a single space rather than
        // removing them, so "B.Y.O.B." normalizes to "b y o b" while "byob" normalizes to
        // "byob" - not equal. Stripping the remaining spaces makes title matching fully
        // punctuation-insensitive, which is what song-mode needs when comparing an
        // import list's track title against the locally stored (MusicBrainz-sourced) one.
        public static string NormalizeTrackTitleForMatch(string title)
        {
            return NzbDrone.Core.Parser.Parser.NormalizeTitle(title).Replace(" ", string.Empty);
        }

        // Song-mode search gating filter: both import paths (ImportListSyncService's
        // immediate handling of existing albums and AlbumAddedService's deferred handling
        // of newly-added albums) must funnel through this so a track is only ever searched
        // when its own title came from a search-enabled list, not merely because some other
        // list touching the same album happened to also monitor it.
        public static List<int> GetTrackIdsMatchingTitles(List<Track> tracks, List<string> titles)
        {
            var normalizedTitles = new HashSet<string>(titles.Select(NormalizeTrackTitleForMatch));

            return tracks.Where(t => normalizedTitles.Contains(NormalizeTrackTitleForMatch(t.Title))).Select(t => t.Id).ToList();
        }

        public void Handle(ReleaseDeletedEvent message)
        {
            var tracks = GetTracksByRelease(message.Release.Id);
            _trackRepository.DeleteMany(tracks);
        }

        public void Handle(TrackFileDeletedEvent message)
        {
            _logger.Debug($"Detaching tracks from file {message.TrackFile}");
            _trackRepository.DetachTrackFile(message.TrackFile.Id);
        }
    }
}
