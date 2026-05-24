using EarTrumpet.UI.ViewModels;
using System.Windows;

namespace EarTrumpet.UI.Helpers
{
    public static class AppDragDrop
    {
        public const string Format = "EarTrumpet.AppItem";

        public static bool CanDrag(IAppItemViewModel app) =>
            app != null && app.IsMovable && !app.IsExpanded;

        public static string DescribeDragBlockReason(IAppItemViewModel app)
        {
            if (app == null)
            {
                return "no app";
            }

            if (!app.IsMovable)
            {
                return $"{app.DisplayName} is not movable (system sounds or OS < RS4)";
            }

            if (app.IsExpanded)
            {
                return $"{app.DisplayName} is a child session row";
            }

            return null;
        }

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

        public static string DescribeDropRejectReason(IAppItemViewModel app, DeviceViewModel targetDevice, bool overDeviceSection)
        {
            if (app == null)
            {
                return "no app in drag payload";
            }

            if (!overDeviceSection)
            {
                return "not over a device section (try the device header or app list area)";
            }

            if (targetDevice == null)
            {
                return "over device UI but no DeviceViewModel";
            }

            if (!CanDrag(app))
            {
                return DescribeDragBlockReason(app);
            }

            if (app.Parent is IDeviceViewModel parent && parent.Id == targetDevice.Id)
            {
                return $"already on output device '{targetDevice.DisplayName}'";
            }

            return "unknown";
        }
    }
}
