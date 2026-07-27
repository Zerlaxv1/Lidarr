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

        private static AlbumRelease MultiDisc(string format, string country, int discs, int trackCount)
        {
            var media = new List<Medium>();

            for (var i = 0; i < discs; i++)
            {
                media.Add(new Medium { Format = format });
            }

            return new AlbumRelease { Media = media, Country = new List<string> { country }, TrackCount = trackCount };
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

        [Test]
        public void should_prefer_the_plain_edition_over_an_expanded_one()
        {
            // Both are Digital Media [Worldwide]; picking the biggest tracklist means every
            // album lands on a deluxe/anniversary edition instead of the album itself.
            var deluxe = Release("Digital Media", "[Worldwide]", 21);
            var standard = Release("Digital Media", "[Worldwide]", 14);

            var result = MonitoredReleaseSelector.SelectPreferred(new[] { deluxe, standard }, _ => 0);

            result.Should().BeSameAs(standard);
        }

        [Test]
        public void should_prefer_a_single_disc_edition_over_a_multi_disc_one()
        {
            var twoDisc = MultiDisc("Digital Media", "[Worldwide]", 2, 14);
            var singleDisc = MultiDisc("Digital Media", "[Worldwide]", 1, 14);

            var result = MonitoredReleaseSelector.SelectPreferred(new[] { twoDisc, singleDisc }, _ => 0);

            result.Should().BeSameAs(singleDisc);
        }
    }
}
