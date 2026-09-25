using System.Text.Json;
using TestGenerator.Model;

namespace TestGenerator.Analysis;

public static class AnalysisJsonReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static AnalysisModel Read(string json)
    {
        return JsonSerializer.Deserialize<AnalysisModel>(json, Options) ?? new AnalysisModel();
    }
}
