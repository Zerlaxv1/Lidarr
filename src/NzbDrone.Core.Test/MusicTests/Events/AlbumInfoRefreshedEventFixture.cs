using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Events;

namespace NzbDrone.Core.Test.MusicTests.Events
{
    [TestFixture]
    public class AlbumInfoRefreshedEventFixture
    {
        [Test]
        public void should_assign_updated_albums()
        {
            var added = new List<Album>();
            var updated = new List<Album> { new Album { Id = 1 } };
            var removed = new List<Album>();
            var artist = new Artist();

            var message = new AlbumInfoRefreshedEvent(artist, added, updated, removed);

            message.Updated.Should().BeEquivalentTo(updated);
        }
    }
}
