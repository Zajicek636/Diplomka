using System.Collections.Generic;

namespace TestGenerator.Model;

public sealed class AnalysisModel
{
    public List<AnalysisClass> Classes { get; set; } = new();
    public List<AnalysisEnumeration> Enumerations { get; set; } = new();
}

public sealed class AnalysisClass
{
    public string Name { get; set; } = "";
    public string? Stereotype { get; set; }
    public string? Namespace { get; set; }
    public bool External { get; set; }
    public List<AnalysisAttribute> Attributes { get; set; } = new();
    public List<AnalysisConstraint> Constraints { get; set; } = new();
}

public sealed class AnalysisAttribute
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public int? Length { get; set; }
    public string? Multiplicity { get; set; }
    public bool Derived { get; set; }

    public bool IsRequired => Multiplicity == null || !Multiplicity.StartsWith("0");
    public bool IsString => string.Equals(Type, "string", System.StringComparison.OrdinalIgnoreCase);
}

public sealed class AnalysisConstraint
{
    public string Raw { get; set; } = "";
    public string? Kind { get; set; }
}

public sealed class AnalysisEnumeration
{
    public string Name { get; set; } = "";
    public string? Namespace { get; set; }
    public bool External { get; set; }
}
