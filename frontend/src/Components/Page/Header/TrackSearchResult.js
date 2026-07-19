import PropTypes from 'prop-types';
import React from 'react';
import styles from './TrackSearchResult.css';

function TrackSearchResult(props) {
  const {
    title,
    artist,
    album
  } = props;

  return (
    <div className={styles.result}>
      <div className={styles.titles}>
        <div className={styles.title}>
          {title}
        </div>

        <div className={styles.alternateTitle}>
          {artist.artistName} - {album.title}
        </div>
      </div>
    </div>
  );
}

TrackSearchResult.propTypes = {
  title: PropTypes.string.isRequired,
  artist: PropTypes.object.isRequired,
  album: PropTypes.object.isRequired
};

export default TrackSearchResult;
