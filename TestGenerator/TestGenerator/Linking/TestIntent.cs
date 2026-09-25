using Microsoft.CodeAnalysis;

namespace TestGenerator.Linking;

public enum MatchStatus
{
    Confirmed,
    Conflict,
    AnalysisOnly,
    CodeOnly,
    Ambiguous
}

public enum IntentKind
{
    TypeExists,
    MemberExists,
    StringConstraint
}

public sealed class TestIntent
{
    public TestIntent(string id, IntentKind kind, MatchStatus status, string message, Location location)
    {
        Id = id;
        Kind = kind;
        Status = status;
        Message = message;
        Location = location;
    }

    public string Id { get; }
    public IntentKind Kind { get; }
    public MatchStatus Status { get; }
    public string Message { get; }
    public Location Location { get; }
    public string? AnalysisClass { get; set; }
    public string? AnalysisAttribute { get; set; }
    public string? CodeType { get; set; }
    public string? CodeProperty { get; set; }
    public TestGenerator.Code.CodeType? Target { get; set; }
    public bool? Required { get; set; }
    public int? MaxLength { get; set; }
}
