using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.MediaFileServiceTests
{
    [TestFixture]
    public class AddManySkippingExistingFixture : DbTest<MediaFileService, TrackFile>
    {
        private string _existingPath;
        private string _newPath;

        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<IMediaFileRepository>(Mocker.Resolve<MediaFileRepository>());

            _existingPath = @"C:\Test\Music\Artist\file1.flac".AsOsAgnostic();
            _newPath = @"C:\Test\Music\Artist\file2.flac".AsOsAgnostic();
        }

        private TrackFile GivenTrackFile(string path)
        {
            return Builder<TrackFile>.CreateNew()
                .With(f => f.Id = 0)
                .With(f => f.Quality = new QualityModel(Quality.MP3_192))
                .With(f => f.Path = path)
                .Build();
        }

        [Test]
        public void should_insert_the_other_files_when_a_path_was_inserted_by_another_task()
        {
            // simulates the row a concurrent scan/rename inserted after the caller read the known files
            Db.Insert(GivenTrackFile(_existingPath));

            var files = new List<TrackFile>
            {
                GivenTrackFile(_existingPath),
                GivenTrackFile(_newPath)
            };

            Subject.AddManySkippingExisting(files);

            AllStoredModels.Select(x => x.Path).Should().BeEquivalentTo(new[] { _existingPath, _newPath });
        }

        [Test]
        public void should_only_publish_added_event_for_the_files_actually_inserted()
        {
            Db.Insert(GivenTrackFile(_existingPath));

            var files = new List<TrackFile>
            {
                GivenTrackFile(_existingPath),
                GivenTrackFile(_newPath)
            };

            Subject.AddManySkippingExisting(files);

            VerifyEventPublished<TrackFileAddedEvent>(Times.Once());
        }

        [Test]
        public void should_not_swallow_insert_failures_that_are_not_an_existing_path()
        {
            var files = new List<TrackFile>
            {
                GivenTrackFile(_newPath),
                GivenTrackFile(null)
            };

            Assert.Catch<Exception>(() => Subject.AddManySkippingExisting(files));
        }

        [Test]
        public void add_many_should_still_throw_for_an_existing_path()
        {
            Db.Insert(GivenTrackFile(_existingPath));

            var files = new List<TrackFile>
            {
                GivenTrackFile(_existingPath)
            };

            Assert.Catch<Exception>(() => Subject.AddMany(files));
        }
    }
}
