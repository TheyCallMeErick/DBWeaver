using System.ComponentModel;

namespace DBWeaver.UI.Services.Localization;

public interface ILocalizationService : INotifyPropertyChanged
{
    string CurrentCulture { get; }

    string CurrentLanguageLabel { get; }

    string this[string key] { get; }

    bool ToggleCulture();

    bool SetCulture(string culture);
}
