using System;
using System.Runtime.InteropServices;

namespace EarTrumpet.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    struct SHDRAGIMAGE
    {
        public SIZE sizeDragImage;
        public User32.POINT ptOffset;
        public IntPtr hbmpDragImage;
        public int crColorKey;
    }

    [ComImport]
    [Guid("8836F730-F5F7-48fe-B0F8-20E04A03F3DC")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDragSourceHelper
    {
        void InitializeFromBitmap(ref SHDRAGIMAGE dragImage, [MarshalAs(UnmanagedType.Interface)] object dataObject);
        void InitializeFromWindow(IntPtr hwnd, ref User32.POINT origin, [MarshalAs(UnmanagedType.Interface)] object dataObject);
    }

    [ComImport]
    [Guid("4E16BA94-0B59-11D3-9A04-0060976A04E0")]
    class DragDropHelper
    {
    }

    static class DragSourceHelper
    {
        private const int ClrNone = -1;

        public static bool TrySetBitmapDragImage(IntPtr hBitmap, int width, int height, int hotspotX, int hotspotY, System.Runtime.InteropServices.ComTypes.IDataObject dataObject)
        {
            if (hBitmap == IntPtr.Zero || dataObject == null)
            {
                return false;
            }

            try
            {
                var helper = (IDragSourceHelper)new DragDropHelper();
                var dragImage = new SHDRAGIMAGE
                {
                    sizeDragImage = new SIZE { cx = width, cy = height },
                    ptOffset = new User32.POINT { x = hotspotX, y = hotspotY },
                    hbmpDragImage = hBitmap,
                    crColorKey = ClrNone,
                };

                helper.InitializeFromBitmap(ref dragImage, dataObject);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                Gdi32.DeleteObject(hBitmap);
            }
        }

        public static bool TrySetWindowDragImage(IntPtr hwnd, int originX, int originY, System.Runtime.InteropServices.ComTypes.IDataObject dataObject)
        {
            if (hwnd == IntPtr.Zero || dataObject == null)
            {
                return false;
            }

            try
            {
                var helper = (IDragSourceHelper)new DragDropHelper();
                var origin = new User32.POINT { x = originX, y = originY };
                helper.InitializeFromWindow(hwnd, ref origin, dataObject);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
