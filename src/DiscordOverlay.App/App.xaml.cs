namespace DiscordOverlay.App;

/// <summary>
/// Holds the merged WPF-UI theme dictionaries and the shared styles; it has no
/// startup logic of its own. <see cref="Program"/> owns the entry point, builds
/// the generic host, and drives this — see the <c>Page</c> override for App.xaml
/// in the csproj, which is what stops the SDK generating a competing Main.
/// </summary>
public partial class App : System.Windows.Application
{
}
