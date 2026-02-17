using System;
using System.Threading;
using NDesk.DBus;

namespace Avalonia.DBus.Interop.Tests.Helpers;

/// <summary>
/// Runs NDesk's <c>Bus.Iterate()</c> on a dedicated background thread so that:
/// 1. Registered objects can respond to incoming method calls.
/// 2. Proxy method calls from other threads (including async continuations)
///    go through the <c>PendingCall</c> Monitor.Wait path and receive replies.
///
/// NDesk checks <c>Thread.CurrentThread == conn.mainThread</c> to decide
/// whether to read messages directly or wait on a monitor. By calling
/// <c>ClaimMainThread()</c> on the runner thread, all other threads safely
/// wait while this thread dispatches replies.
/// </summary>
public sealed class NdeskServerRunner : IDisposable
{
    private readonly Bus _bus;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _started = new();
    private volatile bool _stop;

    public NdeskServerRunner(Bus bus)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "NDesk.DBus Iterate"
        };
        _thread.Start();
        _started.Wait();
    }

    private void Run()
    {
        _bus.ClaimMainThread();
        _started.Set();

        while (!_stop)
        {
            try
            {
                _bus.Iterate();
            }
            catch when (_stop)
            {
                break;
            }
            catch
            {
                // Swallow transient errors while iterating; the test will
                // fail on its own assertion if the server can't respond.
            }
        }
    }

    public void Dispose()
    {
        _stop = true;
        _bus.CloseTransport();
        _thread.Join(TimeSpan.FromSeconds(5));
        _started.Dispose();
    }
}
