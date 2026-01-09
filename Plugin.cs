using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;

using XIVKeyboard.Windows;

namespace XIVKeyboard;

public sealed class Plugin : IDalamudPlugin
{
    // ----------------------------
    // Plugin services
    // ----------------------------

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;
    [PluginService] internal static IGamepadState GamepadState { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    // ----------------------------
    // Constants & state
    // ----------------------------

    private const string CommandName = "/pxivkb";

    public Configuration Configuration { get; }

    private readonly WindowSystem windowSystem = new("XIV Keyboard");
    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;

    private bool lastEnterDown;

    // ----------------------------
    // Constructor
    // ----------------------------

    public Plugin()
    {
        Configuration =
            PluginInterface.GetPluginConfig() as Configuration
            ?? new Configuration();

        // Create windows (NON-null, fixes CS8604)
        mainWindow = new MainWindow(this);
        configWindow = new ConfigWindow(this);

        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(configWindow);

        // Slash command
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle XIV Keyboard (controller on-screen keyboard)."
        });

        // UI hooks (validator-required)
        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;

        // Framework tick
        Framework.Update += OnFrameworkUpdate;

        NotificationManager.AddNotification(new Dalamud.Interface.ImGuiNotification.Notification
        {
            Title = "XIV Keyboard",
            Content = "On-screen keyboard loaded successfully.",
            Type = Dalamud.Interface.ImGuiNotification.NotificationType.Success
        });

        Log.Information($"[XIVKeyboard] Loaded {PluginInterface.Manifest.Name}");
    }

    // ----------------------------
    // UI callbacks (validator wants these)
    // ----------------------------

    private void OpenMainUi() => mainWindow.Open();
    private void OpenConfigUi() => configWindow.IsOpen = true;

    private void DrawUI()
    {
        // Hard rule: capture only while keyboard is open
        SetGamepadCapture(mainWindow.IsOpen);
        windowSystem.Draw();
    }

    // ----------------------------
    // Input handling
    // ----------------------------

    private void OnFrameworkUpdate(IFramework framework)
    {
        bool enterDown = KeyState[VirtualKey.RETURN];

        if (enterDown && !lastEnterDown)
        {
            if (mainWindow.IsOpen)
                mainWindow.Close();
            else
                mainWindow.Open();
        }

        lastEnterDown = enterDown;
    }

    public void SetGamepadCapture(bool enabled)
    {
        var io = ImGui.GetIO();

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

    // ----------------------------
    // Commands
    // ----------------------------

    private void OnCommand(string command, string args)
    {
        if (mainWindow.IsOpen)
            mainWindow.Close();
        else
            mainWindow.Open();
    }

    // ----------------------------
    // Disposal
    // ----------------------------

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;

        Framework.Update -= OnFrameworkUpdate;

        SetGamepadCapture(false);

        windowSystem.RemoveAllWindows();

        mainWindow.Dispose();
        configWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }
}
