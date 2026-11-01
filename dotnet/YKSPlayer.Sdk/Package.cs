using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YKSPlayer.Sdk;

/// <summary>yks-plugin.sig: the signature over the exact bytes of yks-plugin.json.</summary>
public sealed class PackageSignature
{
    /// <summary>"ecdsa-p256-sha256"; the signature is IEEE P1363 (r||s, 64 bytes).</summary>
    [JsonPropertyName("algorithm")] public string Algorithm { get; set; } = "ecdsa-p256-sha256";
    /// <summary>First 16 hex digits of SHA-256(public key, SubjectPublicKeyInfo DER).</summary>
    [JsonPropertyName("keyId")] public string KeyId { get; set; } = "";
    /// <summary>Base64 SubjectPublicKeyInfo DER.</summary>
    [JsonPropertyName("publicKey")] public string PublicKey { get; set; } = "";
    [JsonPropertyName("signature")] public string Signature { get; set; } = "";
}

public sealed record VerifiedPackage(PluginManifest Manifest, PackageSignature Signature, string ManifestJson);

/// <summary>Creating, signing and verifying .ykspkg packages (zip files).</summary>
public static class PluginPackage
{
    public const string Extension = ".ykspkg";
    private const long MaxEntryBytes = 512L * 1024 * 1024;

    /// <summary>A new P-256 key pair: (PKCS#8 PEM private key, base64 SPKI public key, key id).</summary>
    public static (string PrivatePem, string PublicKey, string KeyId) GenerateKey()
    {
        using var k = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pub = k.ExportSubjectPublicKeyInfo();
        return (k.ExportPkcs8PrivateKeyPem(), Convert.ToBase64String(pub), KeyId(pub));
    }

    public static string KeyId(byte[] spki) => Convert.ToHexStringLower(SHA256.HashData(spki))[..16];

    /// <summary>
    /// Validates <paramref name="dir"/> (which contains yks-plugin.json and the plugin's files), records the hash of
    /// every file in the manifest, signs it with <paramref name="privatePem"/> and writes the package.
    /// </summary>
    public static PluginManifest Pack(string dir, string privatePem, string outFile)
    {
        var manifestPath = Path.Combine(dir, PluginManifest.FileName);
        var manifest = PluginManifest.Parse(File.ReadAllText(manifestPath));
        manifest.Files.Clear();
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(dir, f).Replace('\\', '/');
            if (rel is PluginManifest.FileName or PluginManifest.SignatureFileName) continue;
            using var s = File.OpenRead(f);
            manifest.Files[rel] = Convert.ToHexStringLower(SHA256.HashData(s));
        }
        var errors = manifest.Validate().ToList();
        if (!manifest.Files.ContainsKey(manifest.Entry)) errors.Add($"entry '{manifest.Entry}' is not in the folder");
        if (errors.Count > 0) throw new InvalidDataException("Invalid plugin:\n  " + string.Join("\n  ", errors));

        var json = manifest.ToJson();
        var bytes = Encoding.UTF8.GetBytes(json);
        using var key = ECDsa.Create();
        key.ImportFromPem(privatePem);
        var spki = key.ExportSubjectPublicKeyInfo();
        var sig = new PackageSignature
        {
            KeyId = KeyId(spki),
            PublicKey = Convert.ToBase64String(spki),
            Signature = Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)),
        };

        var tmp = outFile + ".tmp";
        using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            Write(zip, PluginManifest.FileName, bytes);
            Write(zip, PluginManifest.SignatureFileName, JsonSerializer.SerializeToUtf8Bytes(sig, PluginManifest.JsonOptions));
            foreach (var rel in manifest.Files.Keys) zip.CreateEntryFromFile(Path.Combine(dir, rel), rel, CompressionLevel.Optimal);
        }
        File.Move(tmp, outFile, true);
        return manifest;
    }

    /// <summary>
    /// Checks the signature and every file hash, without extracting. Throws <see cref="InvalidDataException"/> with a
    /// readable reason when anything does not match.
    /// </summary>
    public static VerifiedPackage Verify(string package)
    {
        using var zip = ZipFile.OpenRead(package);
        var manifestEntry = zip.GetEntry(PluginManifest.FileName) ?? throw new InvalidDataException("yks-plugin.json is missing");
        var sigEntry = zip.GetEntry(PluginManifest.SignatureFileName) ?? throw new InvalidDataException("The package is not signed (yks-plugin.sig is missing)");
        var manifestBytes = ReadAll(manifestEntry);
        var sig = JsonSerializer.Deserialize<PackageSignature>(ReadAll(sigEntry), PluginManifest.JsonOptions)
                  ?? throw new InvalidDataException("yks-plugin.sig is empty");
        if (sig.Algorithm != "ecdsa-p256-sha256") throw new InvalidDataException($"Unsupported signature algorithm '{sig.Algorithm}'");
        byte[] spki;
        try
        {
            spki = Convert.FromBase64String(sig.PublicKey);
        }
        catch (FormatException)
        {
            throw new InvalidDataException("The public key is not valid base64");
        }
        if (KeyId(spki) != sig.KeyId) throw new InvalidDataException("The key id does not match the public key");
        using (var key = ECDsa.Create())
        {
            key.ImportSubjectPublicKeyInfo(spki, out _);
            if (!key.VerifyData(manifestBytes, Convert.FromBase64String(sig.Signature), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                throw new InvalidDataException("The signature does not match: the manifest was changed after signing");
        }

        var json = Encoding.UTF8.GetString(manifestBytes);
        var manifest = PluginManifest.Parse(json);
        var errors = manifest.Validate();
        if (errors.Count > 0) throw new InvalidDataException("Invalid manifest: " + string.Join("; ", errors));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in zip.Entries)
        {
            if (e.FullName.EndsWith('/')) continue;  // directory entry
            if (e.FullName is PluginManifest.FileName or PluginManifest.SignatureFileName) continue;
            if (!manifest.Files.TryGetValue(e.FullName, out var want)) throw new InvalidDataException($"'{e.FullName}' is in the package but not in the signed file list");
            if (e.Length > MaxEntryBytes) throw new InvalidDataException($"'{e.FullName}' is too large");
            using var s = e.Open();
            if (Convert.ToHexStringLower(SHA256.HashData(s)) != want) throw new InvalidDataException($"'{e.FullName}' was modified (hash mismatch)");
            seen.Add(e.FullName);
        }
        var missing = manifest.Files.Keys.Where(k => !seen.Contains(k)).ToList();
        if (missing.Count > 0) throw new InvalidDataException("Files listed in the manifest are missing: " + string.Join(", ", missing));
        if (!manifest.Files.ContainsKey(manifest.Entry)) throw new InvalidDataException($"The entry '{manifest.Entry}' is missing");
        return new VerifiedPackage(manifest, sig, json);
    }

    /// <summary>Verifies, then extracts into <paramref name="dir"/> (which must not exist yet).</summary>
    public static VerifiedPackage Extract(string package, string dir)
    {
        var v = Verify(package);
        Directory.CreateDirectory(dir);
        var root = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
        using var zip = ZipFile.OpenRead(package);
        foreach (var e in zip.Entries.Where(e => !e.FullName.EndsWith('/')))
        {
            var target = Path.GetFullPath(Path.Combine(dir, e.FullName));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"'{e.FullName}' points outside the plugin folder");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            e.ExtractToFile(target, false);
        }
        return v;
    }

    private static void Write(ZipArchive zip, string name, byte[] bytes)
    {
        using var s = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        s.Write(bytes);
    }

    private static byte[] ReadAll(ZipArchiveEntry e)
    {
        if (e.Length > 1024 * 1024) throw new InvalidDataException($"{e.FullName} is too large");
        using var s = e.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}
