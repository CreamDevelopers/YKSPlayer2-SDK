using System.Globalization;

namespace YKSPlayer.Sdk;

/// <summary>Semantic version (major.minor.patch[-prerelease]); build metadata is ignored.</summary>
public readonly record struct SemVersion(int Major, int Minor, int Patch, string Prerelease = "") : IComparable<SemVersion>
{
    public static bool TryParse(string? s, out SemVersion v)
    {
        v = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim().TrimStart('v');
        var plus = s.IndexOf('+');
        if (plus >= 0) s = s[..plus];
        var dash = s.IndexOf('-');
        var pre = dash >= 0 ? s[(dash + 1)..] : "";
        var core = (dash >= 0 ? s[..dash] : s).Split('.');
        if (core.Length != 3) return false;
        if (!int.TryParse(core[0], NumberStyles.None, CultureInfo.InvariantCulture, out var a) ||
            !int.TryParse(core[1], NumberStyles.None, CultureInfo.InvariantCulture, out var b) ||
            !int.TryParse(core[2], NumberStyles.None, CultureInfo.InvariantCulture, out var c)) return false;
        v = new SemVersion(a, b, c, pre);
        return true;
    }

    public static SemVersion Parse(string s) => TryParse(s, out var v) ? v : throw new FormatException($"'{s}' is not a SemVer version");

    public int CompareTo(SemVersion o)
    {
        var c = Major.CompareTo(o.Major);
        if (c == 0) c = Minor.CompareTo(o.Minor);
        if (c == 0) c = Patch.CompareTo(o.Patch);
        if (c != 0) return c;
        if (Prerelease == o.Prerelease) return 0;
        if (Prerelease.Length == 0) return 1;  // a release sorts after its prereleases
        if (o.Prerelease.Length == 0) return -1;
        return string.CompareOrdinal(Prerelease, o.Prerelease);
    }

    public static bool operator <(SemVersion a, SemVersion b) => a.CompareTo(b) < 0;
    public static bool operator >(SemVersion a, SemVersion b) => a.CompareTo(b) > 0;
    public static bool operator <=(SemVersion a, SemVersion b) => a.CompareTo(b) <= 0;
    public static bool operator >=(SemVersion a, SemVersion b) => a.CompareTo(b) >= 0;

    public override string ToString() => Prerelease.Length > 0 ? $"{Major}.{Minor}.{Patch}-{Prerelease}" : $"{Major}.{Minor}.{Patch}";
}

/// <summary>Space-separated comparators, all of which must hold: ">=2.0.0 &lt;3.0.0", "=1.2.3", "*".</summary>
public sealed class VersionRange
{
    private readonly List<(string Op, SemVersion V)> _parts;

    private VersionRange(List<(string, SemVersion)> parts) => _parts = parts;

    public static bool TryParse(string? s, out VersionRange range)
    {
        range = new VersionRange([]);
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (s.Trim() == "*") return true;
        var parts = new List<(string, SemVersion)>();
        foreach (var tok in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var op = tok.StartsWith(">=") || tok.StartsWith("<=") ? tok[..2] : tok[0] is '>' or '<' or '=' ? tok[..1] : "=";
            var rest = tok[(op == "=" && tok[0] != '=' ? 0 : op.Length)..];
            if (!SemVersion.TryParse(rest, out var v)) return false;
            parts.Add((op, v));
        }
        range = new VersionRange(parts);
        return parts.Count > 0;
    }

    public bool Contains(SemVersion v) => _parts.All(p => p.Op switch
    {
        ">=" => v >= p.V,
        "<=" => v <= p.V,
        ">" => v > p.V,
        "<" => v < p.V,
        _ => v.CompareTo(p.V) == 0,
    });
}
