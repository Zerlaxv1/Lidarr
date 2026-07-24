using System;
using System.Collections.Generic;
using FluentAssertions;
using FluentValidation.Results;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Test.ImportListTests
{
    [TestFixture]
    public class ImportListBaseFixture : CoreTest<ImportListBaseFixture.TestImportList>
    {
        [SetUp]
        public void Setup()
        {
            Subject.Definition = new ImportListDefinition { Id = 1, Name = "test" };
        }

        [Test]
        public void should_keep_every_track_of_the_same_album_in_song_mode()
        {
            var items = new List<ImportListItemInfo>
            {
                new ImportListItemInfo { Artist = "Muse", Album = "Absolution", TrackTitle = "Hysteria" },
                new ImportListItemInfo { Artist = "Muse", Album = "Absolution", TrackTitle = "Time Is Running Out" }
            };

            Subject.Cleanup(items).Should().HaveCount(2);
        }

        [Test]
        public void should_still_collapse_identical_tracks()
        {
            var items = new List<ImportListItemInfo>
            {
                new ImportListItemInfo { Artist = "Muse", Album = "Absolution", TrackTitle = "Hysteria" },
                new ImportListItemInfo { Artist = "Muse", Album = "Absolution", TrackTitle = "Hysteria" }
            };

            Subject.Cleanup(items).Should().HaveCount(1);
        }

        [Test]
        public void should_still_collapse_album_items_without_track_titles()
        {
            var items = new List<ImportListItemInfo>
            {
                new ImportListItemInfo { Artist = "Muse", Album = "Absolution" },
                new ImportListItemInfo { Artist = "Muse", Album = "Absolution" }
            };

            Subject.Cleanup(items).Should().HaveCount(1);
        }

        public class TestImportList : ImportListBase<TestImportListSettings>
        {
            public TestImportList(IImportListStatusService importListStatusService, IConfigService configService, IParsingService parsingService, Logger logger)
                : base(importListStatusService, configService, parsingService, logger)
            {
            }

            public override string Name => "Test";

            public override ImportListType ListType => ImportListType.Other;

            public override TimeSpan MinRefreshInterval => TimeSpan.FromHours(1);

            public override IList<ImportListItemInfo> Fetch() => new List<ImportListItemInfo>();

            public IList<ImportListItemInfo> Cleanup(IEnumerable<ImportListItemInfo> releases) => CleanupListItems(releases);

            protected override void Test(List<ValidationFailure> failures)
            {
            }
        }

        public class TestImportListSettings : IImportListSettings
        {
            public string BaseUrl { get; set; }

            public NzbDroneValidationResult Validate() => new NzbDroneValidationResult();
        }
    }
}
