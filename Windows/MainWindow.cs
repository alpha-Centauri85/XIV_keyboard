using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.Gui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    public bool WantsInput { get; private set; } = false;
    private string typedText = string.Empty;
    private readonly Plugin plugin;

    // --- Key rows (base letters are stored uppercase; display/output depends on capsEnabled) ---
    private readonly string[] rowPunct = { ".", ",", "/", "?", "-", "_", "<", ">" };
    private readonly string[] rowNums = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
    private readonly string[] rowQ = { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" };
    private readonly string[] rowA = { "A", "S", "D", "F", "G", "H", "J", "K", "L" };
    private readonly string[] rowZ = { "Z", "X", "C", "V", "B", "N", "M" };

    // Caps state (acts like shift)
    private bool capsEnabled = false;

    // Input timing for delete repeat (B/Circle hold)
    private bool deleteHeldLastFrame = false;
    private long deleteHoldStartMs = 0;
    private long lastDeleteRepeatMs = 0;

    // Repeat tuning
    private const int DeleteInitialDelayMs = 350;
    private const int DeleteRepeatMs = 55;

    private int selectedRow = 0;
    private int selectedCol = 0;
    private bool lastChatOpen = false;

    private const float KeyW = 50f;
    private float KeyH = 40f; // dynamic, computed each frame
    private static float Clamp(float v, float min, float max)
    => v < min ? min : (v > max ? max : v);


    private float RowWidth(int keyCount)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        return (keyCount * KeyW) + ((keyCount - 1) * spacing);
    }
    private float KeyWidthForRow(int keyCount)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var avail = ImGui.GetContentRegionAvail().X;

        var totalSpacing = spacing * (keyCount - 1);
        return (avail - totalSpacing) / keyCount;
    }


    private bool pressedThisFrame;
    private bool allowMouseClicks = false;
    private void DrawNavKey(string label, int navRow, int navCol, float width)
    {
        bool selected = (selectedRow == navRow && selectedCol == navCol);

        if (selected)
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.7f, 1f, 1f));

        ImGui.Button(label, new Vector2(width, KeyH));

        if (selected)
            ImGui.PopStyleColor();

        if (allowMouseClicks && ImGui.IsItemClicked())
        {
            selectedRow = navRow;
            selectedCol = navCol;
            PressKey(label);
        }
    }

    public void SetGamepadCapture(bool enabled)
    {
        var io = ImGui.GetIO();

        if (enabled)
        {
            // ✅ Capture the gamepad so FFXIV doesn't see face buttons
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;

            // ✅ Disable mouse interaction with ImGui while keyboard is open
            io.ConfigFlags |= ImGuiConfigFlags.NoMouse;
            io.ConfigFlags |= ImGuiConfigFlags.NoMouseCursorChange;
        }
        else
        {
            io.ConfigFlags &= ~ImGuiConfigFlags.NavEnableGamepad;
            io.ConfigFlags &= ~ImGuiConfigFlags.NoMouse;
            io.ConfigFlags &= ~ImGuiConfigFlags.NoMouseCursorChange;
        }
    }

    private void DrawKeyButton(string label, float width)
    {
        ImGui.Button(label, new Vector2(width, KeyH));

        if (allowMouseClicks && ImGui.IsItemClicked())
            PressKey(label);
    }
    private void DrawLetterKey(string label, int row, int col)
    {
        bool selected = row == selectedRow && col == selectedCol;

        if (selected)
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.7f, 1f, 1f));

        ImGui.Button(label, new Vector2(KeyW, KeyH));

        if (selected)
            ImGui.PopStyleColor();

        // Only allow mouse clicks if enabled
        if (allowMouseClicks && ImGui.IsItemClicked())
        {
            selectedRow = row;
            selectedCol = col;
            PressKey(label);
        }
    }

    private long lastToggleMs = 0;

    private bool CanToggle(int cooldownMs = 250)
    {
        var now = Environment.TickCount64;
        if (now - lastToggleMs < cooldownMs) return false;
        lastToggleMs = now;
        return true;
    }
        private void DeleteOnce()
    {
        if (typedText.Length > 0)
            typedText = typedText[..^1];
    }

    private void PressKey(string key)
    {
        if (pressedThisFrame)
            return;

        pressedThisFrame = true;

        switch (key)
        {
            case "Space":
                typedText += " ";
                break;

            case "Delete":
                DeleteOnce();
                break;

            case "Paste":
                if (!string.IsNullOrWhiteSpace(typedText))
                {
                    ImGui.SetClipboardText(typedText);
                    Plugin.ChatGui.Print("[Keyboard] Copied. Paste (Ctrl+V) then Enter.");
                }
                break;

            case "Enter":
                if (!string.IsNullOrWhiteSpace(typedText))
                {
                    ImGui.SetClipboardText(typedText);
                    Plugin.ChatGui.Print("[Keyboard] Copied. Paste (Ctrl+V) then Enter.");
                    typedText = string.Empty;
                }

                IsOpen = false;
                plugin.SetGamepadCapture(false);
                break;

            default:
                // Letters: apply caps
                if (key.Length == 1 && char.IsLetter(key[0]))
                {
                    if (capsEnabled)
                    {
                        typedText += key;          // uppercase
                        capsEnabled = false;       // auto-reset after one char
                    }
                    else
                    {
                        typedText += key.ToLowerInvariant();
                    }
                }

                else
                {
                    // Numbers and anything else
                    typedText += key;
                }
                break;
        }
    }

    //private bool controllerMode = true; 

    private void BeginCenteredRow(int keyCount)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        var rowW = RowWidth(keyCount);
        var indent = MathF.Max(0, (avail - rowW) * 0.5f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + indent);
    }
        private void BeginRightAlignedRow(float rowWidth)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        var indent = MathF.Max(0, avail - rowWidth);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + indent);
    }


    public void TickAutoOpen()
    {
        // your existing chat check
        bool chatOpen = IsChatOpen();

        // open on rising edge
        if (chatOpen && !lastChatOpen)
        {
            IsOpen = true;
            plugin.SetGamepadCapture(true);
        }

        // close on falling edge
        if (!chatOpen && lastChatOpen)
        {
            IsOpen = false;
            plugin.SetGamepadCapture(false);
        }

        lastChatOpen = chatOpen;
    }
    private long lastMoveMs = 0;
    private bool CanMove(int cooldownMs = 140)
    {
        var now = Environment.TickCount64;
        if (now - lastMoveMs < cooldownMs) return false;
        lastMoveMs = now;
        return true;
    }
    private bool IsChatOpen()
    {
        var chat = Plugin.GameGui.GetAddonByName("ChatLog", 1);

        // AtkUnitBasePtr supports truthiness / null-like checks depending on version.
        // These two lines are the safest pattern:
        if (chat == null)
            return false;

        return chat.IsVisible;
    }
    private bool AddonExists(string name)
    {
        var a = Plugin.GameGui.GetAddonByName(name, 1);
        return a != null && !a.IsNull && a.IsReady;
    }

    private void DumpChatLogAtkValues()
    {
        var chat = Plugin.GameGui.GetAddonByName("ChatLog", 1);

        if (chat == null || chat.IsNull)
        {
            Plugin.Log.Information("ChatLog: null");
            return;
        }

        Plugin.Log.Information($"ChatLog: Ready={chat.IsReady} Visible={chat.IsVisible} AtkValuesCount={chat.AtkValuesCount}");

        int i = 0;
        foreach (var v in chat.AtkValues)
        {
            if (i >= 60) break;

            object? value;
            try { value = v.GetValue(); }
            catch { value = "<unhandled>"; }

            Plugin.Log.Information($"  [{i}] Type={v.ValueType} Value={value}");
            i++;
        }
    }
    private void DumpConditions(string label)
    {
        var set = Plugin.Condition.AsReadOnlySet();
        Plugin.Log.Information($"== Conditions ({label}) ==");
        foreach (var f in set)
            Plugin.Log.Information($"  {f}");
    }

    //private bool keyboardVisible = false;
    private void DrawTypedText()
    {
        ImGui.TextUnformatted("Typing:");

        // Height scales with key height
        var boxH = MathF.Max(36f, KeyH * 1.15f);

        // Full width, scaled height
        var size = new Vector2(ImGui.GetContentRegionAvail().X, boxH);

        if (string.IsNullOrEmpty(typedText))
        {
            ImGui.TextDisabled("Type with the controller keyboard below…");
            ImGui.SameLine();
        }

        // Multiline gives control over height
        ImGui.InputTextMultiline(
            "##typedText",
            ref typedText,
            1024,
            size,
            ImGuiInputTextFlags.ReadOnly | ImGuiInputTextFlags.NoHorizontalScroll
        );

        ImGui.Separator();
    }


    private void SendToChat(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (text.StartsWith("/"))
        {
            Plugin.CommandManager.ProcessCommand(text);
        }

        else
        {
            Plugin.ChatGui.Print($"[Keyboard] {text}");
        }
    }

    private int NavRowCount => 8;

    private int GetNavRowLength(int row)
    {
        return row switch
        {
            0 => 1,                 // Delete
            1 => rowPunct.Length,   // . , / ? - _ < >
            2 => rowNums.Length,    // 1-0
            3 => rowQ.Length,       // Q row
            4 => rowA.Length,       // A row
            5 => rowZ.Length,       // Z row
            6 => 1,                 // Space
            7 => 2,                 // Paste, Enter
            _ => 1
        };
    }

    private string GetNavKey(int row, int col)
    {
        return row switch
        {
            0 => "Delete",
            1 => rowPunct[col],
            2 => rowNums[col],
            3 => rowQ[col],
            4 => rowA[col],
            5 => rowZ[col],
            6 => "Space",
            7 => col == 0 ? "Paste" : "Enter",
            _ => "Space"
        };
    }


    private void HandleDeleteHoldRepeat()
    {
        bool down = ImGui.IsKeyDown(ImGuiKey.GamepadFaceRight);
        var now = Environment.TickCount64;

        if (down && !deleteHeldLastFrame)
        {
            // initial press
            DeleteOnce();
            deleteHoldStartMs = now;
            lastDeleteRepeatMs = now;
        }
        else if (down)
        {
            // after initial delay, repeat at interval
            if (now - deleteHoldStartMs >= DeleteInitialDelayMs &&
                now - lastDeleteRepeatMs >= DeleteRepeatMs)
            {
                DeleteOnce();
                lastDeleteRepeatMs = now;
            }
        }

        deleteHeldLastFrame = down;
    }

    private void HandleGamepadNavigation()
    {
        foreach (ImGuiKey key in Enum.GetValues(typeof(ImGuiKey)))
        {
            if (key.ToString().StartsWith("Gamepad") && ImGui.IsKeyPressed(key))
            {
                Plugin.Log.Information($"Gamepad key pressed: {key}");
            }
        }

        if (!IsOpen)
            return;

        // RB => Shift (next character uppercase)
        if (ImGui.IsKeyPressed(ImGuiKey.GamepadR1))
        {
            capsEnabled = true;
        }


        // D-pad navigation across the nav grid
        if (ImGui.IsKeyPressed(ImGuiKey.GamepadDpadLeft) && CanMove()) selectedCol--;
        else if (ImGui.IsKeyPressed(ImGuiKey.GamepadDpadRight) && CanMove()) selectedCol++;
        else if (ImGui.IsKeyPressed(ImGuiKey.GamepadDpadUp) && CanMove()) selectedRow--;
        else if (ImGui.IsKeyPressed(ImGuiKey.GamepadDpadDown) && CanMove()) selectedRow++;

        // Clamp to current row length
        selectedRow = Math.Clamp(selectedRow, 0, NavRowCount - 1);
        selectedCol = Math.Clamp(selectedCol, 0, GetNavRowLength(selectedRow) - 1);

        // --- Dedicated button binds ---
        // LB => Space
        if (ImGui.IsKeyPressed(ImGuiKey.GamepadL1))
            PressKey("Space");

        // West (X/Square) => Enter
        if (ImGui.IsKeyPressed(ImGuiKey.GamepadFaceLeft))
            PressKey("Enter");

        // East (B/Circle) => Delete (hold-repeat)
        HandleDeleteHoldRepeat();

        // South (A/Cross) => press selected key
        if (ImGui.IsKeyPressed(ImGuiKey.GamepadFaceDown))
        {
            var key = GetNavKey(selectedRow, selectedCol);
            PressKey(key);
        }
    }

    private string DisplayKey(string baseKey)
    {
        // baseKey is uppercase A-Z; display depends on capsEnabled
        if (baseKey.Length == 1 && char.IsLetter(baseKey[0]))
            return capsEnabled ? baseKey : baseKey.ToLowerInvariant();

        return baseKey;
    }

    private void DrawNavKey(string label, int navRow, int navCol, float width, string? actualKey = null)
    {
        using (ImRaii.PushId($"{navRow}:{navCol}"))
        {

            bool selected = (selectedRow == navRow && selectedCol == navCol);

            if (selected)
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.7f, 1f, 1f));

            ImGui.ButtonEx(label, new Vector2(width, KeyH), ImGuiButtonFlags.NoNavFocus);

            if (selected)
                ImGui.PopStyleColor();

            // Mouse is disabled in your plugin capture, but keep this safe anyway
            if (allowMouseClicks && ImGui.IsItemClicked())
            {
                selectedRow = navRow;
                selectedCol = navCol;
                PressKey(actualKey ?? label);
            }
        }
    }

    private void DrawKeyboardGrid()
    {
        // --- Delete row (nav row 0), right aligned above top rows ---
        var topRowW = RowWidth(10);
        BeginRightAlignedRow(topRowW);
        var deleteW = KeyWidthForRow(10) * 1.7f;
        DrawNavKey("Delete", navRow: 0, navCol: 0, width: deleteW);
        ImGui.Dummy(new Vector2(0, 2f));

        // --- Punctuation row (nav row 1) ---
        var punctW = KeyWidthForRow(rowPunct.Length);
        for (int col = 0; col < rowPunct.Length; col++)
        {
            DrawNavKey(rowPunct[col], navRow: 1, navCol: col, width: punctW);
            if (col < rowPunct.Length - 1) ImGui.SameLine();
        }
        ImGui.Dummy(new Vector2(0, 2f));

        // --- Numbers row (nav row 2) ---
        var numKeyW = KeyWidthForRow(rowNums.Length);

        for (int col = 0; col < rowNums.Length; col++)
        {
            DrawNavKey(rowNums[col], navRow: 1, navCol: col, width: numKeyW);
            if (col < rowNums.Length - 1) ImGui.SameLine();
        }

        ImGui.Dummy(new Vector2(0, 2f));

        // --- Q row (nav row 3) ---
        var qKeyW = KeyWidthForRow(rowQ.Length);
        for (int col = 0; col < rowQ.Length; col++)
        {
            DrawNavKey(
                DisplayKey(rowQ[col]),
                navRow: 2,
                navCol: col,
                width: qKeyW,
                actualKey: rowQ[col]
            );
            if (col < rowQ.Length - 1) ImGui.SameLine();
        }

        ImGui.Dummy(new Vector2(0, 2f));

        // --- A row (nav row 4) ---
        var aKeyW = KeyWidthForRow(rowA.Length);
        for (int col = 0; col < rowA.Length; col++)
        {
            DrawNavKey(
                DisplayKey(rowA[col]),
                navRow: 3,
                navCol: col,
                width: aKeyW,
                actualKey: rowA[col]
            );
            if (col < rowA.Length - 1) ImGui.SameLine();
        }

        ImGui.Dummy(new Vector2(0, 2f));

        // --- Z row (nav row 5) ---
        var zKeyW = KeyWidthForRow(rowZ.Length);
        for (int col = 0; col < rowZ.Length; col++)
        {
            DrawNavKey(
                DisplayKey(rowZ[col]),
                navRow: 4,
                navCol: col,
                width: zKeyW,
                actualKey: rowZ[col]
            );
            if (col < rowZ.Length - 1) ImGui.SameLine();
        }

        ImGui.Dummy(new Vector2(0, 6f));

        // --- Space row (nav row 6) ---
        DrawNavKey("Space", navRow: 5, navCol: 0, width: ImGui.GetContentRegionAvail().X);
        ImGui.Dummy(new Vector2(0, 6f));

        // --- Paste + Enter (nav row 7)
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var avail = ImGui.GetContentRegionAvail().X;
        var actionKeyW = (avail - spacing) / 2f;

        DrawNavKey("Paste", navRow: 6, navCol: 0, width: actionKeyW);
        ImGui.SameLine();
        DrawNavKey("Enter", navRow: 6, navCol: 1, width: actionKeyW);
    }

    public MainWindow(Plugin plugin, string goatImagePath)
        : base("On Screen Keyboard for Controllers##With a hidden ID",
            ImGuiWindowFlags.NoScrollbar
          | ImGuiWindowFlags.NoScrollWithMouse
          | ImGuiWindowFlags.NoCollapse
          | ImGuiWindowFlags.NoTitleBar)

    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        pressedThisFrame = false;

        plugin.SetGamepadCapture(IsOpen);

        if (ImGui.IsWindowAppearing())
            ImGui.SetWindowFocus();

        // --- Responsive sizing based on window height/width ---
        var winSize = ImGui.GetWindowSize();
        var availW = ImGui.GetContentRegionAvail().X;

        KeyH = Clamp(winSize.Y * 0.075f, 34f, 60f);

        var spacingX = Clamp(availW * 0.010f, 6f, 14f);
        var spacingY = Clamp(KeyH * 0.20f, 2f, 10f);

        var padX = Clamp(availW * 0.012f, 8f, 18f);
        var padY = Clamp(KeyH * 0.25f, 6f, 16f);

        ImGui.TextUnformatted(
            $"Shift: {(capsEnabled ? "ARMED" : "off")}  |  RB=Shift  LB=Space  X=Enter  B=Delete(hold)  A=Select");

        HandleGamepadNavigation();

        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(spacingX, spacingY)))
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(padX, padY)))
        using (ImRaii.PushColor(ImGuiCol.NavHighlight, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.NavWindowingHighlight, new Vector4(0f, 0f, 0f, 0f)))
        {
            DrawTypedText();
            DrawKeyboardGrid();
        }

    }



}

