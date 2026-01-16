using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalisationERP.Desktop.Services.IoT;

public sealed class AgvRobotMessenger : IDisposable
{
    private readonly BluetoothSerialClient _client;
    private readonly AgvRobotMessageBuilder _builder;
    private readonly bool _ownsClient;

    // Simple state so we can keep mat_p sticky if needed.
    private int _rawMaterialNeeded;
    private readonly Dictionary<int, string> _messagesByPost = new();

    public AgvRobotMessenger(BluetoothSerialClient client, AgvRobotMessageBuilder builder, bool ownsClient = true)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _ownsClient = ownsClient;
    }

    public void SetRawMaterialNeeded(bool needed)
    {
        _rawMaterialNeeded = needed ? 1 : 0;
    }

    public void SetPostMessage(int postIndex1Based, string message)
    {
        if (postIndex1Based <= 0) return;
        if (string.IsNullOrWhiteSpace(message)) return;
        _messagesByPost[postIndex1Based] = message;
    }

    public void ClearPostMessage(int postIndex1Based)
    {
        _messagesByPost.Remove(postIndex1Based);
    }

    public Task SendAsync(string? command = null, string? orderNumber = null, CancellationToken cancellationToken = default)
    {
        var json = _builder.BuildJson(_rawMaterialNeeded, _messagesByPost, command: command, orderNumber: orderNumber);
        // User expects newline terminated payload.
        return _client.SendLineAsync(json, cancellationToken);
    }

    public Task SendRawAsync(string json, CancellationToken cancellationToken = default)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));
        return _client.SendLineAsync(json, cancellationToken);
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}
