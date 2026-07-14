namespace NzbDrone.Core.IndexerSearch.Definitions
{
    public class TrackSearchCriteria : AlbumSearchCriteria
    {
        public string TrackTitle { get; set; }

        public string CleanTrackQuery => GetQueryTitle(TrackTitle);

        public override string ToString()
        {
            return $"[{Artist.Name} - {AlbumTitle} - {TrackTitle}]";
        }
    }
}
