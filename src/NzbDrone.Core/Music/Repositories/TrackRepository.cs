using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Music
{
    public interface ITrackRepository : IBasicRepository<Track>
    {
        List<Track> GetTracks(int artistId);
        List<Track> GetTracksByAlbum(int albumId);
        List<Track> GetTracksByRelease(int albumReleaseId);
        List<Track> GetTracksByReleases(List<int> albumReleaseIds);
        List<Track> GetTracksForRefresh(int albumReleaseId, List<string> foreignTrackIds);
        List<Track> GetTracksByFileId(int fileId);
        List<Track> GetTracksByFileId(IEnumerable<int> ids);
        List<Track> TracksWithFiles(int artistId);
        List<Track> TracksWithoutFiles(int albumId);
        PagingSpec<Track> TracksWithoutFilesPaged(PagingSpec<Track> pagingSpec, bool monitored);
        void SetFileId(List<Track> tracks);
        void DetachTrackFile(int trackFileId);
        void SetMonitored(IEnumerable<int> ids, bool monitored);
    }

    public class TrackRepository : BasicRepository<Track>, ITrackRepository
    {
        public TrackRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public void SetMonitored(IEnumerable<int> ids, bool monitored)
        {
            var tracks = ids.Select(x => new Track { Id = x, Monitored = monitored }).ToList();
            SetFields(tracks, t => t.Monitored);
        }

        public List<Track> GetTracks(int artistId)
        {
            return Query(Builder()
                         .Join<Track, AlbumRelease>((t, r) => t.AlbumReleaseId == r.Id)
                         .Join<AlbumRelease, Album>((r, a) => r.AlbumId == a.Id)
                         .Join<Album, Artist>((album, artist) => album.ArtistMetadataId == artist.ArtistMetadataId)
                         .Where<AlbumRelease>(r => r.Monitored == true)
                         .Where<Artist>(x => x.Id == artistId));
        }

        public List<Track> GetTracksByAlbum(int albumId)
        {
            return Query(Builder()
                         .Join<Track, AlbumRelease>((t, r) => t.AlbumReleaseId == r.Id)
                         .Join<AlbumRelease, Album>((r, a) => r.AlbumId == a.Id)
                         .Where<AlbumRelease>(r => r.Monitored == true)
                         .Where<Album>(x => x.Id == albumId));
        }

        public List<Track> GetTracksByRelease(int albumReleaseId)
        {
            return Query(t => t.AlbumReleaseId == albumReleaseId).ToList();
        }

        public List<Track> GetTracksByReleases(List<int> albumReleaseIds)
        {
            // this will populate the artist metadata also
            return _database.QueryJoined<Track, ArtistMetadata>(Builder()
                               .Join<Track, ArtistMetadata>((l, r) => l.ArtistMetadataId == r.Id)
                               .Where<Track>(x => albumReleaseIds.Contains(x.AlbumReleaseId)), (track, metadata) =>
                    {
                        track.ArtistMetadata = metadata;
                        return track;
                    }).ToList();
        }

        public List<Track> GetTracksForRefresh(int albumReleaseId, List<string> foreignTrackIds)
        {
            return Query(a => a.AlbumReleaseId == albumReleaseId || foreignTrackIds.Contains(a.ForeignTrackId));
        }

        public List<Track> GetTracksByFileId(int fileId)
        {
            return Query(e => e.TrackFileId == fileId);
        }

        public List<Track> GetTracksByFileId(IEnumerable<int> ids)
        {
            return Query(x => ids.Contains(x.TrackFileId));
        }

        public List<Track> TracksWithFiles(int artistId)
        {
            return Query(Builder()
                         .Join<Track, AlbumRelease>((t, r) => t.AlbumReleaseId == r.Id)
                         .Join<AlbumRelease, Album>((r, a) => r.AlbumId == a.Id)
                         .Join<Album, Artist>((album, artist) => album.ArtistMetadataId == artist.ArtistMetadataId)
                         .Join<Track, TrackFile>((t, f) => t.TrackFileId == f.Id)
                         .Where<AlbumRelease>(r => r.Monitored == true)
                         .Where<Artist>(x => x.Id == artistId));
        }

        public List<Track> TracksWithoutFiles(int albumId)
        {
            // x.Id == null is converted to SQL, so warning incorrect
#pragma warning disable CS0472
            return Query(Builder()
                         .Join<Track, AlbumRelease>((t, r) => t.AlbumReleaseId == r.Id)
                         .LeftJoin<Track, TrackFile>((t, f) => t.TrackFileId == f.Id)
                         .Where<AlbumRelease>(r => r.Monitored == true && r.AlbumId == albumId)
                         .Where<TrackFile>(x => x.Id == null));
#pragma warning restore CS0472
        }

        // x.Id == null is converted to SQL, so warning incorrect
#pragma warning disable CS0472
        private SqlBuilder TracksWithoutFilesPagedBuilder(DateTime currentTime, bool monitored)
        {
            var builder = Builder()
                .Join<Track, AlbumRelease>((t, r) => t.AlbumReleaseId == r.Id)
                .Join<AlbumRelease, Album>((r, a) => r.AlbumId == a.Id)
                .Join<Album, Artist>((album, artist) => album.ArtistMetadataId == artist.ArtistMetadataId)
                .LeftJoin<Track, TrackFile>((t, f) => t.TrackFileId == f.Id)
                .Where<TrackFile>(f => f.Id == null)
                .Where<AlbumRelease>(r => r.Monitored == true)
                .Where<Album>(a => a.ReleaseDate <= currentTime);

            if (monitored)
            {
                builder = builder.Where<Track>(t => t.Monitored == true)
                    .Where<Album>(a => a.Monitored == true)
                    .Where<Artist>(a => a.Monitored == true);
            }
            else
            {
                var falseIndicator = _database.DatabaseType == DatabaseType.PostgreSQL ? "false" : "0";
                builder = builder.Where($"(\"Tracks\".\"Monitored\" = {falseIndicator} OR \"Albums\".\"Monitored\" = {falseIndicator} OR \"Artists\".\"Monitored\" = {falseIndicator})");
            }

            return builder;
        }
#pragma warning restore CS0472

        public PagingSpec<Track> TracksWithoutFilesPaged(PagingSpec<Track> pagingSpec, bool monitored)
        {
            var currentTime = DateTime.UtcNow;

            pagingSpec.Records = GetPagedRecords(TracksWithoutFilesPagedBuilder(currentTime, monitored), pagingSpec, PagedJoinedQuery);
            pagingSpec.TotalRecords = GetPagedRecordCount(TracksWithoutFilesPagedBuilder(currentTime, monitored).SelectCount(), pagingSpec);

            return pagingSpec;
        }

        private IEnumerable<Track> PagedJoinedQuery(SqlBuilder builder) =>
            _database.QueryJoined<Track, AlbumRelease, Album, Artist>(builder, (track, release, album, artist) =>
            {
                track.AlbumRelease = release;
                track.Album = album;
                track.Artist = artist;
                return track;
            });

        public void SetFileId(List<Track> tracks)
        {
            SetFields(tracks, t => t.TrackFileId);
        }

        public void DetachTrackFile(int trackFileId)
        {
            var tracks = GetTracksByFileId(trackFileId);
            tracks.ForEach(x => x.TrackFileId = 0);
            SetFileId(tracks);
        }
    }
}
