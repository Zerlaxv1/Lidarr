import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import * as commandNames from 'Commands/commandNames';
import { executeCommand } from 'Store/Actions/commandActions';
import createArtistSelector from 'Store/Selectors/createArtistSelector';
import MissingRow from './MissingRow';

function createMapStateToProps() {
  return createSelector(
    createArtistSelector(),
    (artist) => {
      return {
        artist
      };
    }
  );
}

function createMapDispatchToProps(dispatch, props) {
  return {
    onSearchPress() {
      dispatch(executeCommand({
        name: commandNames.TRACK_SEARCH,
        trackIds: [props.id]
      }));
    }
  };
}

export default connect(createMapStateToProps, createMapDispatchToProps)(MissingRow);
