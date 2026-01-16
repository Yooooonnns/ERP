using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DigitalisationERP.Desktop.Services.IoT;

public sealed class AgvRobotMessage
{
    // Raw material needed (1) or not (0)
    [JsonPropertyName("mat_p")]
    public int RawMaterialNeeded { get; set; }

    // Per-post messages. Keys must be "1", "2", "3"...
    [JsonPropertyName("mant")]
    public Dictionary<string, string> MessagesByPost { get; set; } = new();

    // Optional command channel (kept optional so we can still produce the exact legacy payload).
    [JsonPropertyName("cmd")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Command { get; set; }

    [JsonPropertyName("of")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OrderNumber { get; set; }
}
