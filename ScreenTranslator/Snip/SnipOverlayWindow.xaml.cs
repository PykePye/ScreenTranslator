using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using ScreenTranslator.UI;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace ScreenTranslator.Snip;

public partial class SnipOverlayWindow : Window
{
    private const double MinimumSelectionSize = 5;
    private const double HandleHitSize = 10;
    private const double HandleVisualSize = 10;
    private const double ConfirmationPanelWidth = 90;
    private const double ConfirmationPanelHeight = 38;

    private Rect _virtualScreenBounds;
    private Rect _selectionRect;
    private Rect _dragStartRect;
    private Point _dragStartPoint;
    private DragMode _dragMode;

    public Bitmap? CapturedBitmap { get; private set; }
    public Rect? CapturedRegion { get; private set; }
    public bool IsSuccess { get; private set; }

    [Flags]
    private enum DragMode
    {
        None = 0,
        NewSelection = 1,
        Move = 2,
        Left = 4,
        Top = 8,
        Right = 16,
        Bottom = 32
    }

    public SnipOverlayWindow()
    {
        InitializeComponent();
        Icon = IconGenerator.GetAppIconSource();

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
        RenderSelection();
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
        var point = e.GetPosition(RootGrid);
        _dragStartPoint = point;
        _dragStartRect = _selectionRect;
        _dragMode = GetDragMode(point);

        if (_dragMode == DragMode.None)
        {
            _dragMode = DragMode.NewSelection;
            _selectionRect = new Rect(point, point);
            HintText.Visibility = Visibility.Collapsed;
        }

        InputCanvas.CaptureMouse();
        RenderSelection();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var point = e.GetPosition(RootGrid);

        if (_dragMode == DragMode.None)
        {
            UpdateCursor(GetDragMode(point));
            return;
        }

        if (_dragMode == DragMode.NewSelection)
        {
            _selectionRect = MakeRect(_dragStartPoint, ClampToCanvas(point));
        }
        else if (_dragMode == DragMode.Move)
        {
            var horizontalOffset = point.X - _dragStartPoint.X;
            var verticalOffset = point.Y - _dragStartPoint.Y;
            var left = Math.Clamp(_dragStartRect.Left + horizontalOffset, 0, Math.Max(0, ActualWidth - _dragStartRect.Width));
            var top = Math.Clamp(_dragStartRect.Top + verticalOffset, 0, Math.Max(0, ActualHeight - _dragStartRect.Height));
            _selectionRect = new Rect(left, top, _dragStartRect.Width, _dragStartRect.Height);
        }
        else
        {
            ResizeSelection(ClampToCanvas(point));
        }

        RenderSelection();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragMode == DragMode.None) return;

        InputCanvas.ReleaseMouseCapture();
        var wasNewSelection = _dragMode == DragMode.NewSelection;
        _dragMode = DragMode.None;

        if (wasNewSelection && !HasValidSelection)
        {
            _selectionRect = Rect.Empty;
            HintText.Visibility = Visibility.Visible;
        }

        RenderSelection();
        UpdateCursor(GetDragMode(e.GetPosition(RootGrid)));
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!HasValidSelection) return;

        var screenRect = new Rect(
            _selectionRect.Left + _virtualScreenBounds.Left,
            _selectionRect.Top + _virtualScreenBounds.Top,
            _selectionRect.Width,
            _selectionRect.Height);

        Hide();
        System.Windows.Forms.Application.DoEvents();
        System.Threading.Thread.Sleep(50);

        CapturedBitmap = CaptureScreenRegion(screenRect);
        CapturedRegion = screenRect;
        IsSuccess = true;
        Close();
    }

    private void Discard_Click(object sender, RoutedEventArgs e)
    {
        IsSuccess = false;
        Close();
    }

    private void ResizeSelection(Point point)
    {
        var left = _dragStartRect.Left;
        var top = _dragStartRect.Top;
        var right = _dragStartRect.Right;
        var bottom = _dragStartRect.Bottom;

        if (_dragMode.HasFlag(DragMode.Left))
            left = Math.Clamp(point.X, 0, right - MinimumSelectionSize);
        if (_dragMode.HasFlag(DragMode.Right))
            right = Math.Clamp(point.X, left + MinimumSelectionSize, ActualWidth);
        if (_dragMode.HasFlag(DragMode.Top))
            top = Math.Clamp(point.Y, 0, bottom - MinimumSelectionSize);
        if (_dragMode.HasFlag(DragMode.Bottom))
            bottom = Math.Clamp(point.Y, top + MinimumSelectionSize, ActualHeight);

        _selectionRect = new Rect(new Point(left, top), new Point(right, bottom));
    }

    private DragMode GetDragMode(Point point)
    {
        if (!HasValidSelection) return DragMode.None;

        var nearLeft = Math.Abs(point.X - _selectionRect.Left) <= HandleHitSize;
        var nearRight = Math.Abs(point.X - _selectionRect.Right) <= HandleHitSize;
        var nearTop = Math.Abs(point.Y - _selectionRect.Top) <= HandleHitSize;
        var nearBottom = Math.Abs(point.Y - _selectionRect.Bottom) <= HandleHitSize;
        var withinHorizontalBounds = point.X >= _selectionRect.Left - HandleHitSize && point.X <= _selectionRect.Right + HandleHitSize;
        var withinVerticalBounds = point.Y >= _selectionRect.Top - HandleHitSize && point.Y <= _selectionRect.Bottom + HandleHitSize;

        var mode = DragMode.None;
        if (withinVerticalBounds && nearLeft) mode |= DragMode.Left;
        if (withinVerticalBounds && nearRight) mode |= DragMode.Right;
        if (withinHorizontalBounds && nearTop) mode |= DragMode.Top;
        if (withinHorizontalBounds && nearBottom) mode |= DragMode.Bottom;
        if (mode != DragMode.None) return mode;

        return _selectionRect.Contains(point) ? DragMode.Move : DragMode.None;
    }

    private void RenderSelection()
    {
        var isVisible = HasValidSelection;
        SelectionBorder.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        ConfirmationPanel.Visibility = isVisible && _dragMode == DragMode.None ? Visibility.Visible : Visibility.Collapsed;

        SetHandleVisibility(TopLeftHandle, isVisible);
        SetHandleVisibility(TopRightHandle, isVisible);
        SetHandleVisibility(BottomLeftHandle, isVisible);
        SetHandleVisibility(BottomRightHandle, isVisible);

        if (!isVisible)
        {
            UpdateMask(null);
            return;
        }

        Canvas.SetLeft(SelectionBorder, _selectionRect.Left);
        Canvas.SetTop(SelectionBorder, _selectionRect.Top);
        SelectionBorder.Width = _selectionRect.Width;
        SelectionBorder.Height = _selectionRect.Height;

        SetHandlePosition(TopLeftHandle, _selectionRect.Left, _selectionRect.Top);
        SetHandlePosition(TopRightHandle, _selectionRect.Right, _selectionRect.Top);
        SetHandlePosition(BottomLeftHandle, _selectionRect.Left, _selectionRect.Bottom);
        SetHandlePosition(BottomRightHandle, _selectionRect.Right, _selectionRect.Bottom);

        var panelLeft = Math.Min(_selectionRect.Right + 10, Math.Max(0, ActualWidth - ConfirmationPanelWidth));
        var preferredPanelTop = _selectionRect.Bottom + 10;
        var panelTop = preferredPanelTop + ConfirmationPanelHeight <= ActualHeight
            ? preferredPanelTop
            : Math.Max(0, _selectionRect.Top - ConfirmationPanelHeight - 10);
        Canvas.SetLeft(ConfirmationPanel, panelLeft);
        Canvas.SetTop(ConfirmationPanel, panelTop);

        UpdateMask(_selectionRect);
    }

    private static void SetHandleVisibility(System.Windows.Shapes.Rectangle handle, bool isVisible)
    {
        handle.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void SetHandlePosition(System.Windows.Shapes.Rectangle handle, double x, double y)
    {
        Canvas.SetLeft(handle, x - HandleVisualSize / 2);
        Canvas.SetTop(handle, y - HandleVisualSize / 2);
    }

    private void UpdateCursor(DragMode mode)
    {
        Cursor = mode switch
        {
            DragMode.Move => System.Windows.Input.Cursors.SizeAll,
            DragMode.Left or DragMode.Right => System.Windows.Input.Cursors.SizeWE,
            DragMode.Top or DragMode.Bottom => System.Windows.Input.Cursors.SizeNS,
            DragMode.Left | DragMode.Top or DragMode.Right | DragMode.Bottom => System.Windows.Input.Cursors.SizeNWSE,
            DragMode.Right | DragMode.Top or DragMode.Left | DragMode.Bottom => System.Windows.Input.Cursors.SizeNESW,
            _ => System.Windows.Input.Cursors.Cross
        };
    }

    private Point ClampToCanvas(Point point) => new(
        Math.Clamp(point.X, 0, ActualWidth),
        Math.Clamp(point.Y, 0, ActualHeight));

    private bool HasValidSelection => _selectionRect.Width >= MinimumSelectionSize && _selectionRect.Height >= MinimumSelectionSize;

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
        var full = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));

        if (holeRect.HasValue && holeRect.Value.Width > 0 && holeRect.Value.Height > 0)
        {
            var hole = new RectangleGeometry(holeRect.Value);
            MaskPath.Data = new CombinedGeometry(GeometryCombineMode.Exclude, full, hole);
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
        var width = Math.Abs(a.X - b.X);
        var height = Math.Abs(a.Y - b.Y);
        return new Rect(x, y, width, height);
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
