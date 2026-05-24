using EarTrumpet.Extensions;
using EarTrumpet.UI.Helpers;
using EarTrumpet.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EarTrumpet.UI.Views
{
    public partial class AppItemView : UserControl
    {
        private IAppItemViewModel App => (IAppItemViewModel)DataContext;

        private Point? _dragStartPoint;
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

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || !IsPointerOverIcon(e.OriginalSource as DependencyObject))
            {
                return;
            }

            if (!AppDragDrop.CanDrag(App))
            {
                DevTrace.Write($"Drag blocked: {AppDragDrop.DescribeDragBlockReason(App)}");
                return;
            }

            _dragStartPoint = e.GetPosition(this);
            _isDragInProgress = false;
            DevTrace.Write($"Drag armed: {App.DisplayName}");
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragStartPoint == null || e.LeftButton != MouseButtonState.Pressed || _isDragInProgress)
            {
                return;
            }

            if (!AppDragDrop.CanDrag(App))
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

            DevTrace.Write($"Drag start: {App.DisplayName} (device {App.Parent?.Id})");
            try
            {
                var data = new DataObject(AppDragDrop.Format, App);
                var effect = DragDrop.DoDragDrop(this, data, DragDropEffects.Move);
                DevTrace.Write($"Drag end: {App.DisplayName} effect={effect}");
            }
            catch (Exception ex)
            {
                DevTrace.LogException($"DragDrop {App?.DisplayName}", ex);
            }
            finally
            {
                _isDragInProgress = false;
                AppDragDropFlyout.EndDrag();
            }
        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragStartPoint != null)
            {
                DevTrace.Write($"Drag cancelled (no threshold): {App?.DisplayName}");
            }

            _dragStartPoint = null;
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
            return System.Math.Abs(diff.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                   System.Math.Abs(diff.Y) >= SystemParameters.MinimumVerticalDragDistance;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.M:
                case Key.OemPeriod:
                    App.IsMuted = !App.IsMuted;
                    e.Handled = true;
                    break;
                case Key.Right:
                case Key.OemPlus:
                    App.Volume++;
                    e.Handled = true;
                    break;
                case Key.Left:
                case Key.OemMinus:
                    App.Volume--;
                    e.Handled = true;
                    break;
                case Key.Space:
                    OpenPopup();
                    e.Handled = true;
                    break;
            }
        }

        private void OpenPopup()
        {
            var viewModel = Window.GetWindow(this).DataContext as IPopupHostViewModel;
            if (viewModel != null && App != null && !App.IsExpanded)
            {
                viewModel.OpenPopup(App, this);
            }
        }
    }
}
