using CommunityToolkit.Mvvm.ComponentModel;

namespace DashDetective.Tabs.Hardware;

/// <summary>
/// One key/value spec row inside a hardware card (e.g. "Cores / Threads" → "24 / 32"). The
/// <see cref="Key"/> is fixed (it defines the row and is how the view model addresses it); the
/// <see cref="Value"/> is observable so a provider can fill it in after the async WMI read — it
/// starts as the neutral placeholder "—" and updates live once real data lands.
/// </summary>
public partial class HardwareSpec : ObservableObject {
    public string Key { get; }

    [ObservableProperty]
    private string _value;

    public HardwareSpec(string key, string value = "—") {
        Key = key;
        _value = value;
    }
}
