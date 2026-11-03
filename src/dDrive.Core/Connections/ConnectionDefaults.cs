namespace dDrive.Core.Connections;

public static class ConnectionDefaults
{
    /// <summary>
    /// Ensures exactly one connection is the default. Keeps the first marked connection,
    /// or the first connection overall if none is marked.
    /// </summary>
    public static List<ConnectionProfile> Normalize(IEnumerable<ConnectionProfile> connections)
    {
        var list = connections.ToList();
        if (list.Count == 0)
        {
            return list;
        }

        var defaultIndex = list.FindIndex(connection => connection.IsDefault);
        if (defaultIndex < 0)
        {
            defaultIndex = 0;
        }

        for (var i = 0; i < list.Count; i++)
        {
            var shouldBeDefault = i == defaultIndex;
            if (list[i].IsDefault != shouldBeDefault)
            {
                list[i] = list[i] with { IsDefault = shouldBeDefault };
            }
        }

        return list;
    }

    /// <summary>
    /// Returns a copy of the list where only the given connection is the default.
    /// </summary>
    public static List<ConnectionProfile> SetDefault(IEnumerable<ConnectionProfile> connections, Guid id)
    {
        return connections
            .Select(connection => connection with { IsDefault = connection.Id == id })
            .ToList();
    }
}
