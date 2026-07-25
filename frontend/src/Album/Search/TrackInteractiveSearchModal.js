import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import Button from 'Components/Link/Button';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { scrollDirections, sizes } from 'Helpers/Props';
import InteractiveSearchConnector from 'InteractiveSearch/InteractiveSearchConnector';
import { cancelFetchReleases, clearReleases } from 'Store/Actions/releaseActions';
import translate from 'Utilities/String/translate';

function createMapDispatchToProps(dispatch, props) {
  return {
    dispatchCancelFetchReleases() {
      dispatch(cancelFetchReleases());
    },

    dispatchClearReleases() {
      dispatch(clearReleases());
    },

    onModalClose() {
      dispatch(cancelFetchReleases());
      dispatch(clearReleases());
      props.onModalClose();
    }
  };
}

// Song mode monitors single tracks, so the interactive search has to be able to target
// one - the releases endpoint takes a trackId the same way it takes an albumId.
class TrackInteractiveSearchModal extends Component {

  //
  // Lifecycle

  componentWillUnmount() {
    this.props.dispatchCancelFetchReleases();
    this.props.dispatchClearReleases();
  }

  //
  // Render

  render() {
    const {
      isOpen,
      trackId,
      trackTitle,
      onModalClose
    } = this.props;

    return (
      <Modal
        isOpen={isOpen}
        size={sizes.EXTRA_EXTRA_LARGE}
        closeOnBackgroundClick={false}
        onModalClose={onModalClose}
      >
        <ModalContent onModalClose={onModalClose}>
          <ModalHeader>
            {trackTitle === undefined ?
              translate('InteractiveSearchModalHeader') :
              translate('InteractiveSearchModalHeaderTitle', { title: trackTitle })
            }
          </ModalHeader>

          <ModalBody scrollDirection={scrollDirections.BOTH}>
            <InteractiveSearchConnector
              type="album"
              searchPayload={{
                trackId
              }}
            />
          </ModalBody>

          <ModalFooter>
            <Button onPress={onModalClose}>
              {translate('Close')}
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    );
  }
}

TrackInteractiveSearchModal.propTypes = {
  isOpen: PropTypes.bool.isRequired,
  trackId: PropTypes.number.isRequired,
  trackTitle: PropTypes.string,
  onModalClose: PropTypes.func.isRequired,
  dispatchCancelFetchReleases: PropTypes.func.isRequired,
  dispatchClearReleases: PropTypes.func.isRequired
};

export default connect(null, createMapDispatchToProps)(TrackInteractiveSearchModal);
