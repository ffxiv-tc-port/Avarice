using Dalamud.Interface.Components;
using ECommons.MathHelpers;
using System.Windows.Forms.Design.Behavior;

namespace Avarice.ConfigurationWindow;

internal unsafe partial class ConfigWindow : Window
{
    internal const float SelectWidth = 200f;
    public ConfigWindow() : base($"{P.Name} {"Configuration".Loc()} - {P.currentProfile.Name.Default("Unnamed profile".Loc())}###AvariceConfig")
    {
        Size = new(640, 480);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void OnClose()
    {
        base.OnClose();
        Svc.PluginInterface.SavePluginConfig(P.config);
    }

    public override void Draw()
    {
        ImGuiEx.EzTabBar("##tabbar",
            ("Settings".Loc(), TabSettings.Draw, null, true),
            ("Anticipation".Loc(), TabAnticipation.Draw, null, true),
            ("Profiles".Loc(), TabProfiles.Draw, null, true),
            //("Tank middle", TabTank.Draw, null, true),
            ("Statistics".Loc(), TabStatistics.Draw, null, true),
            ("About".Loc(), delegate { PunishLib.ImGuiMethods.AboutTab.Draw(Svc.PluginInterface.InternalName); }, null, true),
            (P.currentProfile.Debug ? "Log" : null, InternalLog.PrintImgui, null, false),
            (P.currentProfile.Debug ? "Debug" : null, Debug, null, true)
        );
    }

    private int ActionOverride = 0;

    private void Debug()
    {
        if(ImGui.CollapsingHeader("StaticAutoDetectRadiusData"))
        {
            ImGuiEx.Text(P.StaticAutoDetectRadiusData.Select(x => x.ToString()).Join("\n"));
        }
        {
            if(ImGui.Button("vfx yes".Loc()))
            {
                VfxEditorManager.DisplayVfx(true);
            }
            if(ImGui.Button("vfx no".Loc()))
            {
                VfxEditorManager.DisplayVfx(false);
            }
            ImGui.InputInt("Action override test".Loc(), ref ActionOverride);
            if(ImGui.Button("set action override".Loc()))
            {
                Svc.PluginInterface.GetOrCreateData("Avarice.ActionOverride", () => new List<uint>() { 0 })[0] = (uint)ActionOverride;
            }
            ImGuiEx.Text($"Current action override: {(Svc.PluginInterface.TryGetData<List<uint>>("Avarice.ActionOverride", out var data) ? data[0] : 0)}");
            ImGuiEx.Text($"Combo: {P.memory.LastComboMove}");
            foreach(var x in Svc.Objects.LocalPlayer?.StatusList)
            {
                ImGuiEx.TextCopy($"{x.GameData.ValueNullable?.Name}: id={x.StatusId}, time={x.RemainingTime}");
            }

            ImGuiEx.Text("N. S. ");
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.SameLine(0, 0);
            ImGuiEx.Text(ImGuiColors.DalamudRed, FontAwesomeIcon.Heart.ToIconString());
            ImGui.PopFont();
            ImGuiEx.Text($"Is target positional: {Svc.Targets.Target?.HasPositional()}");
            if(ImGui.Button("Test IPC".Loc()))
            {
                Safe(TestIPC);
            }
        }
    }

    private void TestIPC()
    {
        var result = Svc.PluginInterface.GetIpcSubscriber<IntPtr, CardinalDirection>("Avarice.CardinalDirection").InvokeFunc(Svc.Targets.Target?.Address ?? IntPtr.Zero);
        Svc.Chat.Print(result.ToString());
    }

    internal static void DrawUnfilledSettings(string id, ref Brush b, bool displayCondition = true)
    {
        if(displayCondition)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150f);
            ImGuiEx.EnumCombo($"##b{id}", ref b.DisplayCondition);
            ImGuiEx.InvisibleButton(3);
        }
        ImGui.SameLine();
        b.Fill = Vector4.Zero;
        ImGuiEx.Text("Thickness:".Loc());
        ImGui.SameLine();
        ImGui.SetNextItemWidth(50f);
        ImGui.DragFloat($"##c{id}", ref b.Thickness, 0.1f, 0f, 10f);
        ImGui.SameLine();
        ImGuiEx.Text("  Color:".Loc());
        ImGui.SameLine();
        ImGui.ColorEdit4($"##a{id}", ref b.Color, ImGuiColorEditFlags.NoInputs);
    }

    internal static void DrawUnfilledMultiSettings(string id, ref Brush b, ref Vector4 south, ref Vector4 east, ref Vector4 west, ref bool lines, ref bool makeSameColor)
    {
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150f);
        ImGuiEx.EnumCombo($"##b{id}", ref b.DisplayCondition);
        ImGuiEx.InvisibleButton(3);
        ImGui.SameLine();
        b.Fill = Vector4.Zero;
        ImGuiEx.Text("Thickness:".Loc());
        ImGui.SameLine();
        ImGui.SetNextItemWidth(50f);
        ImGui.DragFloat($"##c{id}", ref b.Thickness, 0.1f, 0f, 10f);
        ImGui.SameLine();
        if(!makeSameColor) { ImGuiEx.Text("  Colours:".Loc()); }
        ImGuiEx.InvisibleButton(11);
        ImGui.SameLine();
        ImGui.Checkbox("Colour match borders?".Loc() + $"##{id}", ref makeSameColor);
        ImGuiComponents.HelpMarker("If enabled, the borders of each segment will automatically be set to a higher alpha variation of their own respective setting.".Loc());
        if(!makeSameColor)
        {
            ImGuiEx.Text("            Front:".Loc());
            ImGui.SameLine();
            ImGui.ColorEdit4($"##a{id}", ref b.Color, ImGuiColorEditFlags.NoInputs);
            ImGuiEx.Text("            Rear:".Loc());
            ImGui.SameLine();
            ImGui.ColorEdit4($"##a{id}s", ref south, ImGuiColorEditFlags.NoInputs);
            ImGuiEx.Text("            Left Flank:".Loc());
            ImGui.SameLine();
            ImGui.ColorEdit4($"##a{id}e", ref east, ImGuiColorEditFlags.NoInputs);
            ImGuiEx.Text("            Right Flank:".Loc());
            ImGui.SameLine();
            ImGui.ColorEdit4($"##a{id}w", ref west, ImGuiColorEditFlags.NoInputs);
        }
        ImGuiEx.InvisibleButton(11);
        ImGui.SameLine();
        ImGui.Checkbox("Display zoning separator lines?".Loc() + $"##{id}", ref lines);
        ImGuiEx.InvisibleButton(11);
        ImGui.SameLine();
        if(ImGui.RadioButton("Display only max melee weaponskill range ring?".Loc() + $"##{id}", P.currentProfile.Radius3 && !P.currentProfile.Radius2))
        {
            P.currentProfile.Radius3 = true;
            P.currentProfile.Radius2 = false;
        }
        ImGuiEx.InvisibleButton(11);
        ImGui.SameLine();
        if(ImGui.RadioButton("Display only max auto-attack range ring?".Loc() + $"##{id}", P.currentProfile.Radius2 && !P.currentProfile.Radius3))
        {
            P.currentProfile.Radius2 = true;
            P.currentProfile.Radius3 = false;
        }
        ImGuiEx.InvisibleButton(11);
        ImGui.SameLine();
        if(ImGui.RadioButton("Display auto-attack/weaponskill combination ring?".Loc() + $"##{id}", P.currentProfile.Radius2 && P.currentProfile.Radius3))
        {
            P.currentProfile.Radius3 = true;
            P.currentProfile.Radius2 = true;
        }
    }
}
