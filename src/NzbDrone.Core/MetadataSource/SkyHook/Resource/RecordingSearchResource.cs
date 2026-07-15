using System.Collections.Generic;

namespace NzbDrone.Core.MetadataSource.SkyHook.Resource
{
    public class RecordingSearchResource
    {
        public List<RecordingResource> Recordings { get; set; }
    }

    public class RecordingResource
    {
        public string Id { get; set; }
    }
}
