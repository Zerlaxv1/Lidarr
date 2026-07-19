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

            foreach (var newTrack in newTracks)
            {
                var match = byRecording[newTrack.ForeignRecordingId].FirstOrDefault();

                // Fallback when recording ids don't line up (e.g. different medium layout):
                // a normalized-title match is safer than absolute track number here, since
                // absolute track number does not survive a medium-count change (e.g. a
                // 2x-vinyl 20-track release vs. a single-disc 13-track digital release).
                match ??= oldTracks.FirstOrDefault(t =>
                    t.Title.CleanTrackTitle().Equals(newTrack.Title.CleanTrackTitle(), StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    continue;
                }

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
