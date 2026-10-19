using Avarice.Structs;
using Dalamud.Hooking;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace Avarice
{
    internal unsafe class Memory
    {
        internal uint LastComboMove => ActionManager.Instance()->Combo.Action;

        //TC 7.20:ECommons 的 ActionEffect 模組內建簽名過新,在 TC 客戶端掃描失敗,其事件永遠不會觸發。
        //改在插件端以 TC 驗證過的簽名(ActionWatching.ActionEffectSig)自行掛鉤,沿用原本的 ActionEffectSet 處理邏輯。
        private readonly Hook<ActionEffect.ProcessActionEffect> receiveActionEffectHook;

        internal Memory()
        {
            SignatureHelper.Initialise(this);
            try
            {
                receiveActionEffectHook = Svc.Hook.HookFromSignature<ActionEffect.ProcessActionEffect>(Data.ActionWatching.ActionEffectSig, ProcessActionEffectDetour);
                receiveActionEffectHook.Enable();
            }
            catch (Exception e)
            {
                PluginLog.Error($"Could not find ActionEffect signature: {e.Message}");
            }
        }

        private void ProcessActionEffectDetour(uint sourceId, Character* sourceCharacter, Vector3* pos, EffectHeader* effectHeader, EffectEntry* effectArray, ulong* effectTail)
        {
            try
            {
                ReceiveActionEffectDetour(new ActionEffectSet(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTail));
            }
            catch (Exception e)
            {
                e.Log();
            }
            receiveActionEffectHook.OriginalDisposeSafe(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTail);
        }

        void ReceiveActionEffectDetour(ActionEffectSet set)
        {
            try
            {
                if (set.Source?.Address == Svc.Objects.LocalPlayer?.Address)
                {
                    var positionalState = PositionalState.Ignore;
                    if (P.PositionalManager?.IsPositional((int)set.Header.ActionID) == true)
                    {
                        positionalState = PositionalState.Failure;
                        if (set.TargetEffects != null)
                        {
                            // 與 TargetEffect.ForEach 相同的 0..7 走訪。刻意不用 lambda:捕獲 set 會讓每次
                            // 進本方法(任何人的每個技能效果)都把整個 ActionEffectSet 複製進堆積上的閉包。
                            foreach (var effect in set.TargetEffects)
                            {
                                for (var i = 0; i < 8; i++)
                                {
                                    var entry = effect[i];
                                    if (entry.type == ActionEffectType.Damage)
                                        if (P.PositionalManager?.IsPositionalHit((int)set.Header.ActionID, entry.param2) == true)
                                            positionalState = PositionalState.Success;
                                }
                            }
                        }
                    }
                    if (positionalState == PositionalState.Success)
                    {
                        if (P.currentProfile?.EnableChatMessagesSuccess == true) Svc.Chat?.Print("Positional HIT!".Loc());
                        if (P.currentProfile?.EnableVFXSuccess == true) VfxEditorManager.DisplayVfx(true);
                        P.RecordStat(false);
                    }
                    else if (positionalState == PositionalState.Failure)
                    {
                        if (P.currentProfile?.EnableChatMessagesFailure == true) Svc.Chat?.Print("Positional MISS!".Loc());
                        if (P.currentProfile?.EnableVFXFailure == true) VfxEditorManager.DisplayVfx(false);
                        P.RecordStat(true);
                    }
                    // Ignore 是絕大多數且無診斷價值；只有真的判定過方位的結果留在 Debug。
                    if (positionalState == PositionalState.Ignore)
                        PluginLog.Verbose($"Positional state: {positionalState}");
                    else
                        PluginLog.Debug($"Positional state: {positionalState}");
                }
            }
            catch (Exception e)
            {
                e.Log();
            }
        }

        public void Dispose()
        {
            receiveActionEffectHook?.Dispose();
        }
    }
}
