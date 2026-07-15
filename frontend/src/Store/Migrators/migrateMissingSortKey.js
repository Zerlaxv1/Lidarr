import { get } from 'lodash';

const MISSING_SORT_KEYS = [
  'artists.sortName',
  'albums.title',
  'absoluteTrackNumber',
  'title',
  'albums.releaseDate'
];

export default function migrateMissingSortKey(persistedState) {
  const sortKey = get(persistedState, 'wanted.missing.sortKey');

  if (!sortKey) {
    return;
  }

  if (!MISSING_SORT_KEYS.includes(sortKey)) {
    persistedState.wanted.missing.sortKey = 'albums.releaseDate';
  }
}
