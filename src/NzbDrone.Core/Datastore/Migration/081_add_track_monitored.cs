using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(081)]
    public class add_track_monitored : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("Tracks").AddColumn("Monitored").AsBoolean().WithDefaultValue(true);

            // Backfill each track's Monitored from its parent album so existing
            // unmonitored albums don't suddenly have all their tracks wanted.
            Execute.Sql(@"UPDATE ""Tracks"" SET ""Monitored"" = (
                SELECT ""Albums"".""Monitored""
                FROM ""AlbumReleases""
                JOIN ""Albums"" ON ""AlbumReleases"".""AlbumId"" = ""Albums"".""Id""
                WHERE ""AlbumReleases"".""Id"" = ""Tracks"".""AlbumReleaseId"")
                WHERE ""AlbumReleaseId"" IN (SELECT ""Id"" FROM ""AlbumReleases"")");
        }
    }
}
