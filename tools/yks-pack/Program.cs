// yks-pack — validate, package and sign YKSPlayer plugins.
//
//   yks-pack keygen <name>                       writes <name>.key.pem (keep secret) and prints the public key
//   yks-pack validate <folder>                   checks yks-plugin.json
//   yks-pack pack <folder> --key <key.pem> [-o out.ykspkg]
//   yks-pack verify <file.ykspkg>                checks the signature and every file hash
using YKSPlayer.Sdk;

static int Usage()
{
    Console.Error.WriteLine("""
        yks-pack keygen <name>
        yks-pack validate <folder>
        yks-pack pack <folder> --key <key.pem> [-o <out.ykspkg>]
        yks-pack verify <file.ykspkg>
        """);
    return 2;
}

static string? Option(string[] a, params string[] names)
{
    for (var i = 0; i + 1 < a.Length; i++)
        if (names.Contains(a[i])) return a[i + 1];
    return null;
}

if (args.Length < 2) return Usage();
try
{
    switch (args[0])
    {
        case "keygen":
        {
            var (pem, pub, id) = PluginPackage.GenerateKey();
            var file = args[1] + ".key.pem";
            if (File.Exists(file)) throw new IOException($"{file} already exists");
            File.WriteAllText(file, pem);
            Console.WriteLine($"Private key: {Path.GetFullPath(file)}  (never commit or share it)");
            Console.WriteLine($"Key id:      {id}");
            Console.WriteLine($"Public key:  {pub}");
            return 0;
        }
        case "validate":
        {
            var m = PluginManifest.Parse(File.ReadAllText(Path.Combine(args[1], PluginManifest.FileName)));
            var errors = m.Validate().ToList();
            if (!File.Exists(Path.Combine(args[1], m.Entry))) errors.Add($"entry '{m.Entry}' does not exist");
            if (m.Kind == PluginKinds.Theme && errors.Count == 0)
            {
                try
                {
                    var theme = ThemeDefinition.Parse(File.ReadAllText(Path.Combine(args[1], m.Entry)));
                    errors.AddRange(theme.Validate());
                    if (theme.BackgroundImage is { } bg && !File.Exists(Path.Combine(args[1], bg))) errors.Add($"backgroundImage '{bg}' does not exist");
                }
                catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException)
                {
                    errors.Add("theme file: " + ex.Message);
                }
            }
            if (m.Icon is { } icon && errors.Count == 0)
            {
                var iconFile = new FileInfo(Path.Combine(args[1], icon));
                if (!iconFile.Exists) errors.Add($"icon '{icon}' does not exist");
                else if (iconFile.Length > PluginManifest.MaxIconBytes) errors.Add($"icon is larger than {PluginManifest.MaxIconBytes / 1024} KB");
            }
            foreach (var e in errors) Console.Error.WriteLine("  ✗ " + e);
            Console.WriteLine(errors.Count == 0 ? $"✓ {m.Id} {m.Version} ({m.Kind}) is valid" : $"{errors.Count} problem(s)");
            return errors.Count == 0 ? 0 : 1;
        }
        case "pack":
        {
            var key = Option(args, "--key", "-k") ?? throw new ArgumentException("--key is required");
            var dir = args[1];
            var probe = PluginManifest.Parse(File.ReadAllText(Path.Combine(dir, PluginManifest.FileName)));
            var output = Option(args, "-o", "--out") ?? $"{probe.Id}-{probe.Version}{PluginPackage.Extension}";
            var m = PluginPackage.Pack(dir, File.ReadAllText(key), output);
            Console.WriteLine($"✓ {Path.GetFullPath(output)}: {m.Id} {m.Version}, {m.Files.Count} file(s), signed");
            return 0;
        }
        case "verify":
        {
            var v = PluginPackage.Verify(args[1]);
            Console.WriteLine($"✓ {v.Manifest.Id} {v.Manifest.Version} ({v.Manifest.Kind}) — signature OK, key {v.Signature.KeyId}");
            foreach (var p in v.Manifest.Permissions) Console.WriteLine($"  permission: {p}");
            return 0;
        }
        default:
            return Usage();
    }
}
catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or FormatException or System.Text.Json.JsonException)
{
    Console.Error.WriteLine("✗ " + e.Message);
    return 1;
}
