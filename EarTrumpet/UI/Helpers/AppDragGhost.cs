using EarTrumpet.Extensions;
using EarTrumpet.UI.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EarTrumpet.UI.Helpers
{
    /// <summary>
    /// Shows the app icon near the cursor during DoDragDrop (WPF's default ghost is an empty dashed box).
    /// </summary>
    public sealed class AppDragGhost : IDisposable
    {
        private readonly Window _ghostWindow;
        private readonly Image _image;
        private bool _disposed;

        private AppDragGhost(BitmapSource bitmap, double width, double height)
        {
            _image = new Image
            {
                Source = bitmap,
                Width = width,
                Height = height,
                IsHitTestVisible = false,
            };

            _ghostWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                IsHitTestVisible = false,
                SizeToContent = SizeToContent.WidthAndHeight,
                Content = _image,
            };

            CompositionTarget.Rendering += OnRendering;
            _ghostWindow.Show();
            UpdatePosition();
        }

        public static AppDragGhost Start(FrameworkElement iconElement)
        {
            var bitmap = GetIconBitmap(iconElement);
            if (bitmap == null)
            {
                DevTrace.Write("AppDragGhost: no icon bitmap available");
                return null;
            }

            var width = iconElement.ActualWidth > 0 ? iconElement.ActualWidth : 32;
            var height = iconElement.ActualHeight > 0 ? iconElement.ActualHeight : 32;
            return new AppDragGhost(bitmap, width, height);
        }

        private static BitmapSource GetIconBitmap(FrameworkElement iconElement)
        {
            if (iconElement == null)
            {
                return null;
            }

            var imageEx = iconElement.FindVisualChild<ImageEx>() ?? iconElement as ImageEx;
            if (imageEx?.Source is BitmapSource existing)
            {
                return existing;
            }

            if (iconElement is Image image && image.Source is BitmapSource imageSource)
            {
                return imageSource;
            }

            return CaptureVisual(iconElement);
        }

        private static BitmapSource CaptureVisual(Visual visual)
        {
            var element = visual as FrameworkElement;
            var width = (int)Math.Max(1, Math.Ceiling(element?.ActualWidth ?? 32));
            var height = (int)Math.Max(1, Math.Ceiling(element?.ActualHeight ?? 32));
            if (width <= 1 || height <= 1)
            {
                width = height = 32;
            }

            try
            {
                var dpiX = 96.0;
                var dpiY = 96.0;
                var source = PresentationSource.FromVisual(visual);
                if (source?.CompositionTarget != null)
                {
                    var transform = source.CompositionTarget.TransformToDevice;
                    dpiX = 96.0 * transform.M11;
                    dpiY = 96.0 * transform.M22;
                }

                var rtb = new RenderTargetBitmap(width, height, dpiX, dpiY, PixelFormats.Pbgra32);
                rtb.Render(visual);
                rtb.Freeze();
                return rtb;
            }
            catch (Exception ex)
            {
                DevTrace.LogException("AppDragGhost capture", ex);
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
            if (_disposed)
            {
                return;
            }

            var pos = System.Windows.Forms.Cursor.Position;
            _ghostWindow.Left = pos.X + 12;
            _ghostWindow.Top = pos.Y + 12;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CompositionTarget.Rendering -= OnRendering;
            _ghostWindow.Close();
        }
    }
}
