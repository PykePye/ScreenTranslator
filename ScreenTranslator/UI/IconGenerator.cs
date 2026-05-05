using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Media.Imaging;
using System.IO;

namespace ScreenTranslator.UI;

public static class IconGenerator
{
    private static Icon? _appIcon;

    public static Icon GetAppIcon()
    {
        if (_appIcon != null) return _appIcon;

        // Create a 64x64 bitmap for the icon
        using var bitmap = new Bitmap(64, 64);
        using var g = Graphics.FromImage(bitmap);
        
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Draw a rounded rectangle/circle background
        using var brush = new LinearGradientBrush(
            new Rectangle(0, 0, 64, 64),
            Color.FromArgb(0, 122, 255), // iOS Blue
            Color.FromArgb(10, 132, 255),
            LinearGradientMode.ForwardDiagonal);
        
        g.FillEllipse(brush, 4, 4, 56, 56);

        // Draw a white 'T' for Translator
        using var font = new Font("Segoe UI", 32, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.White);
        
        var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        g.DrawString("T", font, textBrush, new RectangleF(0, 2, 64, 64), format);

        _appIcon = Icon.FromHandle(bitmap.GetHicon());
        return _appIcon;
    }

    public static BitmapSource GetAppIconSource()
    {
        using var icon = GetAppIcon();
        using var bitmap = icon.ToBitmap();
        using var ms = new MemoryStream();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Position = 0;
        
        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = ms;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();
        return bitmapImage;
    }
}