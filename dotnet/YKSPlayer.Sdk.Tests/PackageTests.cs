using System.IO.Compression;
using YKSPlayer.Sdk;

namespace YKSPlayer.Sdk.Tests;

public sealed class PackageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "yks-sdk-" + Guid.NewGuid().ToString("N"));

    public PackageTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, true);

    private string PluginFolder()
    {
        var d = Path.Combine(_dir, "src");
        Directory.CreateDirectory(Path.Combine(d, "lib"));
        File.WriteAllText(Path.Combine(d, PluginManifest.FileName), """
            {
              "schema": 1, "id": "test.tone", "name": "Tone", "version": "1.2.0", "publisher": "Me", "license": "MIT",
              "kind": "decoder", "entry": "tone.dll", "engines": { "yksplayer": ">=2.0.0 <3.0.0", "pluginAbi": 1 },
              "permissions": ["audio.decode"], "decoder": { "extensions": [".ykstone"] }
            }
            """);
        File.WriteAllBytes(Path.Combine(d, "tone.dll"), [0x4D, 0x5A, 1, 2, 3]);
        File.WriteAllText(Path.Combine(d, "lib", "helper.txt"), "hello");
        return d;
    }

    [Fact]
    public void PackThenVerifyRoundTrips()
    {
        var (pem, pub, id) = PluginPackage.GenerateKey();
        var pkg = Path.Combine(_dir, "t.ykspkg");
        var m = PluginPackage.Pack(PluginFolder(), pem, pkg);
        Assert.Equal(2, m.Files.Count);
        Assert.True(m.Files.ContainsKey("lib/helper.txt"));
        var v = PluginPackage.Verify(pkg);
        Assert.Equal("test.tone", v.Manifest.Id);
        Assert.Equal(pub, v.Signature.PublicKey);
        Assert.Equal(id, v.Signature.KeyId);
        var outDir = Path.Combine(_dir, "installed");
        PluginPackage.Extract(pkg, outDir);
        Assert.Equal("hello", File.ReadAllText(Path.Combine(outDir, "lib", "helper.txt")));
    }

    [Fact]
    public void TamperedFileIsRejected()
    {
        var (pem, _, _) = PluginPackage.GenerateKey();
        var pkg = Path.Combine(_dir, "t.ykspkg");
        PluginPackage.Pack(PluginFolder(), pem, pkg);
        using (var zip = ZipFile.Open(pkg, ZipArchiveMode.Update))
        {
            zip.GetEntry("tone.dll")!.Delete();
            using var s = zip.CreateEntry("tone.dll").Open();
            s.Write([0x4D, 0x5A, 9, 9, 9]);
        }
        var ex = Assert.Throws<InvalidDataException>(() => PluginPackage.Verify(pkg));
        Assert.Contains("modified", ex.Message);
    }

    [Fact]
    public void TamperedManifestAndExtraFilesAreRejected()
    {
        var (pem, _, _) = PluginPackage.GenerateKey();
        var pkg = Path.Combine(_dir, "t.ykspkg");
        PluginPackage.Pack(PluginFolder(), pem, pkg);
        var copy = Path.Combine(_dir, "extra.ykspkg");
        File.Copy(pkg, copy);
        using (var zip = ZipFile.Open(copy, ZipArchiveMode.Update))
        {
            using var s = zip.CreateEntry("evil.dll").Open();
            s.Write([1]);
        }
        Assert.Contains("not in the signed file list", Assert.Throws<InvalidDataException>(() => PluginPackage.Verify(copy)).Message);

        using (var zip = ZipFile.Open(pkg, ZipArchiveMode.Update))
        {
            var e = zip.GetEntry(PluginManifest.FileName)!;
            string json;
            using (var r = new StreamReader(e.Open())) json = r.ReadToEnd();
            e.Delete();
            using var w = new StreamWriter(zip.CreateEntry(PluginManifest.FileName).Open());
            w.Write(json.Replace("audio.decode", "net"));  // sneak in a permission
        }
        Assert.Contains("signature does not match", Assert.Throws<InvalidDataException>(() => PluginPackage.Verify(pkg)).Message);
    }

    [Fact]
    public void ManifestValidationCatchesMistakes()
    {
        var m = PluginManifest.Parse("""{"schema":1,"id":"Bad Id","name":"","version":"1.0","publisher":"","kind":"thing","entry":"../x.dll","permissions":["root"]}""");
        var errors = m.Validate();
        Assert.Contains(errors, e => e.StartsWith("id "));
        Assert.Contains(errors, e => e.StartsWith("version "));
        Assert.Contains(errors, e => e.StartsWith("kind "));
        Assert.Contains(errors, e => e.StartsWith("entry "));
        Assert.Contains(errors, e => e.Contains("unknown permission 'root'"));
    }

    [Fact]
    public void SettingsAreValidated()
    {
        var m = PluginManifest.Parse("""
            {"schema":1,"id":"me.x","name":"X","version":"1.0.0","publisher":"me","kind":"extension","entry":"x.dll",
             "settings":[{"id":"a","type":"bool","label":"A"},{"id":"a","type":"slider","label":"B"},{"id":"c","type":"choice","label":"C"}]}
            """);
        var errors = m.Validate();
        Assert.Contains(errors, e => e.Contains("unique"));
        Assert.Contains(errors, e => e.Contains("type is bool"));
        Assert.Contains(errors, e => e.Contains("needs choices"));
    }

    [Fact]
    public void ThemesAreValidated()
    {
        Assert.Empty(ThemeDefinition.Parse("""{"accent":"#ff8a7a","blobs":["#112233","#445566"],"backgroundImage":"bg.jpg"}""").Validate());
        var bad = ThemeDefinition.Parse("""{"accent":"red","blobs":["#112233"],"backgroundImage":"../bg.gif","backgroundOpacity":2}""").Validate();
        Assert.Equal(4, bad.Count);
        var m = PluginManifest.Parse("""{"schema":1,"id":"me.t","name":"T","version":"1.0.0","publisher":"me","kind":"theme","entry":"theme.dll"}""");
        Assert.Contains(m.Validate(), e => e.Contains("theme JSON"));
    }

    [Fact]
    public void DrawListBuildsOperations()
    {
        var d = new YKSPlayer.Sdk.Extensions.DrawList().Rect(1, 2, 3, 4, "#ffffff").Circle(5, 6, 7, "#000000", 2).Poly([(0, 0), (1, 1)], "#123456");
        Assert.Equal(["rect", "circle", "poly"], d.Ops.Select(o => o.Op));
        Assert.Equal([0f, 0f, 1f, 1f], d.Ops[2].Points!);
        Assert.Equal("#ff880080", YKSPlayer.Sdk.Extensions.DrawList.WithAlpha("#ff8800", 0.5));
    }

    [Theory]
    [InlineData("icon.svg", true)]
    [InlineData("art/icon.png", true)]
    [InlineData("icon.exe", false)]
    [InlineData("../icon.svg", false)]
    public void IconMustBeAnImageInsideThePackage(string icon, bool valid)
    {
        var m = PluginManifest.Parse($$"""{"schema":1,"id":"me.x","name":"X","version":"1.0.0","publisher":"me","kind":"visualizer","entry":"x.cso","icon":"{{icon}}"}""");
        Assert.Equal(valid, !m.Validate().Any(e => e.StartsWith("icon ")));
    }

    [Theory]
    [InlineData(">=2.0.0 <3.0.0", "2.4.1", true)]
    [InlineData(">=2.0.0 <3.0.0", "3.0.0", false)]
    [InlineData(">=2.0.0 <3.0.0", "3.0.0-beta.1", true)]
    [InlineData("=1.2.3", "1.2.3", true)]
    [InlineData("1.2.3", "1.2.4", false)]
    [InlineData("*", "9.9.9", true)]
    public void VersionRanges(string range, string version, bool expected)
    {
        Assert.True(VersionRange.TryParse(range, out var r));
        Assert.Equal(expected, r.Contains(SemVersion.Parse(version)));
    }
}
