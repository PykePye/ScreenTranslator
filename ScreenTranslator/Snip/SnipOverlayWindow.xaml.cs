using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using ScreenTranslator.UI;
using Application = System.Windows.Application;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace ScreenTranslator.Snip;

public partial class SnipOverlayWindow : Window
{
    private Point _startPoint;
    private bool _isSelecting;
    private Rect _virtualScreenBounds;

    public Bitmap? CapturedBitmap { get; private set; }
    public Rect? CapturedRegion { get; private set; }
    public bool IsSuccess { get; private set; }

    public SnipOverlayWindow()
    {
        InitializeComponent();
        Icon = IconGenerator.GetAppIconSource();

        // Phủ toàn bộ virtual screen (bao gồm tất cả monitor)
        var vs = SystemInformation.VirtualScreen;
        _virtualScreenBounds = new Rect(vs.Left, vs.Top, vs.Width, vs.Height);

        Left = vs.Left;
        Top = vs.Top;
        Width = vs.Width;
        Height = vs.Height;

        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
        InputCanvas.MouseLeftButtonDown += OnMouseDown;
        InputCanvas.MouseMove += OnMouseMove;
        InputCanvas.MouseLeftButtonUp += OnMouseUp;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateMask(null);
        Activate();
        Focus();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            IsSuccess = false;
            Close();
        }
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(RootGrid);
        _isSelecting = true;
        HintText.Visibility = Visibility.Collapsed;
        SelectionBorder.Visibility = Visibility.Visible;
        InputCanvas.CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSelecting) return;

        var current = e.GetPosition(RootGrid);
        var rect = MakeRect(_startPoint, current);

        SelectionBorder.Width = rect.Width;
        SelectionBorder.Height = rect.Height;
        SelectionBorder.Margin = new Thickness(rect.Left, rect.Top, 0, 0);
        SelectionBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        SelectionBorder.VerticalAlignment = System.Windows.VerticalAlignment.Top;

        UpdateMask(rect);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting) return;

        _isSelecting = false;
        InputCanvas.ReleaseMouseCapture();

        var endPoint = e.GetPosition(RootGrid);
        var rectInWindow = MakeRect(_startPoint, endPoint);

        // Vùng quá nhỏ — coi như user click nhầm, hủy
        if (rectInWindow.Width < 5 || rectInWindow.Height < 5)
        {
            IsSuccess = false;
            Close();
            return;
        }

        // Convert toạ độ window-relative sang screen-absolute
        var screenRect = new Rect(
            rectInWindow.Left + _virtualScreenBounds.Left,
            rectInWindow.Top + _virtualScreenBounds.Top,
            rectInWindow.Width,
            rectInWindow.Height);

        // Ẩn overlay TRƯỚC khi capture, không thì capture cả lớp mask đen
        Hide();
        System.Windows.Forms.Application.DoEvents();
        System.Threading.Thread.Sleep(50);  // đợi compositor refresh

        CapturedBitmap = CaptureScreenRegion(screenRect);
        CapturedRegion = screenRect;
        IsSuccess = true;
        Close();
    }

    private Bitmap CaptureScreenRegion(Rect region)
    {
        var width = (int)region.Width;
        var height = (int)region.Height;
        var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(
            (int)region.Left,
            (int)region.Top,
            0, 0,
            new System.Drawing.Size(width, height),
            CopyPixelOperation.SourceCopy);

        return bmp;
    }

    private void UpdateMask(Rect? holeRect)
    {
        // Full-screen geometry
        var full = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));

        if (holeRect.HasValue && holeRect.Value.Width > 0 && holeRect.Value.Height > 0)
        {
            // Khoét lỗ: full minus hole
            var hole = new RectangleGeometry(holeRect.Value);
            var combined = new CombinedGeometry(GeometryCombineMode.Exclude, full, hole);
            MaskPath.Data = combined;
        }
        else
        {
            MaskPath.Data = full;
        }
    }

    private static Rect MakeRect(Point a, Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        var w = Math.Abs(a.X - b.X);
        var h = Math.Abs(a.Y - b.Y);
        return new Rect(x, y, w, h);
    }

    protected override void OnClosed(EventArgs e)
    {
        InputCanvas.MouseLeftButtonDown -= OnMouseDown;
        InputCanvas.MouseMove -= OnMouseMove;
        InputCanvas.MouseLeftButtonUp -= OnMouseUp;
        KeyDown -= OnKeyDown;
        base.OnClosed(e);
    }
}