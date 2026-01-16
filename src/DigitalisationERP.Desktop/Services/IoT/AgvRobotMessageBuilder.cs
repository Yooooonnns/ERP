using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DigitalisationERP.Desktop.Services.IoT;

public sealed class AgvRobotMessageBuilder
{
    private readonly IReadOnlyList<string> _route;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public AgvRobotMessageBuilder(IReadOnlyList<string> route)
    {
        _route = route ?? throw new ArgumentNullException(nameof(route));
    }

    public int? GetPostIndex1Based(string postCode)
    {
        if (string.IsNullOrWhiteSpace(postCode)) return null;
        var idx = _route.ToList().FindIndex(p => p.Equals(postCode, StringComparison.OrdinalIgnoreCase));
        return idx >= 0 ? idx + 1 : null;
    }

    public string BuildJson(
        int rawMaterialNeeded,
        IDictionary<int, string>? messagesByPostIndex1Based = null,
        string? command = null,
        string? orderNumber = null)
    {
        var payload = new AgvRobotMessage
        {
            RawMaterialNeeded = rawMaterialNeeded != 0 ? 1 : 0,
            Command = command,
            OrderNumber = orderNumber
        };

        if (messagesByPostIndex1Based != null)
        {
            foreach (var (postIndex, message) in messagesByPostIndex1Based)
            {
                if (postIndex <= 0) continue;
                if (string.IsNullOrWhiteSpace(message)) continue;
                payload.MessagesByPost[postIndex.ToString()] = message;
            }
        }

        return JsonSerializer.Serialize(payload, _options);
    }
}
