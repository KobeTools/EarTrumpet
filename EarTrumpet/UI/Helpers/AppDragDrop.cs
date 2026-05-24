using EarTrumpet.UI.ViewModels;
using System.Windows;

namespace EarTrumpet.UI.Helpers
{
    public sealed class AppDragInfo
    {
        public IAppItemViewModel App { get; set; }

        /// <summary>Device section this row is shown under (not persisted routing parent).</summary>
        public string ListDeviceId { get; set; }
    }

    public static class AppDragDrop
    {
        public const string Format = "EarTrumpet.AppItem";

        public static bool CanDrag(IAppItemViewModel app) =>
            app != null && app.IsMovable && !app.IsExpanded;

        public static bool TryGetDragInfo(IDataObject data, out AppDragInfo info)
        {
            info = data?.GetData(Format) as AppDragInfo;
            return info?.App != null;
        }

        public static bool TryGetApp(IDataObject data, out IAppItemViewModel app)
        {
            if (TryGetDragInfo(data, out var info))
            {
                app = info.App;
                return true;
            }

            app = null;
            return false;
        }

        public static bool CanDrop(IAppItemViewModel app, DeviceViewModel targetDevice, string listDeviceId = null)
        {
            if (!CanDrag(app) || targetDevice == null)
            {
                return false;
            }

            var sourceId = listDeviceId ?? app.Parent?.Id;
            if (!string.IsNullOrEmpty(sourceId) && sourceId == targetDevice.Id)
            {
                return false;
            }

            return true;
        }
    }
}
