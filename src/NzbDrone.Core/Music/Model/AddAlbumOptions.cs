using System.Collections.Generic;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Music
{
    public class AddAlbumOptions : IEmbeddedDocument
    {
        public AddAlbumOptions()
        {
            // default in case not set in db
            AddType = AlbumAddType.Automatic;
            MonitorTrackTitles = new List<string>();
        }

        public AlbumAddType AddType { get; set; }
        public bool SearchForNewAlbum { get; set; }
        public List<string> MonitorTrackTitles { get; set; }
    }

    public enum AlbumAddType
    {
        Automatic,
        Manual
    }
}
