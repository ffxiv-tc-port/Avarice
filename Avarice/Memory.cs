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
            receiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTail);
        }

        void ReceiveActionEffectDetour(ActionEffectSet set)
        {
            try
            {
                if (set.Source?.Address == Svc.ClientState.LocalPlayer?.Address)
                {
                    var positionalState = PositionalState.Ignore;
                    if (P.PositionalManager?.IsPositional((int)set.Header.ActionID) == true)
                    {
                        positionalState = PositionalState.Failure;
                        if (set.TargetEffects != null)
                        {
                            foreach (var effect in set.TargetEffects)
                            {
                                effect.ForEach(entry =>
                                {
                                    if (entry.type == ActionEffectType.Damage)
                                        if (P.PositionalManager?.IsPositionalHit((int)set.Header.ActionID, entry.param2) == true)
                                            positionalState = PositionalState.Success;
                                });
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
