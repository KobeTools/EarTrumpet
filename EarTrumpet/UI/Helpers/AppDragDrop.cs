using EarTrumpet.UI.ViewModels;
using System.Windows;

namespace EarTrumpet.UI.Helpers
{
    public static class AppDragDrop
    {
        public const string Format = "EarTrumpet.AppItem";

        public static bool CanDrag(IAppItemViewModel app) =>
            app != null && app.IsMovable && !app.IsExpanded;

        public static bool TryGetApp(IDataObject data, out IAppItemViewModel app)
        {
            app = data?.GetData(Format) as IAppItemViewModel;
            return app != null;
        }

        public static bool CanDrop(IAppItemViewModel app, DeviceViewModel targetDevice)
        {
            if (!CanDrag(app) || targetDevice == null)
            {
                return false;
            }

            if (app.Parent is IDeviceViewModel parent && parent.Id == targetDevice.Id)
            {
                return false;
            }

            return true;
        }
    }
}
