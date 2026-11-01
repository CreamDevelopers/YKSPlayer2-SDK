# Manifest: `yks-plugin.json`

Every plugin has a `yks-plugin.json` at the root of its package.

```json
{
  "schema": 1,
  "id": "example.tracker",
  "name": "Tracker Modules",
  "version": "1.2.0",
  "publisher": "Example",
  "description": "MOD / XM / IT playback",
  "license": "MIT",
  "repository": "https://github.com/example/yks-tracker",
  "kind": "decoder",
  "entry": "tracker.dll",
  "engines": { "yksplayer": ">=2.0.0 <3.0.0", "pluginAbi": 1 },
  "permissions": ["audio.decode"],
  "dependencies": { "example.common": ">=1.0.0 <2.0.0" },
  "decoder": { "extensions": [".mod", ".xm", ".it"] },
  "categories": ["decoder", "tracker"],
  "credits": "Uses libfoo (MIT)"
}
```

| Key | Required | Meaning |
|---|---|---|
| `schema` | yes | `1` |
| `id` | yes | Unique, lower case, dotted: `publisher.name`. Never changes between versions. |
| `name`, `publisher`, `description`, `license` | name, publisher | Shown in the player. |
| `version` | yes | [SemVer](https://semver.org) of the plugin. |
| `repository` | no | Where the plugin lives (usually GitHub); shown as a link on the plugin card. |
| `kind` | yes | `decoder`, `dsp`, `visualizer` or `extension`. |
| `entry` | yes | Path inside the package: a `.dll` (native), a `.cso` (visualizer), a `.dll` (.NET extension) or `.exe` (exe extension). |
| `icon` | no | Square icon inside the package, `.svg` (preferred) or `.png`, at most 256 KB. It is drawn as a whole tile at 44 px and larger, so give it its own background and rounded corners (`rx` ≈ 22% of the size). Without one, the player shows a glyph for the plugin kind. |
| `engines.yksplayer` | yes | SemVer range of supported player versions. Space-separated comparators: `>=2.0.0 <3.0.0`. |
| `engines.pluginAbi` | native | `YKS_PLUGIN_ABI` the DLL was built against (`1`). |
| `permissions` | yes (may be empty) | See below. |
| `dependencies` | no | Other plugin ids and the version ranges needed. Installation fails if they are missing. |
| `decoder.extensions` | decoder | File extensions with the dot. Built-in formats always take precedence. |
| `visualizer.bins` | no | Spectrum bins the shader reads (default 64). |
| `extension.runtime` | extension | `dotnet` (entry is an assembly run by the player's host) or `exe`. |
| `files` | written by `yks-pack` | Path → SHA-256 of every file. Do not write it by hand. |

## Permissions

| Permission | Allows |
|---|---|
| `audio.decode` | Receiving the bytes of files to decode (decoders) |
| `audio.process` | Processing the audio being played (effects) |
| `playback.read` | Receiving `playback/trackChanged` and `playback/stateChanged` |
| `playback.control` | Sending `playback/control` (play, pause, next, previous) |
| `library.read` | Seeing file paths in track information |
| `lyrics.provide` | Answering `lyrics/get` |
| `lyrics.translate` | Receiving the playing track's lyrics in `lyrics/translate` to translate them (SDK 1.2) |
| `lyrics.pronounce` | Receiving the playing track's lyrics in `lyrics/pronounce` to spell out how they sound (SDK 1.2) |
| `metadata.provide` | Reserved for metadata providers |
| `ui.panel` | Panels and `panel/invalidate`, `ui/notify` |
| `ui.command` | Commands in the player's menus, `ui/notify` |
| `net`, `net:<host>` | Declares network use (shown to the user; see security.md) |

The player shows the permissions in plain language and enables a plugin only after the user agrees.
An update that asks for more permissions is installed disabled until the user agrees again.
