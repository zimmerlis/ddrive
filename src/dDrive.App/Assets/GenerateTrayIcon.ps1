Add-Type -AssemblyName System.Drawing

$iconSource = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class DDriveTrayIconAssetGenerator
{
    private static GraphicsPath RoundedRectangle(RectangleF rectangle, float radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2;
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static byte[] RenderPng(int size)
    {
        using (var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);
            graphics.ScaleTransform(size / 256f, size / 256f);

            RectangleF tile = new RectangleF(18, 18, 220, 220);
            using (var tilePath = RoundedRectangle(tile, 52))
            using (var background = new LinearGradientBrush(tile, Color.FromArgb(31, 59, 67), Color.FromArgb(11, 28, 36), 135f))
            using (var border = new Pen(Color.FromArgb(76, 125, 125), 3f))
            {
                graphics.FillPath(background, tilePath);
                graphics.DrawPath(border, tilePath);
            }

            using (var mark = new Pen(Color.FromArgb(139, 235, 207), 23f))
            using (var accent = new SolidBrush(Color.FromArgb(70, 222, 190)))
            using (var dark = new SolidBrush(Color.FromArgb(14, 34, 41)))
            {
                mark.StartCap = LineCap.Round;
                mark.EndCap = LineCap.Round;
                graphics.DrawEllipse(mark, 54, 70, 112, 112);
                graphics.DrawLine(mark, 169, 46, 169, 181);
                graphics.FillEllipse(dark, 87, 103, 46, 46);
                graphics.FillEllipse(accent, 176, 177, 23, 23);
            }

            using (var detail = new Pen(Color.FromArgb(70, 222, 190), 7f))
            {
                detail.StartCap = LineCap.Round;
                detail.EndCap = LineCap.Round;
                graphics.DrawLine(detail, 77, 201, 179, 201);
            }

            using (var output = new MemoryStream())
            {
                bitmap.Save(output, ImageFormat.Png);
                return output.ToArray();
            }
        }
    }

    public static void Create(string outputPath)
    {
        int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
        byte[][] images = new byte[sizes.Length][];
        for (int index = 0; index < sizes.Length; index++)
        {
            images[index] = RenderPng(sizes[index]);
        }

        string directory = Path.GetDirectoryName(outputPath);
        Directory.CreateDirectory(directory);
        using (var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)sizes.Length);
            int offset = 6 + sizes.Length * 16;
            for (int index = 0; index < sizes.Length; index++)
            {
                byte size = sizes[index] == 256 ? (byte)0 : (byte)sizes[index];
                writer.Write(size);
                writer.Write(size);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)images[index].Length);
                writer.Write((uint)offset);
                offset += images[index].Length;
            }

            for (int index = 0; index < images.Length; index++)
            {
                writer.Write(images[index]);
            }
        }
    }
}
'@

Add-Type -TypeDefinition $iconSource -ReferencedAssemblies 'System.Drawing.dll'
[DDriveTrayIconAssetGenerator]::Create((Join-Path $PSScriptRoot 'dDrive.ico'))