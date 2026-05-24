using EarTrumpet.UI.Helpers;

namespace EarTrumpet.UI.ViewModels
{
    public class EarTrumpetDragSettingsPageViewModel : SettingsPageViewModel
    {
        private readonly AppSettings _settings;

        public int AppDragVisualIntensity
        {
            get => _settings.AppDragVisualIntensity;
            set
            {
                if (_settings.AppDragVisualIntensity != value)
                {
                    _settings.AppDragVisualIntensity = value;
                    RaisePropertyChanged(nameof(AppDragVisualIntensity));
                    RaisePropertyChanged(nameof(IntensityLabel));
                }
            }
        }

        public string IntensityLabel => string.Format(
            Properties.Resources.SettingsAppDragVisualIntensityValue,
            AppDragVisualIntensity);

        public EarTrumpetDragSettingsPageViewModel(AppSettings settings) : base(null)
        {
            _settings = settings;
            Title = Properties.Resources.DragDropSettingsPageText;
            Glyph = "\uE8C5";
        }
    }
}
