using System;
using System.Collections.Generic;
using System.Linq;

namespace Avalonia.DBus;

public sealed class DBusObjectTree
{
    private readonly object _gate = new();
    private readonly Dictionary<string, DBusObject> _objects = new(StringComparer.Ordinal);
    private readonly HashSet<string> _activePaths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DBusObject> _activeObjects = new(StringComparer.Ordinal);
    private readonly string _rootPath;
    private readonly string[] _rootSegments;

    public DBusObjectTree(string rootPath)
    {
        _rootPath = NormalizePath(rootPath);
        _rootSegments = SplitPath(_rootPath);
        Path = _rootPath;
        _objects[_rootPath] = new DBusObject(_rootPath);
    }

    public string Path { get; }

    public DBusObject AddPath(string path)
    {
        path = NormalizePath(path);
        EnsureUnderRoot(path);
        lock (_gate)
        {
            var obj = EnsureObject(path);
            if (_activePaths.Add(path))
            {
                RebuildActiveTree();
            }

            return obj;
        }
    }

    public bool RemovePath(string path)
    {
        path = NormalizePath(path);
        EnsureUnderRoot(path);
        lock (_gate)
        {
            if (!_activePaths.Remove(path))
                return false;

            RebuildActiveTree();
            return true;
        }
    }

    public IDisposable Register(DBusConnection connection)
    {
        var registrations = new List<IDisposable>();
        lock (_gate)
        {
            registrations.AddRange(_activeObjects.Values.Select(handler => connection.RegisterObject(handler)));
        }

        return new CompositeDisposable(registrations);
    }

    private DBusObject EnsureObject(string path)
    {
        if (_objects.TryGetValue(path, out var obj)) return obj;
        obj = new DBusObject(path);
        _objects[path] = obj;
        return obj;
    }

    private void RebuildActiveTree()
    {
        _activeObjects.Clear();

        var activeNodes = new HashSet<string>(StringComparer.Ordinal);
        var children = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var path in _activePaths) AddActivePath(path, activeNodes, children);

        foreach (var pair in _objects)
        {
            if (children.TryGetValue(pair.Key, out var childNames))
            {
                var sortedChildren = childNames.OrderBy(static x => x, StringComparer.Ordinal).ToArray();
                pair.Value.SetChildNodes(sortedChildren);
            }
            else
                pair.Value.SetChildNodes([]);
        }

        foreach (var path in activeNodes)
        {
            if (_objects.TryGetValue(path, out var obj)) _activeObjects[path] = obj;
        }
    }

    private void AddActivePath(string path, HashSet<string> activeNodes, Dictionary<string, HashSet<string>> children)
    {
        EnsureObject(path);

        var segments = SplitPath(path);
        var current = _rootPath;
        activeNodes.Add(current);

        for (var i = _rootSegments.Length; i < segments.Length; i++)
        {
            var child = segments[i];
            var next = current == "/" ? "/" + child : current + "/" + child;
            EnsureObject(next);
            activeNodes.Add(next);

            if (!children.TryGetValue(current, out var childSet))
            {
                childSet = new HashSet<string>(StringComparer.Ordinal);
                children[current] = childSet;
            }
            childSet.Add(child);

            current = next;
        }
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must be provided.", nameof(path));

        if (!path.StartsWith('/'))
            throw new ArgumentException("Path must start with '/'.", nameof(path));

        if (path.Length > 1 && path.EndsWith('/')) path = path.TrimEnd('/');

        return path;
    }

    private static string[] SplitPath(string path)
    {
        return path == "/" ? [] : path.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
    }

    private void EnsureUnderRoot(string path)
    {
        if (_rootPath == "/")
            return;

        if (string.Equals(path, _rootPath, StringComparison.Ordinal))
            return;

        if (!path.StartsWith(_rootPath, StringComparison.Ordinal) || path.Length <= _rootPath.Length || path[_rootPath.Length] != '/')
            throw new ArgumentException("Path must be under the root path.", nameof(path));
    }

    private sealed class CompositeDisposable(IReadOnlyList<IDisposable> items) : IDisposable
    {
        public void Dispose()
        {
            foreach (var item in items) 
                item.Dispose();
        }
    }
}
