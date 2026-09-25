using System;
using System.Collections.Generic;
using System.Linq;
using TestGenerator.Generation.Profiles;

namespace TestGenerator.Generation;

public static class ProfileRegistry
{
    private static readonly Dictionary<string, ITestProfile> Profiles = new(StringComparer.OrdinalIgnoreCase);

    static ProfileRegistry()
    {
        Register(new GenericXunitProfile());
        Register(new FakeEntityProfile());
    }

    public static void Register(ITestProfile profile) => Profiles[profile.Name] = profile;

    public static ITestProfile Get(string name) =>
        Profiles.TryGetValue(name, out var profile)
            ? profile
            : throw new ArgumentException($"Neznámý profil '{name}'. Dostupné: {string.Join(", ", Names)}.");

    public static IEnumerable<string> Names => Profiles.Keys.OrderBy(k => k);

    public static IEnumerable<ITestProfile> All => Profiles.Values;
}
