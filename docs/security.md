# Security model

YKSPlayer treats every plugin as untrusted code.

## Isolation

| Kind | Where it runs | Limits |
|---|---|---|
| decoder, dsp | `yks-plugin-host.exe`, one process per instance | Low integrity level; job object (killed with the player, cannot spawn processes, 512 MB memory cap, no desktop/clipboard/system-setting changes); call timeouts |
| extension | Its own process (the player's extension host or your `.exe`) | Same sandbox; JSON-RPC only; every incoming call checked against the granted permissions |
| visualizer | GPU (Direct2D pixel shader) | No CPU code at all |

At low integrity a process cannot write to the user's files, the registry outside its low hive, or other processes.
Decoders never open files: they get the bytes from the player.

Network access cannot be blocked per process on Windows without administrator rights, so `net` is a *declared*
permission: shown to the user before enabling — not technically enforced.

## Crash handling

A crash or timeout ends only that instance. The player counts failures per plugin and disables a plugin after three,
telling the user why.

## Packages and signatures

A `.ykspkg` is a zip with:

- `yks-plugin.json` — including `files`: the SHA-256 of every other file,
- `yks-plugin.sig` — an ECDSA P-256 / SHA-256 signature over the exact bytes of `yks-plugin.json`, with the public key.

On install the player verifies the signature, then every file hash, and rejects files that are not listed, missing
files, paths that escape the plugin folder and oversized entries.

## Trust levels

| Level | Meaning |
|---|---|
| Official | Ships with YKSPlayer or is signed with YKSPlayer's key |
| Community | Installed by the user, signed with the publisher's own key — the player warns before enabling |

An update must be signed with the same key as the installed version; otherwise it is refused (no takeover of a
plugin id by someone else).

## Reporting a vulnerability

Please report privately via the repository's security advisories rather than in a public issue.
