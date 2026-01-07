using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SamplePlugin.Windows;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;

namespace SamplePlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;
    [PluginService] internal static IGamepadState GamepadState { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    private const string CommandName = "/posk";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("On Screen Keyboard");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private bool lastEnterDown;

    private void OnFrameworkUpdate(IFramework framework)
    {
        // only care about Enter key on the physical keyboard
        bool enterDown = KeyState[VirtualKey.RETURN];

        // rising edge = just pressed this frame
        if (enterDown && !lastEnterDown)
        {
            // Toggle the keyboard window
            MainWindow.IsOpen = !MainWindow.IsOpen;

            // Optionally: only capture gamepad when open
            SetGamepadCapture(MainWindow.IsOpen);
        }

        lastEnterDown = enterDown;
    }

    public void SetGamepadCapture(bool enabled)
    {
        var io = ImGui.GetIO();

        // Always start from "off" state for our flags, then apply what we need.
        io.ConfigFlags &= ~ImGuiConfigFlags.NavEnableGamepad;
        io.ConfigFlags &= ~ImGuiConfigFlags.NoMouse;
        io.ConfigFlags &= ~ImGuiConfigFlags.NoMouseCursorChange;

        if (enabled)
        {
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
            io.ConfigFlags |= ImGuiConfigFlags.NoMouse;
            io.ConfigFlags |= ImGuiConfigFlags.NoMouseCursorChange;
        }
    }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // You might normally want to embed resources and load them from the manifest stream
        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, goatImagePath);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        NotificationManager.AddNotification(new Dalamud.Interface.ImGuiNotification.Notification
        {
            Title = "On Screen Keyboard",
            Content = "On Screen keyboard loaded successfully",
            Type = Dalamud.Interface.ImGuiNotification.NotificationType.Success
        });

        Framework.Update += OnFrameworkUpdate;

        if (!ClientState.IsLoggedIn)
            return;

        if (ImGui.GetIO().WantTextInput)
            return;

        // Replace that with a custom draw method so we can tick first
        PluginInterface.UiBuilder.Draw += DrawUI;


        // This adds a button to the plugin installer entry of this plugin which allows
        // toggling the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        // Adds another button doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [SamplePlugin] ===A cool log message from Sample Plugin===
        Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");
    }

    public void Dispose()
    {
        // Unregister all actions to not leak anything during disposal of plugin
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        SetGamepadCapture(false);
        Framework.Update -= OnFrameworkUpdate;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }
    private void DrawUI()
    {
        // Hard rule: capture = window open
        SetGamepadCapture(MainWindow.IsOpen);

        WindowSystem.Draw();
    }


    private void OnCommand(string command, string args)
    {
        // In response to the slash command, toggle the display status of our main ui
        MainWindow.Toggle();
    }
    
    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();

}
