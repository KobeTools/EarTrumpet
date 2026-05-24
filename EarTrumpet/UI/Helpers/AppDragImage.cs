using EarTrumpet.Extensions;
using EarTrumpet.Interop;
using EarTrumpet.UI.Controls;
using System;
using System.Runtime.InteropServices;
using ComIDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingColor = System.Drawing.Color;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingImageLockMode = System.Drawing.Imaging.ImageLockMode;

namespace EarTrumpet.UI.Helpers
{
    /// <summary>
    /// Optional Windows shell drag image (often ignored by WPF DoDragDrop). Use AppDragOverlay for the visible icon.
    /// </summary>
    public static class AppDragImage
    {
        public static void TryApplyShell(DataObject data, FrameworkElement iconRoot, System.Windows.Point dragStartInIcon)
        {
            if (data == null || iconRoot == null)
            {
                DevTrace.Write("AppDragImage: skipped (null args)");
                return;
            }

            var bitmap = TryGetIconBitmap(iconRoot);
            if (bitmap != null)
            {
                try
                {
                    data.SetData(DataFormats.Bitmap, bitmap);
                }
                catch (Exception ex)
                {
                    DevTrace.LogException("AppDragImage SetData Bitmap", ex);
                }
            }

            var comData = (ComIDataObject)data;
            if (TryApplyShellFromWindow(iconRoot, dragStartInIcon, comData))
            {
                return;
            }

            if (bitmap != null && TryApplyShellFromBitmap(bitmap, dragStartInIcon, comData))
            {
                return;
            }

            DevTrace.Write("AppDragImage: shell drag image not applied");
        }

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

        private static bool TryApplyShellFromWindow(FrameworkElement iconRoot, System.Windows.Point dragStartInIcon, ComIDataObject comData)
        {
            var source = PresentationSource.FromVisual(iconRoot) as HwndSource;
            if (source?.Handle == IntPtr.Zero)
            {
                DevTrace.Write("AppDragImage: no HWND for window snapshot");
                return false;
            }

            try
            {
                var screen = iconRoot.PointToScreen(dragStartInIcon);
                var pt = new User32.POINT { x = (int)Math.Round(screen.X), y = (int)Math.Round(screen.Y) };
                if (!User32.ScreenToClient(source.Handle, ref pt))
                {
                    DevTrace.Write("AppDragImage: ScreenToClient failed");
                    return false;
                }

                if (DragSourceHelper.TrySetWindowDragImage(source.Handle, pt.x, pt.y, comData))
                {
                    DevTrace.Write($"AppDragImage: shell window drag image at {pt.x},{pt.y}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                DevTrace.LogException("AppDragImage window", ex);
            }

            return false;
        }

        private static bool TryApplyShellFromBitmap(BitmapSource bitmap, System.Windows.Point dragStartInIcon, ComIDataObject comData)
        {
            var width = Math.Max(1, bitmap.PixelWidth);
            var height = Math.Max(1, bitmap.PixelHeight);
            var hotspotX = (int)Math.Round(Clamp(dragStartInIcon.X, 0, width - 1), MidpointRounding.AwayFromZero);
            var hotspotY = (int)Math.Round(Clamp(dragStartInIcon.Y, 0, height - 1), MidpointRounding.AwayFromZero);

            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                hBitmap = CreateHBitmap(bitmap);
                if (hBitmap == IntPtr.Zero)
                {
                    DevTrace.Write("AppDragImage: CreateHBitmap failed");
                    return false;
                }

                if (DragSourceHelper.TrySetBitmapDragImage(hBitmap, width, height, hotspotX, hotspotY, comData))
                {
                    DevTrace.Write($"AppDragImage: shell bitmap drag image {width}x{height}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                DevTrace.LogException("AppDragImage bitmap", ex);
            }

            return false;
        }

        private static BitmapSource CaptureElement(FrameworkElement element)
        {
            try
            {
                element.UpdateLayout();
                var display = GetDisplaySize(element);

                var dpiX = 96.0;
                var dpiY = 96.0;
                var scaleX = 1.0;
                var scaleY = 1.0;
                var source = PresentationSource.FromVisual(element);
                if (source?.CompositionTarget != null)
                {
                    var transform = source.CompositionTarget.TransformToDevice;
                    scaleX = transform.M11;
                    scaleY = transform.M22;
                    dpiX = 96.0 * scaleX;
                    dpiY = 96.0 * scaleY;
                }

                var pixelWidth = (int)Math.Max(1, Math.Round(display.Width * scaleX));
                var pixelHeight = (int)Math.Max(1, Math.Round(display.Height * scaleY));

                var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, dpiX, dpiY, PixelFormats.Pbgra32);
                rtb.Render(element);
                rtb.Freeze();
                return rtb;
            }
            catch (Exception ex)
            {
                DevTrace.LogException("AppDragImage CaptureElement", ex);
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
