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
    /// Flyout-wide drag/drop so drops work over app rows, sliders, and device chrome (WPF only
    /// hits AllowDrop elements under the cursor; hit-testing finds the owning DeviceView).
    /// </summary>
    public static class AppDragDropFlyout
    {
        private static DeviceView _highlightedDeviceView;
        private static string _lastRejectReason;

        public static void Attach(FrameworkElement root, Func<IPopupHostViewModel> getHost)
        {
            root.AllowDrop = true;
            root.PreviewDragOver += (_, e) => OnDragOver(root, getHost, e);
            root.PreviewDrop += (_, e) => OnDrop(root, getHost, e);
            root.PreviewDragLeave += (_, __) => ClearHighlight();
            root.PreviewDragEnter += (_, e) => OnDragOver(root, getHost, e);
        }

        private static void OnDragOver(FrameworkElement root, Func<IPopupHostViewModel> getHost, DragEventArgs e)
        {
            if (!AppDragDrop.TryGetApp(e.Data, out var app))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                LogRejectOnce("drag payload missing");
                ClearHighlight();
                return;
            }

            var deviceView = FindDeviceView(root, e.GetPosition(root));
            var device = deviceView?.Device;

            if (device != null && AppDragDrop.CanDrop(app, device))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
                SetHighlight(deviceView);
            }
            else
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                LogRejectOnce(AppDragDrop.DescribeDropRejectReason(app, device, deviceView != null));
                ClearHighlight();
            }
        }

        private static void OnDrop(FrameworkElement root, Func<IPopupHostViewModel> getHost, DragEventArgs e)
        {
            ClearHighlight();

            try
            {
                if (!AppDragDrop.TryGetApp(e.Data, out var app))
                {
                    DevTrace.Write("Drop ignored: no drag payload");
                    return;
                }

                var deviceView = FindDeviceView(root, e.GetPosition(root));
                var device = deviceView?.Device;
                if (device == null || !AppDragDrop.CanDrop(app, device))
                {
                    DevTrace.Write($"Drop ignored: {AppDragDrop.DescribeDropRejectReason(app, device, deviceView != null)}");
                    return;
                }

                var host = getHost();
                if (host == null)
                {
                    DevTrace.Write("Drop failed: no IPopupHostViewModel on window");
                    return;
                }

                DevTrace.Write($"Drop: {app.DisplayName} (AppId={app.AppId}, Pid={app.ProcessId}) -> {device.DisplayName} ({device.Id})");
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
            _lastRejectReason = null;
        }

        private static void ClearHighlight()
        {
            if (_highlightedDeviceView != null)
            {
                _highlightedDeviceView.IsDropTarget = false;
                _highlightedDeviceView = null;
            }
        }

        private static void LogRejectOnce(string reason)
        {
            if (string.IsNullOrEmpty(reason) || reason == _lastRejectReason)
            {
                return;
            }

            _lastRejectReason = reason;
            DevTrace.Write($"Drag over rejected: {reason}");
        }
    }
}
