using System;
using System.Collections.Generic;

namespace Avalonia.DBus;

internal sealed class CompositeDisposable(IReadOnlyList<IDisposable> disposables) : IDisposable
{
    private readonly IReadOnlyList<IDisposable> _disposables = disposables ?? throw new ArgumentNullException(nameof(disposables));
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        for (var i = 0; i < _disposables.Count; i++)
            _disposables[i].Dispose();
    }
}
