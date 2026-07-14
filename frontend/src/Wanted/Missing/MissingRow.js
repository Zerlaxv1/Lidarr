import PropTypes from 'prop-types';
import React from 'react';
import AlbumTitleLink from 'Album/AlbumTitleLink';
import ArtistNameLink from 'Artist/ArtistNameLink';
import IconButton from 'Components/Link/IconButton';
import RelativeDateCellConnector from 'Components/Table/Cells/RelativeDateCellConnector';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableSelectCell from 'Components/Table/Cells/TableSelectCell';
import TableRow from 'Components/Table/TableRow';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

function MissingRow(props) {
  const {
    id,
    artist,
    album,
    absoluteTrackNumber,
    title,
    isSelected,
    columns,
    onSelectedChange,
    onSearchPress
  } = props;

  if (!artist || !album) {
    return null;
  }

  return (
    <TableRow>
      <TableSelectCell
        id={id}
        isSelected={isSelected}
        onSelectedChange={onSelectedChange}
      />

      {
        columns.map((column) => {
          const {
            name,
            isVisible
          } = column;

          if (!isVisible) {
            return null;
          }

          if (name === 'artists.sortName') {
            return (
              <TableRowCell key={name}>
                <ArtistNameLink
                  foreignArtistId={artist.foreignArtistId}
                  artistName={artist.artistName}
                />
              </TableRowCell>
            );
          }

          if (name === 'albums.title') {
            return (
              <TableRowCell key={name}>
                <AlbumTitleLink
                  foreignAlbumId={album.foreignAlbumId}
                  title={album.title}
                  disambiguation={album.disambiguation}
                />
              </TableRowCell>
            );
          }

          if (name === 'absoluteTrackNumber') {
            return (
              <TableRowCell key={name}>
                {absoluteTrackNumber}
              </TableRowCell>
            );
          }

          if (name === 'title') {
            return (
              <TableRowCell key={name}>
                {title}
              </TableRowCell>
            );
          }

          if (name === 'albums.releaseDate') {
            return (
              <RelativeDateCellConnector
                key={name}
                date={album.releaseDate}
              />
            );
          }

          if (name === 'actions') {
            return (
              <TableRowCell key={name}>
                <IconButton
                  name={icons.SEARCH}
                  title={translate('AutomaticSearch')}
                  onPress={onSearchPress}
                />
              </TableRowCell>
            );
          }

          return null;
        })
      }
    </TableRow>
  );
}

MissingRow.propTypes = {
  id: PropTypes.number.isRequired,
  artist: PropTypes.object.isRequired,
  album: PropTypes.object.isRequired,
  absoluteTrackNumber: PropTypes.number.isRequired,
  title: PropTypes.string.isRequired,
  isSelected: PropTypes.bool,
  columns: PropTypes.arrayOf(PropTypes.object).isRequired,
  onSelectedChange: PropTypes.func.isRequired,
  onSearchPress: PropTypes.func.isRequired
};

export default MissingRow;
