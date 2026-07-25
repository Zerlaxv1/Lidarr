using System;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.ImportLists.Spotify;
using NzbDrone.Core.Test.Framework;
using SpotifyAPI.Web.Models;

namespace NzbDrone.Core.Test.ImportListTests
{
    [TestFixture]

    // the base import list class is abstract so use the followed artists one
    public class SpotifyTokenRefreshFixture : CoreTest<SpotifyFollowedArtists>
    {
        private SpotifyFollowedArtistsSettings _settings;

        [SetUp]
        public void Setup()
        {
            _settings = new SpotifyFollowedArtistsSettings
            {
                AccessToken = "stale-token",
                RefreshToken = "refresh-token",
                Expires = DateTime.UtcNow.AddMinutes(-5)
            };

            Subject.Definition = new ImportListDefinition
            {
                Id = 1,
                Name = "test",
                Settings = _settings
            };
        }

        private void GivenRenewReturns(Token token)
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.Get<Token>(It.IsAny<HttpRequest>()))
                .Returns<HttpRequest>(r => new HttpResponse<Token>(new HttpResponse(r, new HttpHeader(), token.ToJson())));
        }

        [Test]
        public void should_store_a_renewed_token()
        {
            GivenRenewReturns(new Token { AccessToken = "fresh-token", ExpiresIn = 3600, RefreshToken = "new-refresh" });

            Subject.RefreshToken();

            _settings.AccessToken.Should().Be("fresh-token");
            _settings.RefreshToken.Should().Be("new-refresh");
            _settings.Expires.Should().BeAfter(DateTime.UtcNow.AddMinutes(50));

            Mocker.GetMock<IImportListRepository>()
                .Verify(v => v.UpdateSettings(It.IsAny<ImportListDefinition>()), Times.Once());
        }

        // Pushing Expires an hour out for a token that was never returned makes GetApi skip
        // every renewal for that hour, so the list keeps failing with an authorization error
        // and never retries.
        [TestCase(null, 3600)]
        [TestCase("", 3600)]
        [TestCase("fresh-token", 0)]
        public void should_not_extend_expiry_when_renewal_returns_no_usable_token(string accessToken, int expiresIn)
        {
            var before = _settings.Expires;

            GivenRenewReturns(new Token { AccessToken = accessToken, ExpiresIn = expiresIn });

            Subject.RefreshToken();

            _settings.Expires.Should().Be(before);
            _settings.AccessToken.Should().Be("stale-token");

            Mocker.GetMock<IImportListRepository>()
                .Verify(v => v.UpdateSettings(It.IsAny<ImportListDefinition>()), Times.Never());
        }

        [Test]
        public void should_expire_a_token_spotify_rejected()
        {
            _settings.Expires = DateTime.UtcNow.AddHours(1);

            Subject.ExpireToken();

            _settings.Expires.Should().BeOnOrBefore(DateTime.UtcNow);

            Mocker.GetMock<IImportListRepository>()
                .Verify(v => v.UpdateSettings(It.IsAny<ImportListDefinition>()), Times.Once());
        }
    }
}
