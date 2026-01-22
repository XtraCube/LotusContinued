using VentLib.Options;

namespace Lotus.Options.Client;

public class AdvancedOptions
{
    public AdvancedOptions()
    {
        OptionManager defaultManager = OptionManager.GetManager(file: "advanced.txt", managerFlags: OptionManagerFlags.IgnorePreset);
    }
}