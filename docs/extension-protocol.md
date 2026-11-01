# Extension protocol (API version 1)

Extensions add panels, commands and lyrics. They run in their own process at low integrity and talk to the player
over **JSON-RPC 2.0, one JSON message per line, UTF-8, on stdin/stdout**. stderr is free for logging.

- `.NET`: derive from `YKSPlayer.Sdk.Extensions.Extension`; the player's extension host loads your assembly
  (`"extension": { "runtime": "dotnet" }`). The SDK must not be copied into your package (it is provided).
- Any other language: ship an `.exe` (`"runtime": "exe"`) that speaks the protocol below.

## Player → extension

| Method | Kind | Params | Result |
|---|---|---|---|
| `initialize` | request | `{apiVersion: 1, appVersion, permissions: [granted]}` | `{commands: [{id, title, glyph?}], panels: [{id, title}], providers: ["lyrics"]}` |
| `playback/trackChanged` | notification | `{title, artist, album, durationSeconds, path?}` (`path` only with `library.read`) | — |
| `playback/stateChanged` | notification | `{state: "playing" \| "paused" \| "stopped"}` | — |
| `command/execute` | request | `{id}` | message to show, or `null` |
| `panel/render` | request | `{id}` | a Panel (below) or `null` |
| `lyrics/get` | request | `{title, artist, album, durationSeconds}` | `{lrc}` or `null` |
| `lyrics/pronounce` | request | `{lines, script, title, artist}` (SDK 1.2; `script` is `"ja-Kana"`; provider `pronunciation`, needs `lyrics.pronounce`) | `{lines}`: how each line sounds, same order, `""` for none; or `null` |
| `lyrics/translate` | request | `{lines, target, title, artist}` (SDK 1.2; provider `translation`, needs `lyrics.translate`) | `{lines}`: one per line, same order, `""` for none; or `null` |
| `shutdown` | request | — | `null`; then exit |

Requests time out after 5 s (lyrics/get: 8 s, lyrics/translate and lyrics/pronounce: 30 s). Track and state notifications need `playback.read`.

## Extension → player

| Method | Kind | Params | Needs |
|---|---|---|---|
| `panel/invalidate` | notification | `{id}` | `ui.panel` |
| `ui/notify` | notification | `{message}` (≤ 200 chars) | `ui.panel` or `ui.command` |
| `playback/control` | notification | `{action: "play" \| "pause" \| "next" \| "previous"}` | `playback.control` |
| `log` | notification | `{message}` | — |

Calls without the permission are ignored and logged.

## Panels

Extensions describe content; the player draws it with its own controls and colours (so panels always match the
current theme and cannot imitate the player's UI).

```json
{
  "title": "今日のリスニング日記",
  "blocks": [
    { "type": "heading", "text": "12 曲 · 約 48 分" },
    { "type": "text", "text": "Any paragraph." },
    { "type": "keyValue", "key": "はじめた時刻", "value": "19:02" },
    { "type": "progress", "text": "今日の目標 60 分", "progress": 0.8 },
    { "type": "list", "items": ["19:40  Song — Artist", "19:36  …"] },
    { "type": "button", "text": "新しいページ", "command": "new-page" }
  ]
}
```

Limits: 40 blocks, 20 list items per list. Panels appear as widgets on the home page.

## Example session

```
→ {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"apiVersion":1,"appVersion":"2.0.0","permissions":["playback.read","ui.panel"]}}
← {"jsonrpc":"2.0","id":1,"result":{"commands":[],"panels":[{"id":"today","title":"Diary"}],"providers":[]}}
→ {"jsonrpc":"2.0","method":"playback/trackChanged","params":{"title":"夕焼け","artist":"YKS","album":"","durationSeconds":214}}
← {"jsonrpc":"2.0","method":"panel/invalidate","params":{"id":"today"}}
→ {"jsonrpc":"2.0","id":2,"method":"panel/render","params":{"id":"today"}}
← {"jsonrpc":"2.0","id":2,"result":{"title":"Diary","blocks":[{"type":"heading","text":"1 曲"}]}}
```
