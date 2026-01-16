namespace DigitalisationERP.Core.Abstractions.Communication;

/// <summary>
/// Interface for device communication (Bluetooth, IoT, etc.)
/// </summary>
public interface IDeviceCommunicationService
{
    /// <summary>
    /// Connect to a device
    /// </summary>
    Task<bool> ConnectAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect from a device
    /// </summary>
    Task DisconnectAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send data to a device
    /// </summary>
    Task<bool> SendDataAsync(string deviceId, byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Receive data from a device
    /// </summary>
    Task<byte[]?> ReceiveDataAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if device is connected
    /// </summary>
    bool IsConnected(string deviceId);

    /// <summary>
    /// Scan for available devices
    /// </summary>
    Task<IEnumerable<DeviceInfo>> ScanDevicesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Device information
/// </summary>
public record DeviceInfo(
    string DeviceId,
    string DeviceName,
    string DeviceType,
    int SignalStrength,
    bool IsAvailable);
