namespace dDrive.Core.Storage;

public static class StoragePath
{
    public static string Resolve(string rootPath, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(path);

        var segments = new List<string>();
        AddSegments(segments, rootPath, allowParent: false);
        var rootSegmentCount = segments.Count;
        AddSegments(segments, path, allowParent: true, rootSegmentCount);

        return segments.Count == 0 ? "/" : $"/{string.Join('/', segments)}";
    }

    private static void AddSegments(
        List<string> segments,
        string path,
        bool allowParent,
        int rootSegmentCount = 0)
    {
        foreach (var segment in path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (!allowParent || segments.Count <= rootSegmentCount)
                {
                    throw new ArgumentException("Der Pfad darf das konfigurierte Stammverzeichnis nicht verlassen.", nameof(path));
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }
    }
}