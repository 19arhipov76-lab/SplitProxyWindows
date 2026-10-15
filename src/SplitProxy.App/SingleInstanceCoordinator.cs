using System.IO.Pipes;
using System.Text;

namespace SplitProxy.App;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private CancellationTokenSource? _cts;
    public bool IsPrimary { get; }
    public event EventHandler? ActivationRequested;

    public SingleInstanceCoordinator(string name)
    {
        _pipeName = name.Replace('.', '_');
        _mutex = new Mutex(true, @"Local\" + name, out var created);
        IsPrimary = created;
    }

    public void StartServer()
    {
        if (!IsPrimary) return;
        _cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(_cts.Token);
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    _ = await reader.ReadLineAsync(_cts.Token);
                    ActivationRequested?.Invoke(this, EventArgs.Empty);
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(250); }
            }
        });
    }

    public async Task SignalPrimaryAsync()
    {
        try
        {
            await using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(cts.Token);
            await using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            await writer.WriteLineAsync("SHOW");
        }
        catch { }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        if (IsPrimary) try { _mutex.ReleaseMutex(); } catch { }
        _mutex.Dispose();
        _cts?.Dispose();
    }
}
