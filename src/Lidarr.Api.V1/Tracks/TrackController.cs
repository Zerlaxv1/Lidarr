using System.Collections.Generic;
using System.Linq;
using Lidarr.Api.V1.Albums;
using Lidarr.Http;
using Lidarr.Http.REST;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Events;
using NzbDrone.SignalR;

namespace Lidarr.Api.V1.Tracks
{
    [V1ApiController]
    public class TrackController : TrackControllerWithSignalR
    {
        private readonly IAlbumService _albumService;
        private readonly IEventAggregator _eventAggregator;

        public TrackController(IArtistService artistService,
                             ITrackService trackService,
                             IAlbumService albumService,
                             IUpgradableSpecification upgradableSpecification,
                             ICustomFormatCalculationService formatCalculator,
                             IEventAggregator eventAggregator,
                             IBroadcastSignalRMessage signalRBroadcaster)
            : base(trackService, artistService, upgradableSpecification, formatCalculator, signalRBroadcaster)
        {
            _albumService = albumService;
            _eventAggregator = eventAggregator;
        }

        [HttpGet]
        public List<TrackResource> GetTracks([FromQuery]int? artistId,
            [FromQuery]int? albumId,
            [FromQuery]int? albumReleaseId,
            [FromQuery]List<int> trackIds)
        {
            if (!artistId.HasValue && !trackIds.Any() && !albumId.HasValue && !albumReleaseId.HasValue)
            {
                throw new BadRequestException("One of artistId, albumId, albumReleaseId or trackIds must be provided");
            }

            if (artistId.HasValue && !albumId.HasValue)
            {
                return MapToResource(_trackService.GetTracksByArtist(artistId.Value), false, false);
            }

            if (albumReleaseId.HasValue)
            {
                return MapToResource(_trackService.GetTracksByRelease(albumReleaseId.Value), false, false);
            }

            if (albumId.HasValue)
            {
                return MapToResource(_trackService.GetTracksByAlbum(albumId.Value), false, false);
            }

            return MapToResource(_trackService.GetTracks(trackIds), false, false);
        }

        [HttpGet("lookup")]
        public List<TrackResource> LookupTracks([FromQuery] string term, [FromQuery] int limit = 10)
        {
            if (term.IsNullOrWhiteSpace())
            {
                return new List<TrackResource>();
            }

            var tracks = _trackService.SearchTracksByTitle(term, limit);
            var resources = MapToResource(tracks, true, false);

            for (int i = 0; i < resources.Count; i++)
            {
                resources[i].Album = tracks[i].Album.ToResource();
            }

            return resources;
        }

        [HttpPut("monitor")]
        public IActionResult SetTracksMonitored([FromBody] TracksMonitoredResource resource)
        {
            _trackService.SetMonitored(resource.TrackIds, resource.Monitored);

            var tracks = _trackService.GetTracks(resource.TrackIds);

            // Track counts and completion percentages are cached per artist and only
            // invalidated by album-level events, so without this the sidebar keeps showing
            // the pre-toggle numbers until the next artist refresh.
            foreach (var albumId in tracks.Select(t => t.AlbumId).Where(id => id > 0).Distinct())
            {
                var album = _albumService.GetAlbum(albumId);
                _eventAggregator.PublishEvent(new AlbumEditedEvent(album, album));
            }

            return Accepted(MapToResource(tracks, false, false));
        }
    }
}
