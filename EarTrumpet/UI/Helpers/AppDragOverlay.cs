using EarTrumpet.Extensions;
using EarTrumpet.Interop;
using EarTrumpet.UI.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EarTrumpet.UI.Helpers
{
    /// <summary>
    /// Topmost click-through window that follows the cursor during DoDragDrop.
    /// Works with WPF SoftwareOnly rendering (transparent/layered popups do not).
    /// </summary>
    public sealed class AppDragOverlay : IDisposable
    {
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST = 0x00000008;

        private readonly Window _window;
        private readonly double _hotspotX;
        private readonly double _hotspotY;
        private bool _disposed;

        private AppDragOverlay(Window window, double hotspotX, double hotspotY)
        {
            _window = window;
            _hotspotX = hotspotX;
            _hotspotY = hotspotY;
            CompositionTarget.Rendering += OnRendering;
            UpdatePosition();
        }

        public static AppDragOverlay TryStart(FrameworkElement iconRoot, System.Windows.Point dragHotspotInIcon)
        {
            if (iconRoot == null)
            {
                DevTrace.Write("AppDragOverlay: no icon element");
                return null;
            }

            var bitmap = AppDragImage.TryGetIconBitmap(iconRoot);
            if (bitmap == null)
            {
                DevTrace.Write("AppDragOverlay: could not get icon bitmap");
                return null;
            }

            var width = Math.Max(bitmap.PixelWidth, 1);
            var height = Math.Max(bitmap.PixelHeight, 1);
            var hotspotX = Clamp(dragHotspotInIcon.X, 0, width);
            var hotspotY = Clamp(dragHotspotInIcon.Y, 0, height);

            var image = new Image
            {
                Source = bitmap,
                Width = width,
                Height = height,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
                Stretch = Stretch.None,
            };

            var window = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = false,
                Background = Brushes.White,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                IsHitTestVisible = false,
                SizeToContent = SizeToContent.Manual,
                Width = width,
                Height = height,
                Content = image,
            };

            window.SourceInitialized += (_, __) => MakeClickThrough(window);

            try
            {
                window.Show();
                DevTrace.Write($"AppDragOverlay: showing {width}x{height} icon at cursor");
                return new AppDragOverlay(window, hotspotX, hotspotY);
            }
            catch (Exception ex)
            {
                DevTrace.LogException("AppDragOverlay.Show", ex);
                return null;
            }
        }

        private static void MakeClickThrough(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var exStyle = User32.GetWindowLong(hwnd, User32.GWL.GWL_EXSTYLE);
            User32.SetWindowLong(
                hwnd,
                User32.GWL.GWL_EXSTYLE,
                exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_TOPMOST);
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
            var pos = System.Windows.Forms.Cursor.Position;
            _window.Left = pos.X - _hotspotX;
            _window.Top = pos.Y - _hotspotY;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CompositionTarget.Rendering -= OnRendering;
            _window.Close();
        }

        private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
    }
}
