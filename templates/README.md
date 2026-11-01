# Project templates

| Short name      | What you get                              | Language |
| --------------- | ----------------------------------------- | -------- |
| `yks-effect`    | DSP effect with one `gain` parameter      | C++      |
| `yks-decoder`   | Decoder skeleton for a new file extension | C        |
| `yks-extension` | Extension with one panel                  | C#       |

```bash
dotnet new install ./templates/native-effect
dotnet new install ./templates/native-decoder
dotnet new install ./templates/extension

dotnet new yks-effect -n Warmth --publisher alice
dotnet new yks-decoder -n Tracker --publisher alice --extension .trk
dotnet new yks-extension -n Scrobbler --publisher alice
```

`--publisher` becomes the first half of the plugin id (`alice.warmth`). Ids must be lower case.
Edit `id` in `yks-plugin.json` if your project name has capitals.

## Building

Native templates find the SDK headers through `YKS_SDK`, which defaults to this checkout
(`../..`). Outside the SDK tree, pass it explicitly:

```bash
cmake -S Warmth -B build -A x64 -DYKS_SDK=C:/path/to/yksplayer-sdk
cmake --build build --config Release
```

The extension template references the `YKSPlayer.Sdk` NuGet package. Until it is on nuget.org,
swap the `PackageReference` for a `ProjectReference` to `dotnet/YKSPlayer.Sdk` with
`ExcludeAssets="runtime"` (see `samples/extension-diary`).

Then validate and pack (see [publishing](../docs/publishing.md)):

```bash
yks-pack validate build/Release
yks-pack pack build/Release --key my.key
```
