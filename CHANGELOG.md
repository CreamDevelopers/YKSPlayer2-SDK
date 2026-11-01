# Changelog

## sdk-v1.2.0 — 2026-11-01

For YKSPlayer 2.8. Extension protocol 1, additive: older extensions keep working.

- Lyrics translation (`lyrics.translate`): `TranslatesLyrics`, `TranslateLyricsAsync` (`lyrics/translate`). The player sends the lines of the playing track's lyrics and the language chosen in its settings, and shows the translations under each line.
- Lyrics readings (`lyrics.pronounce`): `PronouncesLyrics`, `PronounceLyricsAsync` (`lyrics/pronounce`). How each line sounds, in katakana, shown right under it; separate from translations, so a reading plugin and a translation plugin work together.

## sdk-v1.1.0 — 2026-10-22

For YKSPlayer 2.4. Extension protocol 1, additive: older extensions keep working.

- Library and playlists: `QueryLibraryAsync`, `PlaylistsAsync`, `PlaylistTracksAsync`, `CreatePlaylistAsync`, `SetPlaylistTracksAsync`.
- Queue and playback: `QueueAsync`, `StatusAsync`, `PlayAsync`, `EnqueueAsync`, `SeekAsync`, `SetVolumeAsync`; events `position` and `queue` (`Events`, `OnPositionAsync`, `OnQueueChangedAsync`).
- Settings declared in the manifest (`settings`: bool, number, text, choice), shown on the plugin card; `Host.Settings`, `OnSettingsChangedAsync`.
- Track context-menu commands (`TrackCommands`, `ExecuteTrackCommandAsync`).
- Panel blocks: `Input`, `Toggle`, `Image`, `Slider`, `Choice`, `Link`; commands receive a value.
- Streaming sources (`source.provide`): `Sources`, `BrowseAsync`, `ResolveAsync`, `PlayItemsAsync`; the player streams http(s) audio (MP3/AAC, internet radio) and shows a Sources page.
- Drawn visualizers (`visualizer.draw`): `Visualizers`, `RenderVisualizerAsync` returning a `DrawList`.
- Metadata providers (`metadata.provide`): `GetMetadataAsync` fills in missing covers and names.
- `OpenUrlAsync` (https, `ui.command`), a private data folder (`Host.DataDirectory`), helper programs (`process.run`), `AddToLibraryAsync` (`library.write`).
- Theme plugins (kind `theme`): `theme.json` with colours and an optional background picture (`ThemeDefinition`).

## sdk-v1.0.0 — 2026-10-21

First release, for YKSPlayer 2.0.

- Native ABI 1 (`yks_plugin.h`): decoders (open from memory, read, seek, tags, codec name) and effects (parameters, latency).
- Manifest `icon` (SVG or PNG) shown on plugin cards.
- Extension protocol 1: panels, commands, lyrics providers, playback events and control.
- Visualizers: Direct2D pixel shaders with `resolution`, `time`, `pulse`, `flash`, `accent`, `glow`, `bands[16]`.
- `yks-pack`: keygen, validate, pack (SHA-256 of every file, ECDSA P-256 signature), verify.
- Samples: sine decoder (C), bit crusher (C++), Nebula visualizer, Listening Diary extension.
- Templates: native effect, native decoder, .NET extension.
