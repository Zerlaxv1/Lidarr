using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Music.Events;

namespace NzbDrone.Core.Music
{
    public class AlbumEditedService : IHandle<AlbumEditedEvent>
    {
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly ITrackService _trackService;

        public AlbumEditedService(IManageCommandQueue commandQueueManager,
            ITrackService trackService)
        {
            _commandQueueManager = commandQueueManager;
            _trackService = trackService;
        }

        public void Handle(AlbumEditedEvent message)
        {
            if (!message.Album.AlbumReleases.IsLoaded || !message.OldAlbum.AlbumReleases.IsLoaded)
            {
                return;
            }

            var newMonitored = message.Album.AlbumReleases.Value.Where(x => x.Monitored).ToList();
            var oldMonitored = message.OldAlbum.AlbumReleases.Value.Where(x => x.Monitored).ToList();
            var newIds = new HashSet<int>(newMonitored.Select(x => x.Id));
            var oldIds = new HashSet<int>(oldMonitored.Select(x => x.Id));

            if (newIds.SetEquals(oldIds) && !(!message.OldAlbum.AnyReleaseOk && message.Album.AnyReleaseOk))
            {
                return;
            }

            // Re-link existing track files to the newly monitored release's tracks
            // instead of blindly zeroing every TrackFileId and hoping a rescan finds
            // them again — a plain rescan cannot recover this link (the file's own
            // tags still pin it to the old, now-unmonitored release).
            foreach (var newRelease in newMonitored)
            {
                _trackService.RelinkTrackFilesToRelease(newRelease, oldMonitored);
            }

            var folders = new List<string> { message.Album.Artist.Value.Path };
            _commandQueueManager.Push(new RescanFoldersCommand(folders, FilterFilesType.Matched, false, null));
        }
    }
}
