using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Avalonia.DBus;

public sealed class DBusSubtreeRegistrationHelper(DBusConnection connection)
{
    private readonly object _gate = new();
    private readonly DBusConnection _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    private Dictionary<string, DBusSubtreeRegistration> _activeRegistrations = new(StringComparer.Ordinal);

    public void ApplySnapshot(
        IEnumerable<DBusSubtreeRegistration> registrations,
        SynchronizationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        var desiredRegistrations = new Dictionary<string, DBusSubtreeRegistration>(StringComparer.Ordinal);
        foreach (var registration in registrations)
        {
            var normalizedPath = NormalizePath(registration.FullPath);
            if (desiredRegistrations.ContainsKey(normalizedPath))
                throw new InvalidOperationException($"Duplicate subtree registration for path '{normalizedPath}'.");

            desiredRegistrations.Add(
                normalizedPath,
                new DBusSubtreeRegistration(normalizedPath, registration.Target, registration.Lifecycle));
        }

        List<DBusSubtreeRegistration> disconnected;
        List<DBusSubtreeRegistration> connected;

        lock (_gate)
        {
            if (AreEquivalent(_activeRegistrations, desiredRegistrations))
                return;

            disconnected = [];
            connected = [];

            var toRemove = _activeRegistrations
                .Where(pair => !desiredRegistrations.TryGetValue(pair.Key, out var desired)
                               || !ReferenceEquals(pair.Value.Target, desired.Target)
                               || !ReferenceEquals(pair.Value.Lifecycle, desired.Lifecycle))
                .Select(pair => pair.Value)
                .ToArray();

            var toAdd = desiredRegistrations
                .Where(pair => !_activeRegistrations.TryGetValue(pair.Key, out var active)
                               || !ReferenceEquals(pair.Value.Target, active.Target)
                               || !ReferenceEquals(pair.Value.Lifecycle, active.Lifecycle))
                .Select(pair => pair.Value)
                .ToArray();

            var removeOperations = _activeRegistrations
                .Where(pair => !desiredRegistrations.ContainsKey(pair.Key))
                .Select(pair => pair.Value.FullPath)
                .ToArray();

            var addOperations = desiredRegistrations
                .Where(pair => !_activeRegistrations.ContainsKey(pair.Key))
                .Select(pair => pair.Value)
                .ToArray();

            var replaceOperations = desiredRegistrations
                .Where(pair =>
                    _activeRegistrations.TryGetValue(pair.Key, out var active)
                    && !ReferenceEquals(pair.Value.Target, active.Target))
                .Select(pair => pair.Value)
                .ToArray();

            List<DBusRegistrationOperation> operations = [];
            operations.AddRange(removeOperations.Select(DBusRegistrationOperation.Remove));
            operations.AddRange(replaceOperations.Select(static replacement =>
                DBusRegistrationOperation.Replace(replacement.FullPath, replacement.Target)));
            operations.AddRange(addOperations.Select(static addition =>
                DBusRegistrationOperation.Add(addition.FullPath, addition.Target)));

            _connection.ApplyRegistrationBatch(operations, context);

            _activeRegistrations = desiredRegistrations;
            disconnected.AddRange(toRemove);
            connected.AddRange(toAdd);
        }

        foreach (var registration in disconnected)
        { 
            registration.Lifecycle?.OnDisconnectedFromTree(_connection, registration.FullPath);
        }

        foreach (var registration in connected)
        {
            registration.Lifecycle?.OnConnectedToTree(_connection, registration.FullPath);
        }
    }

    public void Clear(SynchronizationContext? context = null)
    {
        List<DBusSubtreeRegistration> disconnected;
        lock (_gate)
        {
            if (_activeRegistrations.Count == 0)
                return;

            disconnected = _activeRegistrations.Values.ToList();
            var operations = disconnected
                .Select(static registration => DBusRegistrationOperation.Remove(registration.FullPath))
                .ToArray();
            _connection.ApplyRegistrationBatch(operations, context);

            _activeRegistrations = new Dictionary<string, DBusSubtreeRegistration>(StringComparer.Ordinal);
        }

        foreach (var registration in disconnected)
        {
            registration.Lifecycle?.OnDisconnectedFromTree(_connection, registration.FullPath);
        }
    }

    private static bool AreEquivalent(
        IReadOnlyDictionary<string, DBusSubtreeRegistration> active,
        IReadOnlyDictionary<string, DBusSubtreeRegistration> desired)
    {
        if (active.Count != desired.Count)
            return false;

        foreach (var (path, activeRegistration) in active)
        {
            if (!desired.TryGetValue(path, out var desiredRegistration))
                return false;

            if (!ReferenceEquals(activeRegistration.Target, desiredRegistration.Target)
                || !ReferenceEquals(activeRegistration.Lifecycle, desiredRegistration.Lifecycle))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must be provided.", nameof(path));

        if (!path.StartsWith('/'))
            throw new ArgumentException("Path must start with '/'.", nameof(path));

        if (path.Length > 1 && path.EndsWith('/'))
            return path.TrimEnd('/');

        return path;
    }
}
