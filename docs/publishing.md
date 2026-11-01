# Publishing (GitHub store)

YKSPlayer's store has no server: it lists GitHub repositories that carry the topic **`yksplayer-plugin`**, shows their
README and stars, and installs the `.ykspkg` attached to the latest release. Users can also drop a `.ykspkg` onto the
player window, so a release is all you need either way.

## 1. Repository

```
my-plugin/
  yks-plugin.json      # at the top: the store reads it to show the kind and permissions before installing
  README.md            # the store page (images with relative paths work; put screenshots in the repo)
  LICENSE
  src/...              # your code
```

- `yks-plugin.json`: set `repository` to the repository URL — the player uses it to recognise the installed plugin
  and offer updates.
- Write the README for users: what it does, a screenshot, which permissions it asks for and why.

## 2. Topics (GitHub → your repository → ⚙ next to "About")

| Topic | Effect |
|---|---|
| `yksplayer-plugin` | **required** — the store only lists repositories with it |
| `yksplayer-decoder`, `yksplayer-effect`, `yksplayer-visualizer`, `yksplayer-extension`, `yksplayer-theme` | the kind tab it appears under |

Fill in the description too: it is the text on the store card.

## 3. Signing key (once)

```powershell
yks-pack keygen myname          # -> myname.key.pem (secret) and its key id
```

Keep `myname.key.pem` safe (a password manager, or a CI secret — see 5). Updates must be signed with the same key;
the player refuses an update signed with another one. Put the key id in your README so users can check it.

## 4. Release

```powershell
yks-pack pack ./build --key myname.key.pem      # -> <id>-<version>.ykspkg
```

On GitHub: **Releases → Draft a new release → tag `v1.2.0`** (the same version as `yks-plugin.json`) → attach the
`.ykspkg` → **Publish release**. Drafts and pre-releases are not offered to users.

The store shows the newest release that has a `.ykspkg`; users who have an older version see **更新** (update).

## 5. Automatic releases (optional)

Store the key as a repository secret (**Settings → Secrets and variables → Actions → New repository secret**, name
`YKS_SIGNING_KEY`, value: the contents of `myname.key.pem`) and add `.github/workflows/release.yml`:

```yaml
name: release
on:
  push:
    tags: ['v*']
permissions:
  contents: write
jobs:
  release:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x' }
      - name: Build
        run: dotnet build -c Release -o build        # or your CMake build for native plugins
      - name: Pack and sign
        shell: pwsh
        env: { YKS_SIGNING_KEY: '${{ secrets.YKS_SIGNING_KEY }}' }
        run: |
          Set-Content key.pem $env:YKS_SIGNING_KEY
          dotnet tool run yks-pack pack build --key key.pem   # or the yks-pack.exe from the SDK release
          Remove-Item key.pem
      - name: Publish
        env: { GH_TOKEN: '${{ github.token }}' }
        run: gh release create ${{ github.ref_name }} (Get-ChildItem *.ykspkg).FullName --generate-notes
        shell: pwsh
```

Then releasing is: bump the version in `yks-plugin.json`, commit, `git tag v1.2.0`, `git push --tags`.

## Notes

- Stars come from GitHub; ask your users to star the repository if they like the plugin.
- Plugins installed from the store are **Community** plugins: users see your permissions and a warning before
  enabling. Ask only for the permissions you need.
- Anonymous GitHub API use is limited (about 60 requests an hour per user). The player caches everything it reads,
  so browsing costs little; keep README images reasonably small.
