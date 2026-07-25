import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import createAlbumSelector from 'Store/Selectors/createAlbumSelector';
import createQueueItemSelector from 'Store/Selectors/createQueueItemSelector';
import createTrackFileSelector from 'Store/Selectors/createTrackFileSelector';
import EpisodeStatus from './EpisodeStatus';

function createMapStateToProps() {
  return createSelector(
    createAlbumSelector(),
    createQueueItemSelector(),
    createTrackFileSelector(),
    (state, { trackId }) => trackId,
    (album, queueItem, trackFile, trackId) => {
      const result = _.pick(album, [
        'releaseDate',
        'monitored',
        'grabbed'
      ]);

      if (trackId) {
        // Both the grabbed flag and the queue are album wide, and the queue carries no track
        // id to narrow its item down with, so neither says anything about the single track
        // being rendered. Showing them would repeat the very same progress bar, or the very
        // same downloading icon, on every row of the album.
        result.grabbed = false;
        result.queueItem = null;
      } else {
        result.queueItem = queueItem;
      }

      result.trackFile = trackFile;

      return result;
    }
  );
}

const mapDispatchToProps = {
};

// Own props last: the album wide values above only stand in for what the caller does not
// know. A row that renders a single track knows that track's monitored state and has to win,
// which the default merge (own props first) would not allow.
function mergeProps(stateProps, dispatchProps, ownProps) {
  return {
    ...stateProps,
    ...dispatchProps,
    ...ownProps
  };
}

class EpisodeStatusConnector extends Component {

  //
  // Render

  render() {
    return (
      <EpisodeStatus
        {...this.props}
      />
    );
  }
}

EpisodeStatusConnector.propTypes = {
  albumId: PropTypes.number.isRequired,
  trackId: PropTypes.number,
  trackFileId: PropTypes.number.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps, mergeProps)(EpisodeStatusConnector);
