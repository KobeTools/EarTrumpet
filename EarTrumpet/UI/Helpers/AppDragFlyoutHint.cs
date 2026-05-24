using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace EarTrumpet.UI.Helpers
{
    /// <summary>
    /// Draws the drag icon on top of the flyout/full window (same HWND), which stays visible during WPF DoDragDrop.
    /// </summary>
    public sealed class AppDragFlyoutHint : IDisposable
    {
        private readonly Panel _host;
        private readonly Canvas _canvas;
        private readonly FrameworkElement _visualRoot;
        private readonly double _hotspotX;
        private readonly double _hotspotY;
        private bool _disposed;

        private AppDragFlyoutHint(Panel host, Canvas canvas, FrameworkElement visualRoot, double hotspotX, double hotspotY)
        {
            _host = host;
            _canvas = canvas;
            _visualRoot = visualRoot;
            _hotspotX = hotspotX;
            _hotspotY = hotspotY;
            CompositionTarget.Rendering += OnRendering;
            UpdatePosition();
        }

        public static AppDragFlyoutHint TryStart(Window window, FrameworkElement iconRoot, System.Windows.Point dragHotspotInIcon)
        {
            if (window == null || iconRoot == null)
            {
                DevTrace.Write("AppDragFlyoutHint: no window or icon");
                return null;
            }

            var host = (window.FindName("LayoutRoot") ?? window.FindName("ContentGrid")) as Panel;
            if (host == null)
            {
                DevTrace.Write("AppDragFlyoutHint: LayoutRoot/ContentGrid not found");
                return null;
            }

            var bitmap = AppDragImage.TryGetIconBitmap(iconRoot);
            if (bitmap == null)
            {
                DevTrace.Write("AppDragFlyoutHint: no icon bitmap");
                return null;
            }

            var display = AppDragImage.GetDisplaySize(iconRoot);
            var displayWidth = display.Width;
            var displayHeight = display.Height;
            var hotspotX = Clamp(dragHotspotInIcon.X, 0, displayWidth);
            var hotspotY = Clamp(dragHotspotInIcon.Y, 0, displayHeight);

            var visualRoot = CreateDragVisual(bitmap, displayWidth, displayHeight);

            var canvas = new Canvas
            {
                IsHitTestVisible = false,
                Background = Brushes.Transparent,
                Width = host.ActualWidth,
                Height = host.ActualHeight,
            };
            Panel.SetZIndex(canvas, int.MaxValue);
            canvas.Children.Add(visualRoot);

            try
            {
                host.Children.Add(canvas);
                if (host is Grid grid)
                {
                    var rowSpan = grid.RowDefinitions.Count > 0 ? grid.RowDefinitions.Count : 1;
                    var colSpan = grid.ColumnDefinitions.Count > 0 ? grid.ColumnDefinitions.Count : 1;
                    Grid.SetRowSpan(canvas, rowSpan);
                    Grid.SetColumnSpan(canvas, colSpan);
                }

                DevTrace.Write($"AppDragFlyoutHint: {displayWidth:0}x{displayHeight:0} dip (bitmap {bitmap.PixelWidth}x{bitmap.PixelHeight}px)");
                return new AppDragFlyoutHint(host, canvas, visualRoot, hotspotX, hotspotY);
            }
            catch (Exception ex)
            {
                DevTrace.LogException("AppDragFlyoutHint attach", ex);
                return null;
            }
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            UpdatePosition();
        }

        private void UpdatePosition()
        {
            var screen = new System.Windows.Point(
                System.Windows.Forms.Cursor.Position.X,
                System.Windows.Forms.Cursor.Position.Y);

            var local = _host.PointFromScreen(screen);
            Canvas.SetLeft(_visualRoot, local.X - _hotspotX);
            Canvas.SetTop(_visualRoot, local.Y - _hotspotY);

            if (_host.ActualWidth > 0 && _host.ActualHeight > 0)
            {
                _canvas.Width = _host.ActualWidth;
                _canvas.Height = _host.ActualHeight;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CompositionTarget.Rendering -= OnRendering;

            if (_canvas.Parent == _host)
            {
                _host.Children.Remove(_canvas);
            }
        }

        /// <summary>
        /// Halo + glow that follow the icon alpha (works for round/irregular shapes, not a rectangle frame).
        /// </summary>
        private static FrameworkElement CreateDragVisual(BitmapSource bitmap, double width, double height)
        {
            var halo = new Image
            {
                Source = bitmap,
                Width = width,
                Height = height,
                Stretch = Stretch.Uniform,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
                Opacity = 0.42,
                Effect = new BlurEffect
                {
                    Radius = 6,
                    RenderingBias = RenderingBias.Performance,
                },
            };
            RenderOptions.SetBitmapScalingMode(halo, BitmapScalingMode.Fant);

            var icon = new Image
            {
                Source = bitmap,
                Width = width,
                Height = height,
                Stretch = Stretch.Uniform,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
                Effect = new DropShadowEffect
                {
                    Color = Colors.White,
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.65,
                },
            };
            RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.Fant);

            return new Grid
            {
                Width = width,
                Height = height,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
                Children =
                {
                    halo,
                    icon,
                },
            };
        }

        private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
    }
}
