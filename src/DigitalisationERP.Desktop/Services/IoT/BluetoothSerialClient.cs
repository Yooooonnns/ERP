using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalisationERP.Desktop.Services.IoT;

public sealed class BluetoothSerialClient : IDisposable
{
    private readonly string _portName;
    private readonly int _baudRate;
    private readonly string _newLine;

    public string PortName => _portName;
    public int BaudRate => _baudRate;
    public string NewLine => _newLine;

    private SerialPort? _serialPort;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private readonly object _readSync = new();
    private CancellationTokenSource? _readCts;
    private Task? _readTask;

    public event EventHandler<string>? LineReceived;

    public BluetoothSerialClient(string portName, int baudRate, string newLine)
    {
        _portName = portName;
        _baudRate = baudRate;
        _newLine = string.IsNullOrEmpty(newLine) ? "\n" : newLine;
    }

    public bool IsOpen => _serialPort?.IsOpen == true;

    public Task EnsureOpenAsync()
    {
        if (IsOpen) return Task.CompletedTask;

        try
        {
            _serialPort?.Dispose();
        }
        catch
        {
            // ignore
        }

        var sp = new SerialPort(_portName, _baudRate)
        {
            Encoding = Encoding.UTF8,
            NewLine = _newLine,
            ReadTimeout = 1000,
            WriteTimeout = 1000,
            DtrEnable = true,
            RtsEnable = true
        };

        sp.Open();
        _serialPort = sp;
        return Task.CompletedTask;
    }

    public async Task StartListeningAsync(CancellationToken cancellationToken = default)
    {
        await EnsureOpenAsync().ConfigureAwait(false);

        lock (_readSync)
        {
            if (_readTask != null && !_readTask.IsCompleted)
            {
                return;
            }

            _readCts?.Cancel();
            _readCts?.Dispose();
            _readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _readCts.Token;

            _readTask = Task.Run(() => ReadLoop(token), token);
        }
    }

    private void ReadLoop(CancellationToken token)
    {
        // SerialPort.ReadLine is blocking; we use ReadTimeout so we can observe cancellation.
        while (!token.IsCancellationRequested)
        {
            try
            {
                var sp = _serialPort;
                if (sp == null || !sp.IsOpen)
                {
                    Thread.Sleep(50);
                    continue;
                }

                string? line = null;
                try
                {
                    line = sp.ReadLine();
                }
                catch (TimeoutException)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                LineReceived?.Invoke(this, line.Trim());
            }
            catch
            {
                // Swallow all read-loop exceptions; consumers should surface status in provider logs.
                Thread.Sleep(100);
            }
        }
    }

    public async Task SendLineAsync(string line, CancellationToken cancellationToken = default)
    {
        if (line == null) throw new ArgumentNullException(nameof(line));

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureOpenAsync().ConfigureAwait(false);
            _serialPort!.WriteLine(line);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Dispose()
    {
        try
        {
            lock (_readSync)
            {
                _readCts?.Cancel();
            }
        }
        catch
        {
            // ignore
        }

        try
        {
            _serialPort?.Dispose();
        }
        catch
        {
            // ignore
        }

        try
        {
            lock (_readSync)
            {
                _readCts?.Dispose();
                _readCts = null;
            }
        }
        catch
        {
            // ignore
        }

        _sendLock.Dispose();
    }
}
