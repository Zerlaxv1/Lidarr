import migrateAddArtistDefaults from './migrateAddArtistDefaults';
import migrateBlacklistToBlocklist from './migrateBlacklistToBlocklist';
import migrateMissingSortKey from './migrateMissingSortKey';

export default function migrate(persistedState) {
  migrateAddArtistDefaults(persistedState);
  migrateBlacklistToBlocklist(persistedState);
  migrateMissingSortKey(persistedState);
}
