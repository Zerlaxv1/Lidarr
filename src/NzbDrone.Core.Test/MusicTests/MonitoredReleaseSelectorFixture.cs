using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class MonitoredReleaseSelectorFixture : CoreTest
    {
        private static AlbumRelease Release(string format, string country, int trackCount)
        {
            return new AlbumRelease
            {
                Media = new List<Medium> { new Medium { Format = format } },
                Country = new List<string> { country },
                TrackCount = trackCount
            };
        }

        [Test]
        public void should_prefer_digital_media_worldwide_over_larger_cd()
        {
            var cd = Release("CD", "United States", 33);
            var digitalWorldwide = Release("Digital Media", "[Worldwide]", 12);

            var result = MonitoredReleaseSelector.SelectPreferred(new[] { cd, digitalWorldwide }, _ => 0);

            result.Should().BeSameAs(digitalWorldwide);
        }

        [Test]
        public void should_prefer_worldwide_among_digital_media_releases()
        {
            var digitalUs = Release("Digital Media", "United States", 12);
            var digitalWorldwide = Release("Digital Media", "[Worldwide]", 12);

            var result = MonitoredReleaseSelector.SelectPreferred(new[] { digitalUs, digitalWorldwide }, _ => 0);

            result.Should().BeSameAs(digitalWorldwide);
        }

        [Test]
        public void should_prefer_release_with_files_over_preference()
        {
            var digitalWorldwide = Release("Digital Media", "[Worldwide]", 12);
            var cdWithFiles = Release("CD", "United States", 12);

            var result = MonitoredReleaseSelector.SelectPreferred(
                new[] { digitalWorldwide, cdWithFiles },
                r => r == cdWithFiles ? 5 : 0);

            result.Should().BeSameAs(cdWithFiles);
        }
    }
}
