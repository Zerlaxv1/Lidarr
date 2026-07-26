# Lidarr

[![Build Status](https://dev.azure.com/Lidarr/Lidarr/_apis/build/status/lidarr.Lidarr?branchName=develop)](https://dev.azure.com/Lidarr/Lidarr/_build/latest?definitionId=1&branchName=develop)
[![Translation status](https://translate.servarr.com/widget/servarr/lidarr/svg-badge.svg)](https://translate.servarr.com/engage/servarr/?utm_source=widget)
[![Docker Pulls](https://img.shields.io/docker/pulls/linuxserver/lidarr.svg)](https://wiki.servarr.com/lidarr/installation#docker)
![Github Downloads](https://img.shields.io/github/downloads/lidarr/lidarr/total.svg)
[![Backers on Open Collective](https://opencollective.com/lidarr/backers/badge.svg)](#backers) 
[![Sponsors on Open Collective](https://opencollective.com/lidarr/sponsors/badge.svg)](#sponsors)

Lidarr is a music collection manager for Usenet and BitTorrent users. It can monitor multiple RSS feeds for new tracks from your favorite artists and will grab, sort and rename them. It can also be configured to automatically upgrade the quality of files already downloaded when a better quality format becomes available.

> [!NOTE]
> This is [Zerlaxv1/Lidarr](https://github.com/Zerlaxv1/Lidarr)'s `song-mode` fork: adds track-level ("song mode") monitoring and per-track search on top of upstream Lidarr, so individual songs can be monitored/acquired instead of whole albums. Pairs with the [Tubifarry `song-mode` fork](https://github.com/Zerlaxv1/Tubifarry/tree/song-mode) for per-track downloads.

> [!WARNING]
> NOTICE - The Lidarr Metadata Server is recovering and rebuilding the cache which is impacting adding artists, library imports, etc. Please follow [GHI 5498](https://github.com/Lidarr/Lidarr/issues/5498) or see Discord for details.

> [!IMPORTANT]
> Much of the song-mode work in this fork was written with AI assistance (Claude), reviewed
> and tested by a human before merging. It is covered by unit tests and exercised against a
> real library, but it has not had the scrutiny upstream code gets. Read the diff before you
> run it on a collection you care about.

## What this fork adds

Upstream Lidarr monitors **albums**: you follow an album, it searches for that album, it
downloads the album. This fork adds **song mode** — following individual tracks — and everything
that has to change downstream for that to actually work.

Per-track downloads need the companion [Tubifarry `song-mode` fork](https://github.com/Zerlaxv1/Tubifarry/tree/song-mode);
the Lucida indexer is the one that returns genuine single-track releases.

**Monitoring a single track**

* `Monitored` lives on the track (per release), not only on the album, with a migration that
  backfills existing libraries.
* Toggle any track from the album detail page; the album keeps its own monitored state.
* Switching an album's release, refreshing its metadata, or re-importing a file preserves which
  tracks you picked — including the file links, which a rescan cannot recover on its own.
* Monitoring an album no longer overrides a track selection you made by hand.

**Searching and grabbing one track**

* Automatic and manual (interactive) search at track level, including
  `GET /api/v1/release?trackId=`.
* Missing and cutoff-unmet sweeps search a partially monitored album track by track instead of
  grabbing the whole thing.
* Import specifications accept a legitimate single-track grab instead of rejecting it for
  "missing tracks", and importing one track no longer wipes the album's other files.
* A release must actually name the searched track — indexers that don't understand a track
  search answer with the whole album.
* Queue, pending releases and size checks reason per track, so two tracks of one album download
  independently and a single-track release isn't judged against a 45-minute album.

**Import lists that follow songs**

* `Specific Track` monitor type: a list monitors exactly the tracks it names, and never
  unmonitors the ones it doesn't.
* Deduplication keeps every track of an album instead of collapsing them into one item.
* Items missing from the Spotify→MusicBrainz mapping table are resolved by name instead of
  being dropped.
* Track titles match across edition suffixes (`- Remastered 2011`, `- Live`, `(feat. X)`),
  abbreviations (`Pt.` / `Part`) and scripts, using MusicBrainz recording aliases for
  transliterated titles (`Akuma no Ko` ↔ the Japanese title).
* Albums named in another script resolve through MusicBrainz aliases, requiring both the artist
  and the album to match.
* A Spotify token the renewal never returned is no longer trusted, and one flaky metadata
  request no longer aborts the entire sync.

**Interface**

* Track rows show their own monitored/queue state rather than the album's.
* Album completion counts only the tracks you actually want, so a song-mode album can read as
  complete.
* Wanted/Missing works at track level.

> [!NOTE]
> Building this fork: the plugin only loads when it was compiled against the exact
> `Lidarr.Core.dll` the host runs (`src/Directory.Build.props` wildcards `AssemblyVersion`, so
> every independent build differs). Build host and plugin in one pass, or pin the version.

## Major Features Include:

* Support for major platforms: Windows, Linux, macOS, Raspberry Pi, etc.
* Automatically detects new tracks.
* Can scan your existing library and download any missing tracks.
* Can watch for better quality of the tracks you already have and do an automatic upgrade.
* Automatic failed download handling will try another release if one fails
* Manual search so you can pick any release or to see why a release was not downloaded automatically
* Fully configurable track renaming
* Full integration with SABnzbd and NZBGet
* Full integration with Kodi, Plex (notification, library update, metadata)
* Full support for specials and multi-album releases
* And a beautiful UI

## Support

This fork is maintained for personal use and comes with no support. Please do not raise
song-mode problems with the upstream project — they don't ship this code.

For Lidarr itself, upstream is the place to go:

[![Discord](https://img.shields.io/badge/discord-chat-7289DA.svg?maxAge=60)](https://lidarr.audio/discord)
[![Wiki](https://img.shields.io/badge/servarr-wiki-181717.svg?maxAge=60)](https://wiki.servarr.com/lidarr)

## Credits

This is a fork of [Lidarr](https://github.com/Lidarr/Lidarr) — all of the music
management this builds on is their work, and the upstream project is where to look for
support, documentation and the community.

The companion plugin is a fork of [Tubifarry](https://github.com/TypNull/Tubifarry) by
TypNull.

### License

* [GNU GPL v3](http://www.gnu.org/licenses/gpl.html) — see [LICENSE.md](LICENSE.md)
* Copyright 2010-2021 Lidarr contributors
