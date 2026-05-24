using EarTrumpet.UI.Helpers;
using EarTrumpet.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace EarTrumpet.UI.Views
{
    public partial class DeviceView : UserControl
    {
        public static string DeviceListItemKey = "DeviceListItem";

        public DeviceViewModel Device { get { return (DeviceViewModel)GetValue(DeviceProperty); } set { SetValue(DeviceProperty, value); } }
        public static readonly DependencyProperty DeviceProperty =
            DependencyProperty.Register("Device", typeof(DeviceViewModel), typeof(DeviceView), new PropertyMetadata(new PropertyChangedCallback(DeviceChanged)));

        public bool IsDisplayNameVisible { get { return (bool)GetValue(IsDisplayNameVisibleProperty); } set { SetValue(IsDisplayNameVisibleProperty, value); } }
        public static readonly DependencyProperty IsDisplayNameVisibleProperty =
            DependencyProperty.Register("IsDisplayNameVisible", typeof(bool), typeof(DeviceView), new PropertyMetadata(true));

        public bool IsAppListVisible { get { return (bool)GetValue(IsAppListVisibleProperty); } set { SetValue(IsAppListVisibleProperty, value); } }
        public static readonly DependencyProperty IsAppListVisibleProperty =
            DependencyProperty.Register("IsAppListVisible", typeof(bool), typeof(DeviceView), new PropertyMetadata(true));

        public bool IsDropTarget
        {
            get => (bool)GetValue(IsDropTargetProperty);
            set => SetValue(IsDropTargetProperty, value);
        }

        public static readonly DependencyProperty IsDropTargetProperty =
            DependencyProperty.Register(nameof(IsDropTarget), typeof(bool), typeof(DeviceView), new PropertyMetadata(false));

        public DeviceView()
        {
            InitializeComponent();

            DeviceListItem.PreviewKeyDown += OnPreviewKeyDown;
            DeviceListItem.PreviewMouseRightButtonUp += (_, __) => OpenPopup();

            DragOver += OnDragOver;
            DragLeave += OnDragLeave;
            Drop += OnDrop;
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            UpdateDropTarget(e);
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            IsDropTarget = false;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            IsDropTarget = false;

            if (!AppDragDrop.TryGetApp(e.Data, out var app) || !AppDragDrop.CanDrop(app, Device))
            {
                return;
            }

            var host = Window.GetWindow(this)?.DataContext as IPopupHostViewModel;
            host?.MoveAppToDevice(app, Device);
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void UpdateDropTarget(DragEventArgs e)
        {
            if (AppDragDrop.TryGetApp(e.Data, out var app) && AppDragDrop.CanDrop(app, Device))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
                IsDropTarget = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                IsDropTarget = false;
            }
        }

        public void FocusAndRemoveFocusVisual()
        {
            DeviceListItem.Focus();
            RemoveFocusVisual(DeviceListItem);
        }

        private void RemoveFocusVisual(UIElement element)
        {
            var adornerLayer = AdornerLayer.GetAdornerLayer(element);
            var adorners = adornerLayer.GetAdorners(element);
            if (adorners != null)
            {
                foreach (var adorner in adorners)
                {
                    adornerLayer.Remove(adorner);
                }
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.M:
                case Key.OemPeriod:
                    Device.IsMuted = !Device.IsMuted;
                    e.Handled = true;
                    break;
                case Key.Right:
                case Key.OemPlus:
                    Device.Volume++;
                    e.Handled = true;
                    break;
                case Key.Left:
                case Key.OemMinus:
                    Device.Volume--;
                    e.Handled = true;
                    break;
                case Key.Space:
                    OpenPopup();
                    e.Handled = true;
                    break;
            }
        }

        private static void DeviceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (DeviceView)d;
            self.GridRoot.DataContext = self;
        }

        private void TouchSlider_TouchUp(object sender, TouchEventArgs e)
        {
            SystemSoundsHelper.PlayBeepSound.Execute(null);
        }

        private void TouchSlider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                SystemSoundsHelper.PlayBeepSound.Execute(null);
            }
        }

        private void OpenPopup()
        {
            var viewModel = Window.GetWindow(DeviceListItem).DataContext as IPopupHostViewModel;
            if (viewModel != null)
            {
                viewModel.OpenPopup(Device, DeviceListItem);
            }
        }
    }
}
