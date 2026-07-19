import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Autosuggest from 'react-autosuggest';
import Icon from 'Components/Icon';
import keyboardShortcuts, { shortcuts } from 'Components/keyboardShortcuts';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import { icons } from 'Helpers/Props';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import ArtistSearchResult from './ArtistSearchResult';
import FuseWorker from './fuse.worker';
import TrackSearchResult from './TrackSearchResult';
import styles from './ArtistSearchInput.css';

const ADD_NEW_TYPE = 'addNew';
const TRACK_TYPE = 'track';

class ArtistSearchInput extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this._autosuggest = null;
    this._worker = null;
    this._abortTrackRequest = null;

    this.state = {
      value: '',
      suggestions: [],
      trackSuggestions: []
    };
  }

  componentDidMount() {
    this.props.bindShortcut(shortcuts.ARTIST_SEARCH_INPUT.key, this.focusInput);
  }

  componentWillUnmount() {
    if (this._worker) {
      this._worker.removeEventListener('message', this.onSuggestionsReceived, false);
      this._worker.terminate();
      this._worker = null;
    }

    if (this._abortTrackRequest) {
      this._abortTrackRequest();
    }
  }

  getWorker() {
    if (!this._worker) {
      this._worker = new FuseWorker();
      this._worker.addEventListener('message', this.onSuggestionsReceived, false);
    }

    return this._worker;
  }

  //
  // Control

  setAutosuggestRef = (ref) => {
    this._autosuggest = ref;
  };

  focusInput = (event) => {
    event.preventDefault();
    this._autosuggest.input.focus();
  };

  getSectionSuggestions(section) {
    return section.suggestions;
  }

  renderSectionTitle(section) {
    return (
      <div className={styles.sectionTitle}>
        {section.title}

        {
          section.loading &&
            <LoadingIndicator
              className={styles.loading}
              rippleClassName={styles.ripple}
              size={20}
            />
        }
      </div>
    );
  }

  getSuggestionValue({ title }) {
    return title;
  }

  renderSuggestion(item, { query }) {
    if (item.type === ADD_NEW_TYPE) {
      return (
        <div className={styles.addNewArtistSuggestion}>
          Search for {query}
        </div>
      );
    }

    if (item.type === TRACK_TYPE) {
      return (
        <TrackSearchResult
          {...item.item}
        />
      );
    }

    return (
      <ArtistSearchResult
        {...item.item}
        match={item.matches[0]}
      />
    );
  }

  goToArtist(item) {
    this.setState({ value: '' });
    this.props.onGoToArtist(item.item.foreignArtistId);
  }

  goToTrackAlbum(item) {
    this.setState({ value: '' });
    this.props.onGoToAlbum(item.item.album.foreignAlbumId);
  }

  reset() {
    this.setState({
      value: '',
      suggestions: [],
      trackSuggestions: [],
      loading: false
    });
  }

  //
  // Listeners

  onChange = (event, { newValue, method }) => {
    if (method === 'up' || method === 'down') {
      return;
    }

    this.setState({ value: newValue });
  };

  onKeyDown = (event) => {
    if (event.shiftKey || event.altKey || event.ctrlKey) {
      return;
    }

    if (event.key === 'Escape') {
      this.reset();
      return;
    }

    if (event.key !== 'Tab' && event.key !== 'Enter') {
      return;
    }

    const {
      suggestions,
      value
    } = this.state;

    const {
      highlightedSectionIndex,
      highlightedSuggestionIndex
    } = this._autosuggest.state;

    if (!suggestions.length || highlightedSectionIndex) {
      this.props.onGoToAddNewArtist(value);
      this._autosuggest.input.blur();
      this.reset();

      return;
    }

    // If an suggestion is not selected go to the first artist,
    // otherwise go to the selected artist.

    if (highlightedSuggestionIndex == null) {
      this.goToArtist(suggestions[0]);
    } else {
      this.goToArtist(suggestions[highlightedSuggestionIndex]);
    }

    this._autosuggest.input.blur();
    this.reset();
  };

  onBlur = () => {
    this.reset();
  };

  onSuggestionsFetchRequested = ({ value }) => {
    if (!this.state.loading) {
      this.setState({
        loading: true
      });
    }

    this.requestSuggestions(value);
    this.requestTrackSuggestions(value);
  };

  requestSuggestions = _.debounce((value) => {
    if (!this.state.loading) {
      return;
    }

    const requestLoading = this.state.requestLoading;

    this.setState({
      requestValue: value,
      requestLoading: true
    });

    if (!requestLoading) {
      const payload = {
        value,
        artists: this.props.artists
      };

      this.getWorker().postMessage(payload);
    }
  }, 250);

  requestTrackSuggestions = _.debounce((value) => {
    if (this._abortTrackRequest) {
      this._abortTrackRequest();
    }

    if (!value) {
      this.setState({ trackSuggestions: [] });
      return;
    }

    const { request, abortRequest } = createAjaxRequest({
      url: '/track/lookup',
      data: { term: value }
    });

    this._abortTrackRequest = abortRequest;

    request.done((data) => {
      this.setState({
        trackSuggestions: data.map((track) => ({ type: TRACK_TYPE, item: track, title: track.title }))
      });
    });

    request.fail((xhr) => {
      if (!xhr.aborted) {
        this.setState({ trackSuggestions: [] });
      }
    });
  }, 250);

  onSuggestionsReceived = (message) => {
    const {
      value,
      suggestions
    } = message.data;

    if (!this.state.loading) {
      this.setState({
        requestValue: null,
        requestLoading: false
      });
    } else if (value === this.state.requestValue) {
      this.setState({
        suggestions,
        requestValue: null,
        requestLoading: false,
        loading: false
      });
    } else {
      this.setState({
        suggestions,
        requestLoading: true
      });

      const payload = {
        value: this.state.requestValue,
        artists: this.props.artists
      };

      this.getWorker().postMessage(payload);
    }
  };

  onSuggestionsClearRequested = () => {
    this.setState({
      suggestions: [],
      trackSuggestions: [],
      loading: false
    });
  };

  onSuggestionSelected = (event, { suggestion }) => {
    if (suggestion.type === ADD_NEW_TYPE) {
      this.props.onGoToAddNewArtist(this.state.value);
    } else if (suggestion.type === TRACK_TYPE) {
      this.goToTrackAlbum(suggestion);
    } else {
      this.goToArtist(suggestion);
    }
  };

  //
  // Render

  render() {
    const {
      value,
      loading,
      suggestions,
      trackSuggestions
    } = this.state;

    const suggestionGroups = [];

    if (suggestions.length || loading) {
      suggestionGroups.push({
        title: 'Existing Artist',
        loading,
        suggestions
      });
    }

    if (trackSuggestions.length) {
      suggestionGroups.push({
        title: 'Track',
        suggestions: trackSuggestions
      });
    }

    suggestionGroups.push({
      title: 'Add New Item',
      suggestions: [
        {
          type: ADD_NEW_TYPE,
          title: value
        }
      ]
    });

    const inputProps = {
      ref: this.setInputRef,
      className: styles.input,
      name: 'artistSearch',
      value,
      placeholder: 'Search',
      autoComplete: 'off',
      spellCheck: false,
      onChange: this.onChange,
      onKeyDown: this.onKeyDown,
      onBlur: this.onBlur,
      onFocus: this.onFocus
    };

    const theme = {
      container: styles.container,
      containerOpen: styles.containerOpen,
      suggestionsContainer: styles.artistContainer,
      suggestionsList: styles.list,
      suggestion: styles.listItem,
      suggestionHighlighted: styles.highlighted
    };

    return (
      <div className={styles.wrapper}>
        <Icon name={icons.SEARCH} />

        <Autosuggest
          ref={this.setAutosuggestRef}
          id={name}
          inputProps={inputProps}
          theme={theme}
          focusInputOnSuggestionClick={false}
          multiSection={true}
          suggestions={suggestionGroups}
          getSectionSuggestions={this.getSectionSuggestions}
          renderSectionTitle={this.renderSectionTitle}
          getSuggestionValue={this.getSuggestionValue}
          renderSuggestion={this.renderSuggestion}
          onSuggestionSelected={this.onSuggestionSelected}
          onSuggestionsFetchRequested={this.onSuggestionsFetchRequested}
          onSuggestionsClearRequested={this.onSuggestionsClearRequested}
        />
      </div>
    );
  }
}

ArtistSearchInput.propTypes = {
  artists: PropTypes.arrayOf(PropTypes.object).isRequired,
  onGoToArtist: PropTypes.func.isRequired,
  onGoToAddNewArtist: PropTypes.func.isRequired,
  onGoToAlbum: PropTypes.func.isRequired,
  bindShortcut: PropTypes.func.isRequired
};

export default keyboardShortcuts(ArtistSearchInput);
