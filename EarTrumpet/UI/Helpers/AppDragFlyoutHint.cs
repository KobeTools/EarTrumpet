using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EarTrumpet.UI.Helpers
{
    /// <summary>
    /// Fallback drag hint inside the flyout HWND when the shell drag image cannot be applied.
    /// </summary>
    public sealed class AppDragFlyoutHint : IDisposable
    {
        private readonly Panel _host;
        private readonly Canvas _canvas;
        private readonly Image _image;
        private readonly double _hotspotX;
        private readonly double _hotspotY;
        private bool _disposed;

        private AppDragFlyoutHint(Panel host, Canvas canvas, Image image, double hotspotX, double hotspotY)
        {
            _host = host;
            _canvas = canvas;
            _image = image;
            _hotspotX = hotspotX;
            _hotspotY = hotspotY;
            CompositionTarget.Rendering += OnRendering;
            UpdatePosition();
        }

        public static AppDragFlyoutHint TryStart(Window window, FrameworkElement iconRoot, Point dragHotspotInIcon)
        {
            if (window == null || iconRoot == null || !AppDragVisualSettings.IsEnabled)
            {
                return null;
            }

            var host = (window.FindName("LayoutRoot") ?? window.FindName("ContentGrid")) as Panel;
            if (host == null)
            {
                return null;
            }

            var icon = AppDragImage.TryGetIconBitmap(iconRoot);
            if (icon == null)
            {
                return null;
            }

            var display = AppDragImage.GetDisplaySize(iconRoot);
            var intensity = AppDragVisualSettings.GetIntensity();
            var dragBitmap = AppDragImage.CreateDragImageBitmap(
                iconRoot,
                icon,
                display,
                intensity,
                dragHotspotInIcon,
                out _,
                out _);

            if (dragBitmap == null)
            {
                return null;
            }

            var paddingDip = AppDragVisualSettings.GetGlowPaddingDip(intensity);
            var hotspotX = dragHotspotInIcon.X + paddingDip;
            var hotspotY = dragHotspotInIcon.Y + paddingDip;

            var totalWidthDip = display.Width + (paddingDip * 2);
            var totalHeightDip = display.Height + (paddingDip * 2);
            var image = new Image
            {
                Source = dragBitmap,
                Width = totalWidthDip,
                Height = totalHeightDip,
                Stretch = Stretch.Fill,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.Fant);

            var canvas = new Canvas
            {
                IsHitTestVisible = false,
                Background = Brushes.Transparent,
                Width = host.ActualWidth,
                Height = host.ActualHeight,
            };
            Panel.SetZIndex(canvas, int.MaxValue);
            canvas.Children.Add(image);

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

                return new AppDragFlyoutHint(host, canvas, image, hotspotX, hotspotY);
            }
            catch
            {
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
            Canvas.SetLeft(_image, local.X - _hotspotX);
            Canvas.SetTop(_image, local.Y - _hotspotY);

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
    }
}
