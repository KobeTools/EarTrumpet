using EarTrumpet.Extensions;
using EarTrumpet.UI.Helpers;
using EarTrumpet.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EarTrumpet.UI.Views
{
    public partial class AppItemView : UserControl
    {
        private IAppItemViewModel App => (IAppItemViewModel)DataContext;

        private Point? _dragStartPoint;
        private bool _dragStarted;

        public AppItemView()
        {
            InitializeComponent();

            PreviewMouseRightButtonUp += (_, __) => OpenPopup();

            IconDragSource.PreviewMouseLeftButtonDown += OnIconPreviewMouseLeftButtonDown;
            IconDragSource.PreviewMouseMove += OnIconPreviewMouseMove;
            IconDragSource.PreviewMouseLeftButtonUp += OnIconPreviewMouseLeftButtonUp;
            IconDragSource.LostMouseCapture += (_, __) => _dragStartPoint = null;

            Loaded += (_, __) =>
            {
                var container = this.FindVisualParent<ListViewItem>();
                if (container != null)
                {
                    container.PreviewKeyDown += OnPreviewKeyDown;
                }
            };
        }

        private void OnIconPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || !AppDragDrop.CanDrag(App))
            {
                return;
            }

            _dragStartPoint = e.GetPosition(this);
            _dragStarted = false;
            IconDragSource.CaptureMouse();
            e.Handled = false;
        }

        private void OnIconPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragStartPoint == null || !AppDragDrop.CanDrag(App))
            {
                return;
            }

            if (_dragStarted)
            {
                return;
            }

            var position = e.GetPosition(this);
            if (!HasExceededDragThreshold(_dragStartPoint.Value, position))
            {
                return;
            }

            _dragStarted = true;
            IconDragSource.ReleaseMouseCapture();
            _dragStartPoint = null;

            var data = new DataObject(AppDragDrop.Format, App);
            DragDrop.DoDragDrop(this, data, DragDropEffects.Move);
            _dragStarted = false;
        }

        private void OnIconPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = null;
            _dragStarted = false;
            if (IconDragSource.IsMouseCaptured)
            {
                IconDragSource.ReleaseMouseCapture();
            }
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
