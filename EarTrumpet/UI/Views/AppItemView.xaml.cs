using EarTrumpet.Extensions;
using EarTrumpet.UI.Helpers;
using EarTrumpet.UI.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EarTrumpet.UI.Views
{
    public partial class AppItemView : UserControl
    {
        private Point? _dragStartPoint;
        private Point? _dragStartInIcon;
        private bool _isDragInProgress;

        public AppItemView()
        {
            InitializeComponent();

            PreviewMouseRightButtonUp += (_, __) => OpenPopup();
            PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            PreviewMouseMove += OnPreviewMouseMove;
            PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;

            Loaded += (_, __) =>
            {
                var container = this.FindVisualParent<ListViewItem>();
                if (container != null)
                {
                    container.PreviewKeyDown += OnPreviewKeyDown;
                }
            };
        }

        private bool TryGetApp(out IAppItemViewModel app)
        {
            app = DataContext as IAppItemViewModel;
            return app != null;
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || !IsPointerOverIcon(e.OriginalSource as DependencyObject))
            {
                return;
            }

            if (!TryGetApp(out var app) || !AppDragDrop.CanDrag(app))
            {
                return;
            }

            _dragStartPoint = e.GetPosition(this);
            _dragStartInIcon = e.GetPosition(IconDragSource);
            _isDragInProgress = false;
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragStartPoint == null || e.LeftButton != MouseButtonState.Pressed || _isDragInProgress)
            {
                return;
            }

            if (!TryGetApp(out var app) || !AppDragDrop.CanDrag(app))
            {
                _dragStartPoint = null;
                return;
            }

            var position = e.GetPosition(this);
            if (!HasExceededDragThreshold(_dragStartPoint.Value, position))
            {
                return;
            }

            _isDragInProgress = true;
            _dragStartPoint = null;

            var listDevice = this.FindVisualParent<DeviceView>()?.Device;
            var dragInfo = new AppDragInfo
            {
                App = app,
                ListDeviceId = listDevice?.Id,
            };

            var hotspot = _dragStartInIcon ?? new Point(IconDragSource.ActualWidth / 2, IconDragSource.ActualHeight / 2);
            var window = Window.GetWindow(this);
            var intensity = AppDragVisualSettings.GetIntensity();
            var iconOpacity = IconDragSource.Opacity;
            if (intensity > 0)
            {
                AppDragVisualSettings.GetVisualParameters(intensity, out _, out _, out _, out _, out var sourceIconOpacity);
                IconDragSource.Opacity = sourceIconOpacity;
            }
            GiveFeedback += OnGiveFeedback;
            try
            {
                var data = new DataObject(AppDragDrop.Format, dragInfo);
                var shellApplied = AppDragImage.TryApplyShell(data, IconDragSource, hotspot);
                using (shellApplied ? null : AppDragFlyoutHint.TryStart(window, IconDragSource, hotspot))
                {
                    DragDrop.DoDragDrop(this, data, DragDropEffects.Move);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"AppItemView DragDrop Failed: {ex}");
            }
            finally
            {
                GiveFeedback -= OnGiveFeedback;
                IconDragSource.Opacity = iconOpacity;
                _dragStartInIcon = null;
                _isDragInProgress = false;
                AppDragDropFlyout.EndDrag();
            }
        }

        private void OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            e.UseDefaultCursors = false;
            e.Handled = true;
        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = null;
            _dragStartInIcon = null;
            _isDragInProgress = false;
        }

        private static bool IsPointerOverIcon(DependencyObject source)
        {
            while (source != null)
            {
                if (source is Border border && border.Name == "IconDragSource")
                {
                    return true;
                }

                if (source is Grid grid && grid.Name == "IconCell")
                {
                    return true;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private static bool HasExceededDragThreshold(Point start, Point current)
        {
            var diff = start - current;
            return Math.Abs(diff.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                   Math.Abs(diff.Y) >= SystemParameters.MinimumVerticalDragDistance;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!TryGetApp(out var app))
            {
                return;
            }

            switch (e.Key)
            {
                case Key.M:
                case Key.OemPeriod:
                    app.IsMuted = !app.IsMuted;
                    e.Handled = true;
                    break;
                case Key.Right:
                case Key.OemPlus:
                    app.Volume++;
                    e.Handled = true;
                    break;
                case Key.Left:
                case Key.OemMinus:
                    app.Volume--;
                    e.Handled = true;
                    break;
                case Key.Space:
                    OpenPopup(app);
                    e.Handled = true;
                    break;
            }
        }

        private void OpenPopup()
        {
            if (TryGetApp(out var app))
            {
                OpenPopup(app);
            }
        }

        private void OpenPopup(IAppItemViewModel app)
        {
            var viewModel = Window.GetWindow(this).DataContext as IPopupHostViewModel;
            if (viewModel != null && app != null && !app.IsExpanded)
            {
                viewModel.OpenPopup(app, this);
            }
        }
    }
}
