# Versioning

Four things carry versions, independently:

| What | Where | Rule |
|---|---|---|
| **SDK** (`YKSPlayer.Sdk`, `yks-pack`, docs) | NuGet / Git tags `sdk-vX.Y.Z` | [SemVer 2.0](https://semver.org). A major release only when existing plugins or tools would break. |
| **Native ABI** | `YKS_PLUGIN_ABI` in `yks_plugin.h`, `engines.pluginAbi` in the manifest | An integer. Bumped only for incompatible changes; new optional functions are appended to the tables and detected through `struct_size`, so they do not bump it. The player refuses plugins for another ABI. |
| **Extension protocol** | `Methods.ApiVersion` (`initialize.apiVersion`) | An integer. New methods and new optional fields do not bump it; removing or changing the meaning of anything does. Unknown methods are answered with an error, unknown fields are ignored. |
| **Manifest schema** | `schema` in `yks-plugin.json` | An integer. New optional keys do not bump it. |

## Compatibility promises

- A plugin built for ABI 1 / protocol 1 / schema 1 keeps working in every YKSPlayer 2.x release.
- Deprecations are announced in [CHANGELOG.md](CHANGELOG.md) at least one minor release before removal.
- Each plugin states the player versions it supports in `engines.yksplayer` (a SemVer range such as `>=2.0.0 <3.0.0`);
  the player hides and refuses plugins outside that range.

## Plugin versions

Plugins use SemVer for their own `version`. An update must be signed with the same key as the installed version.
An update that asks for new permissions is installed disabled until the user approves them again.
