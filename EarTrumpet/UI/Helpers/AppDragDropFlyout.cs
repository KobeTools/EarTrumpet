using EarTrumpet.Extensions;
using EarTrumpet.UI.ViewModels;
using EarTrumpet.UI.Views;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EarTrumpet.UI.Helpers
{
    /// <summary>
    /// Optional flyout-wide drop assist (highlights + drop on gaps). Does not replace DeviceView drop targets.
    /// </summary>
    public static class AppDragDropFlyout
    {
        private static DeviceView _highlightedDeviceView;

        public static void Attach(FrameworkElement root, Func<IPopupHostViewModel> getHost)
        {
            root.AllowDrop = true;
            root.PreviewDragOver += (_, e) => OnDragOver(root, e);
            root.PreviewDrop += (_, e) => OnDrop(root, getHost, e);
            root.PreviewDragLeave += (_, __) => ClearHighlight();
        }

        private static void OnDragOver(FrameworkElement root, DragEventArgs e)
        {
            if (!AppDragDrop.TryGetDragInfo(e.Data, out var dragInfo))
            {
                ClearHighlight();
                return;
            }

            var app = dragInfo.App;
            var deviceView = FindDeviceView(root, e.GetPosition(root));
            var device = deviceView?.Device;

            if (device != null && AppDragDrop.CanDrop(app, device, dragInfo.ListDeviceId))
            {
                SetHighlight(deviceView);
            }
            else
            {
                ClearHighlight();
            }
        }

        private static void OnDrop(FrameworkElement root, Func<IPopupHostViewModel> getHost, DragEventArgs e)
        {
            ClearHighlight();

            if (e.Handled)
            {
                return;
            }

            try
            {
                if (!AppDragDrop.TryGetDragInfo(e.Data, out var dragInfo))
                {
                    return;
                }

                var app = dragInfo.App;
                var deviceView = FindDeviceView(root, e.GetPosition(root));
                var device = deviceView?.Device;
                if (device == null || !AppDragDrop.CanDrop(app, device, dragInfo.ListDeviceId))
                {
                    return;
                }

                var host = getHost();
                if (host == null)
                {
                    return;
                }

                host.MoveAppToDevice(app, device);
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
            catch (Exception ex)
            {
                DevTrace.LogException("Flyout drop", ex);
            }
        }

        private static DeviceView FindDeviceView(FrameworkElement root, Point position)
        {
            var hit = VisualTreeHelper.HitTest(root, position);
            if (hit?.VisualHit == null)
            {
                return null;
            }

            return hit.VisualHit.FindVisualParent<DeviceView>();
        }

        private static void SetHighlight(DeviceView deviceView)
        {
            if (_highlightedDeviceView == deviceView)
            {
                return;
            }

            ClearHighlight();
            _highlightedDeviceView = deviceView;
            _highlightedDeviceView.IsDropTarget = true;
        }

        public static void EndDrag()
        {
            ClearHighlight();
        }

        private static void ClearHighlight()
        {
            if (_highlightedDeviceView != null)
            {
                _highlightedDeviceView.IsDropTarget = false;
                _highlightedDeviceView = null;
            }
        }
    }
}
