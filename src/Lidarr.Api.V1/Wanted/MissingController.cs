using Lidarr.Api.V1.Albums;
using Lidarr.Api.V1.Tracks;
using Lidarr.Http;
using Lidarr.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Music;
using NzbDrone.SignalR;

namespace Lidarr.Api.V1.Wanted
{
    [V1ApiController("wanted/missing")]
    public class MissingController : TrackControllerWithSignalR
    {
        public MissingController(ITrackService trackService,
                                 IArtistService artistService,
                                 IUpgradableSpecification upgradableSpecification,
                                 ICustomFormatCalculationService formatCalculator,
                                 IBroadcastSignalRMessage signalRBroadcaster)
            : base(trackService, artistService, upgradableSpecification, formatCalculator, signalRBroadcaster)
        {
        }

        [HttpGet]
        [Produces("application/json")]
        public PagingResource<TrackResource> GetMissingTracks([FromQuery] PagingRequestResource paging, bool includeArtist = false, bool includeAlbum = false, bool monitored = true)
        {
            var pagingResource = new PagingResource<TrackResource>(paging);
            var pagingSpec = pagingResource.MapToPagingSpec<TrackResource, Track>("albums.releaseDate", SortDirection.Descending);

            return pagingSpec.ApplyToPage(
                spec => _trackService.TracksWithoutFilesPaged(spec, monitored),
                track =>
                {
                    var resource = MapToResource(track, includeArtist, false);

                    if (includeAlbum)
                    {
                        resource.Album = track.Album.ToResource();
                    }

                    return resource;
                });
        }
    }
}
