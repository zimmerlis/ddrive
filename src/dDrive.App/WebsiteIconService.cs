using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using dDrive.Core.Connections;

namespace dDrive.App;

public static class WebsiteIconService
{
    private const int MaxIconSize = 256;
    private const int MaxHtmlBytes = 512 * 1024;
    private static readonly Regex LinkRegex = new("<link\\s[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RelRegex = new("rel\\s*=\\s*[\"']([^\"']*)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HrefRegex = new("href\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string IconCacheDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dDrive", "icons");

    /// <summary>
    /// Returns the start page of a WebDAV connection on the standard web port (no WebDAV port).
    /// </summary>
    public static Uri? GetWebsiteUri(ConnectionProfile profile)
    {
        if (profile.Protocol != ConnectionProtocol.WebDav || string.IsNullOrWhiteSpace(profile.Host))
        {
            return null;
        }

        var host = profile.Host.Trim();
        string scheme;
        string hostName;
        if (host.Contains("://", StringComparison.Ordinal) && Uri.TryCreate(host, UriKind.Absolute, out var absolute))
        {
            scheme = absolute.Scheme;
            hostName = absolute.Host;
        }
        else
        {
            scheme = profile.UseTls ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
            hostName = host.Split('/')[0];
            var colon = hostName.LastIndexOf(':');
            if (colon > 0 && hostName.IndexOf(']') < colon)
            {
                hostName = hostName[..colon];
            }
        }

        if (string.IsNullOrWhiteSpace(hostName))
        {
            return null;
        }

        return new UriBuilder(scheme, hostName, -1, "/").Uri;
    }

    public static string GetFaviconCachePath(Guid id) => Path.Combine(IconCacheDirectory, $"{id:N}.ico");

    public static string GetCustomIconCachePath(Guid id) => Path.Combine(IconCacheDirectory, $"{id:N}-custom.ico");

    /// <summary>
    /// Returns the icon to show for a connection: the custom icon if set, otherwise the cached favicon.
    /// </summary>
    public static string? GetEffectiveIconPath(ConnectionProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.CustomIconPath) && File.Exists(profile.CustomIconPath))
        {
            return profile.CustomIconPath;
        }

        var favicon = GetFaviconCachePath(profile.Id);
        return File.Exists(favicon) ? favicon : null;
    }

    public static async Task<string?> DownloadFaviconAsync(Uri siteUri, Guid id, bool verifyTls, CancellationToken cancellationToken = default)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        if (!verifyTls)
        {
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) dDrive");

        var candidates = new List<Uri>();
        try
        {
            var html = await client.GetStringAsync(siteUri, cancellationToken);
            if (html.Length > MaxHtmlBytes)
            {
                html = html[..MaxHtmlBytes];
            }

            foreach (Match link in LinkRegex.Matches(html))
            {
                var tag = link.Value;
                var rel = RelRegex.Match(tag);
                var href = HrefRegex.Match(tag);
                if (!rel.Success || !href.Success || !rel.Groups[1].Value.Contains("icon", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Uri.TryCreate(siteUri, WebUtility.HtmlDecode(href.Groups[1].Value), out var candidate))
                {
                    candidates.Add(candidate);
                }
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Startseite nicht lesbar; es wird nur /favicon.ico versucht.
        }

        candidates.Add(new Uri(siteUri, "/favicon.ico"));

        foreach (var candidate in candidates.DistinctBy(uri => uri.AbsoluteUri))
        {
            try
            {
                using var response = await client.GetAsync(candidate, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                var target = GetFaviconCachePath(id);
                if (TryWriteIcon(bytes, target))
                {
                    return target;
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Nächsten Kandidaten versuchen.
            }
        }

        return null;
    }

    public static string? ImportCustomIcon(string sourcePath, Guid id)
    {
        if (!File.Exists(sourcePath))
        {
            return null;
        }

        var target = GetCustomIconCachePath(id);
        return TryWriteIcon(File.ReadAllBytes(sourcePath), target) ? target : null;
    }

    public static void DeleteCachedIcons(Guid id)
    {
        foreach (var path in new[] { GetFaviconCachePath(id), GetCustomIconCachePath(id) })
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // Datei wird evtl. gerade verwendet; wird beim nächsten Speichern überschrieben.
            }
        }
    }

    /// <summary>
    /// Decodes an image (ICO, PNG, JPEG, GIF, BMP) and stores it as a single-entry ICO with a PNG payload.
    /// </summary>
    private static bool TryWriteIcon(byte[] data, string targetPath)
    {
        try
        {
            using var input = new MemoryStream(data);
            var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
            {
                return false;
            }

            var frame = decoder.Frames.OrderByDescending(f => f.PixelWidth * f.PixelHeight).First();
            BitmapSource source = frame;
            var largest = Math.Max(frame.PixelWidth, frame.PixelHeight);
            if (largest > MaxIconSize)
            {
                var scale = (double)MaxIconSize / largest;
                source = new TransformedBitmap(frame, new System.Windows.Media.ScaleTransform(scale, scale));
            }

            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(source));
            using var pngStream = new MemoryStream();
            png.Save(pngStream);

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            using var file = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(file);
            var width = source.PixelWidth >= MaxIconSize ? 0 : source.PixelWidth;
            var height = source.PixelHeight >= MaxIconSize ? 0 : source.PixelHeight;
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)1);
            writer.Write((byte)width);
            writer.Write((byte)height);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write((uint)pngStream.Length);
            writer.Write((uint)22);
            writer.Write(pngStream.ToArray());
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
