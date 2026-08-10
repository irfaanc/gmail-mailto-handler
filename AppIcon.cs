using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace MailtoPicker;

/// <summary>
/// The app icon, loaded once from the embedded resource.
///
/// Read from the assembly rather than from a file beside the exe, because after
/// single-file publishing there is no such file: everything is bundled into the
/// executable.
/// </summary>
internal static class AppIcon
{
    private const string ResourceName = "MailtoPicker.app.ico";

    private static Icon? _icon;
    private static bool _tried;

    public static Icon? Value
    {
        get
        {
            if (_tried) return _icon;
            _tried = true;
            try
            {
                using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
                if (stream is not null) _icon = new Icon(stream);
            }
            catch
            {
                // A missing icon is not worth failing over; the window just gets
                // the WinForms default.
            }
            return _icon;
        }
    }

    /// <summary>Gives a window the app icon, if there is one.</summary>
    public static void Apply(Form form)
    {
        Icon? icon = Value;
        if (icon is not null) form.Icon = icon;
    }
}
