using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

        // unmonitorOthers mirrors the given titles exactly, which is what a freshly added
        // album wants; an album already in the library must not have a list wipe the
        // monitoring the user (or another list) put there.
        List<Track> SetMonitoredByTitle(int albumId, IEnumerable<string> titles, bool unmonitorOthers);
    }

    public class TrackService : ITrackService,
                                IHandle<ReleaseDeletedEvent>,
                                IHandle<TrackFileDeletedEvent>
    {
        private readonly ITrackRepository _trackRepository;
        private readonly IProvideRecordingAliases _recordingAliasService;
        private readonly Logger _logger;

        public TrackService(ITrackRepository trackRepository,
                            IProvideRecordingAliases recordingAliasService,
                            Logger logger)
        {
            _trackRepository = trackRepository;
            _recordingAliasService = recordingAliasService;
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
                    // Mirror the old monitored release's profile: a new-release track with
                    // no monitored/filed counterpart must not stay monitored. Without this,
                    // a freshly materialized release (PrepareNewChild defaults every track to
                    // album.Monitored) leaves the whole release monitored after a switch.
                    if (newTrack.Monitored)
                    {
                        newTrack.Monitored = false;
                        changed.Add(newTrack);
                    }

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

        public List<Track> SetMonitoredByTitle(int albumId, IEnumerable<string> titles, bool unmonitorOthers)
        {
            var titleList = titles.ToList();
            var tracks = _trackRepository.GetTracksByAlbum(albumId);
            var matched = MatchTracksByTitle(tracks, titleList);

            matched.AddRange(MatchByRecordingAlias(tracks, titleList, matched));

            foreach (var track in tracks)
            {
                if (matched.Contains(track))
                {
                    track.Monitored = true;
                }
                else if (unmonitorOthers)
                {
                    track.Monitored = false;
                }
            }

            _trackRepository.UpdateMany(tracks);

            return matched;
        }

        // Last resort for a list that writes titles in another script than MusicBrainz
        // stores them ("Akuma no Ko" against 悪魔の子): ask MusicBrainz for the aliases of
        // this release's recordings and match on those. Costs one request per album and
        // only runs when a title matched no track by any spelling.
        private List<Track> MatchByRecordingAlias(List<Track> tracks, List<string> titles, List<Track> alreadyMatched)
        {
            var unmatched = titles.Where(t => !alreadyMatched.Any(m => NormalizeTrackTitleForMatch(m.Title) == NormalizeTrackTitleForMatch(t))).ToList();

            var candidates = tracks.Where(t => !alreadyMatched.Contains(t) && t.ForeignRecordingId.IsNotNullOrWhiteSpace()).ToList();

            if (unmatched.Empty() || candidates.Empty())
            {
                return new List<Track>();
            }

            var wanted = new HashSet<string>(unmatched.Select(NormalizeTrackTitleForMatch));
            var aliases = _recordingAliasService.GetAliases(candidates.First().AlbumReleaseId);

            if (aliases.Empty())
            {
                return new List<Track>();
            }

            return candidates
                .Where(t => aliases.TryGetValue(t.ForeignRecordingId, out var names)
                            && names.Any(n => wanted.Contains(NormalizeTrackTitleForMatch(n))))
                .ToList();
        }

        // Streaming services decorate track titles with editorial suffixes MusicBrainz does
        // not carry ("- Remastered 2011", "- Live", "(feat. X)"). Match exactly first, then
        // give only the still-unmatched titles a second pass with those suffixes stripped,
        // so a decorated title can never steal a track that some title matched exactly.
        public static List<Track> MatchTracksByTitle(List<Track> tracks, IEnumerable<string> titles)
        {
            var wanted = titles.ToList();
            var exact = new HashSet<string>(wanted.Select(NormalizeTrackTitleForMatch));

            var matched = tracks.Where(t => exact.Contains(NormalizeTrackTitleForMatch(t.Title))).ToList();

            var unmatched = wanted.Where(w => !matched.Any(m => NormalizeTrackTitleForMatch(m.Title) == NormalizeTrackTitleForMatch(w))).ToList();

            if (unmatched.Any())
            {
                var relaxed = new HashSet<string>(unmatched.SelectMany(TitleMatchVariants));

                matched.AddRange(tracks.Where(t => !matched.Contains(t) && TitleMatchVariants(t.Title).Any(relaxed.Contains)));
            }

            return matched;
        }

        // The same track is spelled differently by each source: "Pt. 2" against "Part 2",
        // a Japanese pressing appending "= <katakana>", a track index like "V. 3005", a
        // "-Anime ver.-" suffix, or a dual-script title where only one side is stored.
        // Reduce a title to the forms it could legitimately be written as.
        private static IEnumerable<string> TitleMatchVariants(string title)
        {
            if (title.IsNullOrWhiteSpace())
            {
                yield break;
            }

            var cleaned = StripEditorialSuffix(title);
            cleaned = DashedVersionRegex.Replace(cleaned, string.Empty);
            cleaned = AlternateTitleRegex.Replace(cleaned, string.Empty);
            cleaned = LeadingIndexRegex.Replace(cleaned, string.Empty);
            cleaned = PartAbbreviationRegex.Replace(cleaned, "Part");

            var normalized = NormalizeTrackTitleForMatch(cleaned);

            if (normalized.IsNotNullOrWhiteSpace())
            {
                yield return normalized;
            }

            // "ピースサイン - Peace Sign" is stored under either side alone.
            if (cleaned.Contains(" - "))
            {
                foreach (var half in cleaned.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var normalizedHalf = NormalizeTrackTitleForMatch(half);

                    if (normalizedHalf.IsNotNullOrWhiteSpace())
                    {
                        yield return normalizedHalf;
                    }
                }
            }
        }

        // "Pt. 2" / "Pt 2" -> "Part 2"
        private static readonly Regex PartAbbreviationRegex = new Regex(
            @"\bPt\.?(?=\s)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Japanese pressings append the transliteration: "Lovefool = ラヴフール"
        private static readonly Regex AlternateTitleRegex = new Regex(
            @"\s*=\s*\S.*$", RegexOptions.Compiled);

        // Track index prefixes on conceptual albums: "V. 3005"
        private static readonly Regex LeadingIndexRegex = new Regex(
            @"^\s*[IVX]+\.\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Dash-wrapped version markers: "Wild Side -Anime ver.-"
        private static readonly Regex DashedVersionRegex = new Regex(
            @"\s*-[^-]*\b(?:ver|version|mix|edit|remix|size)\b\.?[^-]*-\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static string StripEditorialSuffix(string title)
        {
            if (title.IsNullOrWhiteSpace())
            {
                return title;
            }

            return EditorialTailRegex.Replace(FeaturedArtistRegex.Replace(title, string.Empty), string.Empty).Trim();
        }

        // " - Remastered 2011", " - Live", " - 2012 Remaster", " - Single Version" ...
        private static readonly Regex EditorialTailRegex = new Regex(
            @"\s+-\s+[^-]*\b(?:remaster(?:ed)?|live|mono|stereo|version|edit|mix|edition|anniversary|bonus|demo|acoustic|instrumental|radio|session|take)\b.*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // "(feat. X)", "[featuring X]", "(with X)"
        private static readonly Regex FeaturedArtistRegex = new Regex(
            @"\s*[\(\[]\s*(?:feat|ft|featuring|with)\b\.?[^\)\]]*[\)\]]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
            return MatchTracksByTitle(tracks, titles).Select(t => t.Id).ToList();
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
