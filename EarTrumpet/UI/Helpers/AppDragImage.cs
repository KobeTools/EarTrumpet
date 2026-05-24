using EarTrumpet.Extensions;
using EarTrumpet.Interop;
using EarTrumpet.UI.Controls;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ComIDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingColor = System.Drawing.Color;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingImageLockMode = System.Drawing.Imaging.ImageLockMode;

namespace EarTrumpet.UI.Helpers
{
    /// <summary>
    /// Builds drag images (icon + optional glow) and registers them with the shell drag helper.
    /// </summary>
    public static class AppDragImage
    {
        public const double FallbackIconSizeDip = 24;

        public static Size GetDisplaySize(FrameworkElement iconRoot)
        {
            if (iconRoot == null)
            {
                return new Size(FallbackIconSizeDip, FallbackIconSizeDip);
            }

            iconRoot.UpdateLayout();
            var width = iconRoot.ActualWidth > 0 ? iconRoot.ActualWidth : FallbackIconSizeDip;
            var height = iconRoot.ActualHeight > 0 ? iconRoot.ActualHeight : FallbackIconSizeDip;
            return new Size(width, height);
        }

        public static BitmapSource TryGetIconBitmap(FrameworkElement iconRoot)
        {
            if (iconRoot == null)
            {
                return null;
            }

            var imageEx = iconRoot.FindVisualChild<ImageEx>();
            if (imageEx?.Source is BitmapSource fromImageEx)
            {
                return fromImageEx;
            }

            if (iconRoot is Image image && image.Source is BitmapSource fromImage)
            {
                return fromImage;
            }

            return CaptureElement(iconRoot);
        }

        /// <summary>
        /// Registers a shell drag image (replaces WPF's empty dashed rectangle). Returns true when applied.
        /// </summary>
        public static bool TryApplyShell(DataObject data, FrameworkElement iconRoot, Point dragHotspotInIcon)
        {
            if (data == null || iconRoot == null)
            {
                return false;
            }

            var intensity = AppDragVisualSettings.GetIntensity();
            var icon = TryGetIconBitmap(iconRoot);
            var comData = (ComIDataObject)data;

            if (icon != null)
            {
                var display = GetDisplaySize(iconRoot);
                var dragBitmap = CreateDragImageBitmap(iconRoot, icon, display, intensity, dragHotspotInIcon, out var hotspotX, out var hotspotY);
                if (dragBitmap != null && TryApplyShellFromBitmap(dragBitmap, hotspotX, hotspotY, comData))
                {
                    return true;
                }
            }

            return TryApplyShellFromWindow(iconRoot, dragHotspotInIcon, comData);
        }

        public static BitmapSource CreateDragImageBitmap(
            FrameworkElement iconRoot,
            BitmapSource icon,
            Size displaySizeDip,
            int intensity,
            Point dragHotspotInIcon,
            out int hotspotX,
            out int hotspotY)
        {
            hotspotX = 0;
            hotspotY = 0;

            if (icon == null)
            {
                return null;
            }

            GetScale(iconRoot, displaySizeDip, out var scaleX, out var scaleY, out var dpiX, out var dpiY);

            var paddingDip = intensity > 0 ? Math.Max(3, AppDragVisualSettings.GetGlowPaddingDip(intensity)) : 0;
            var totalWidthDip = displaySizeDip.Width + (paddingDip * 2);
            var totalHeightDip = displaySizeDip.Height + (paddingDip * 2);

            var pixelWidth = Math.Max(1, (int)Math.Round(totalWidthDip * scaleX));
            var pixelHeight = Math.Max(1, (int)Math.Round(totalHeightDip * scaleY));

            var visualRoot = BuildDragVisual(icon, displaySizeDip.Width, displaySizeDip.Height, intensity);
            visualRoot.Margin = new Thickness(paddingDip);
            var container = new Grid
            {
                Width = totalWidthDip,
                Height = totalHeightDip,
                Background = Brushes.Transparent,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
            };
            container.Children.Add(visualRoot);

            container.Measure(new Size(totalWidthDip, totalHeightDip));
            container.Arrange(new Rect(0, 0, totalWidthDip, totalHeightDip));

            var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, dpiX, dpiY, PixelFormats.Pbgra32);
            rtb.Render(container);
            rtb.Freeze();

            var hotspotDipX = dragHotspotInIcon.X + paddingDip;
            var hotspotDipY = dragHotspotInIcon.Y + paddingDip;
            hotspotX = (int)Math.Round(Clamp(hotspotDipX * scaleX, 0, pixelWidth - 1), MidpointRounding.AwayFromZero);
            hotspotY = (int)Math.Round(Clamp(hotspotDipY * scaleY, 0, pixelHeight - 1), MidpointRounding.AwayFromZero);

            return rtb;
        }

        internal static FrameworkElement BuildDragVisual(BitmapSource bitmap, double width, double height, int intensity)
        {
            AppDragVisualSettings.GetVisualParameters(
                intensity,
                out var haloOpacity,
                out var blurRadius,
                out var glowOpacity,
                out var glowBlurRadius,
                out _);

            var children = new List<UIElement>();

            if (blurRadius >= 0.5 && haloOpacity > 0.005)
            {
                var halo = new Image
                {
                    Source = bitmap,
                    Width = width,
                    Height = height,
                    Stretch = Stretch.Uniform,
                    IsHitTestVisible = false,
                    SnapsToDevicePixels = true,
                    Opacity = haloOpacity,
                    Effect = new BlurEffect
                    {
                        Radius = blurRadius,
                        RenderingBias = RenderingBias.Performance,
                    },
                };
                RenderOptions.SetBitmapScalingMode(halo, BitmapScalingMode.Fant);
                children.Add(halo);
            }

            var icon = new Image
            {
                Source = bitmap,
                Width = width,
                Height = height,
                Stretch = Stretch.Uniform,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
            };
            RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.Fant);

            if (glowOpacity > 0.005)
            {
                icon.Effect = new DropShadowEffect
                {
                    Color = Colors.White,
                    BlurRadius = glowBlurRadius,
                    ShadowDepth = 0,
                    Opacity = glowOpacity,
                };
            }

            children.Add(icon);

            var grid = new Grid
            {
                Width = width,
                Height = height,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
            };
            foreach (var child in children)
            {
                grid.Children.Add(child);
            }

            return grid;
        }

        private static bool TryApplyShellFromWindow(FrameworkElement iconRoot, Point dragStartInIcon, ComIDataObject comData)
        {
            var source = PresentationSource.FromVisual(iconRoot) as HwndSource;
            if (source?.Handle == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                var screen = iconRoot.PointToScreen(dragStartInIcon);
                var pt = new User32.POINT { x = (int)Math.Round(screen.X), y = (int)Math.Round(screen.Y) };
                if (!User32.ScreenToClient(source.Handle, ref pt))
                {
                    return false;
                }

                return DragSourceHelper.TrySetWindowDragImage(source.Handle, pt.x, pt.y, comData);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryApplyShellFromBitmap(BitmapSource bitmap, int hotspotX, int hotspotY, ComIDataObject comData)
        {
            var width = Math.Max(1, bitmap.PixelWidth);
            var height = Math.Max(1, bitmap.PixelHeight);

            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                hBitmap = CreateHBitmap(bitmap);
                if (hBitmap == IntPtr.Zero)
                {
                    return false;
                }

                return DragSourceHelper.TrySetBitmapDragImage(hBitmap, width, height, hotspotX, hotspotY, comData);
            }
            catch
            {
                return false;
            }
        }

        private static void GetScale(FrameworkElement iconRoot, Size displaySizeDip, out double scaleX, out double scaleY, out double dpiX, out double dpiY)
        {
            scaleX = 1.0;
            scaleY = 1.0;
            dpiX = 96.0;
            dpiY = 96.0;

            var source = iconRoot != null ? PresentationSource.FromVisual(iconRoot) : null;
            if (source?.CompositionTarget != null)
            {
                var transform = source.CompositionTarget.TransformToDevice;
                scaleX = transform.M11;
                scaleY = transform.M22;
                dpiX = 96.0 * scaleX;
                dpiY = 96.0 * scaleY;
            }
        }

        private static BitmapSource CaptureElement(FrameworkElement element)
        {
            try
            {
                element.UpdateLayout();
                var display = GetDisplaySize(element);

                GetScale(element, display, out var scaleX, out var scaleY, out var dpiX, out var dpiY);

                var pixelWidth = (int)Math.Max(1, Math.Round(display.Width * scaleX));
                var pixelHeight = (int)Math.Max(1, Math.Round(display.Height * scaleY));

                var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, dpiX, dpiY, PixelFormats.Pbgra32);
                rtb.Render(element);
                rtb.Freeze();
                return rtb;
            }
            catch
            {
                return null;
            }
        }

        private static IntPtr CreateHBitmap(BitmapSource source)
        {
            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            var width = converted.PixelWidth;
            var height = converted.PixelHeight;
            var stride = width * 4;
            var pixels = new byte[height * stride];
            converted.CopyPixels(pixels, stride, 0);

            using (var bitmap = new DrawingBitmap(width, height, DrawingPixelFormat.Format32bppPArgb))
            {
                var locked = bitmap.LockBits(new DrawingRectangle(0, 0, width, height), DrawingImageLockMode.WriteOnly, DrawingPixelFormat.Format32bppPArgb);
                try
                {
                    Marshal.Copy(pixels, 0, locked.Scan0, pixels.Length);
                }
                finally
                {
                    bitmap.UnlockBits(locked);
                }

                return bitmap.GetHbitmap(DrawingColor.FromArgb(0, 0, 0, 0));
            }
        }

        private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
    }
}
