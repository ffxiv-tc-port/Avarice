using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System.Linq;
using ECommons.DalamudServices;
using System.Reflection;

namespace Avarice
{
    internal static class LuminaSheets
    {
        internal static HashSet<uint> NonPositionalUnits = new HashSet<uint>();
        private static Dictionary<uint, bool> PositionalStatusCache = new Dictionary<uint, bool>();
        internal static readonly uint[] TrueNorthEffects = new uint[] { 1250 };

        internal static void Init()
        {
            try
            {
                var bnpcSheet = Svc.Data.GetExcelSheet<BNpcBase>();
                if (bnpcSheet != null)
                {
                    PropertyInfo property = typeof(BNpcBase).GetProperty("IsOmnidirectional");
                    if (property != null)
                    {
                        foreach (var bnpc in bnpcSheet)
                        {
                            if ((bool)property.GetValue(bnpc))
                            {
                                NonPositionalUnits.Add(bnpc.RowId);
                            }
                        }
                        Svc.Log.Debug($"Loaded {NonPositionalUnits.Count} non-positional enemy types from BNpcBase");
                    }
                    else
                    {
                        Svc.Log.Debug("IsOmnidirectional property not found in BNpcBase");
                    }
                }
                else
                {
                    Svc.Log.Error("Failed to load BNpcBase sheet");
                }
            }
            catch (System.Exception ex)
            {
                Svc.Log.Error(ex, "Error initializing LuminaSheets");
                NonPositionalUnits = new HashSet<uint>();
            }
        }

        public static bool HasPositional(this IGameObject obj)
        {
            // 全向敵人(BNpcBase.IsOmnidirectional)沒有方位差別,一律不算「有方位」。
            // 這個判定必須是無條件的:過去它被綁在 OnlyDrawIfPositional 上,該設定關閉時
            // 直接 return true,等於整個全向過濾失效 —— Canvas 的預測位置圓餅會照樣畫在
            // 根本沒有方位可言的敵人身上。對照組:BossModReborn 的 !Omnidirectional 判定
            // 同樣是無條件的。
            // OnlyDrawIfPositional 字面上的職責是「只在目標需要方位時才繪製整個疊加層」,
            // 由 Canvas.DrawConditions() 自己把關(它會先檢查該設定再呼叫本方法),與這裡無關。
            if (obj is not IBattleNpc bnpc)
                return false;

            uint dataId = bnpc.BaseId;
            if (PositionalStatusCache.TryGetValue(dataId, out bool hasPositional))
                return hasPositional;

            bool result = !NonPositionalUnits.Contains(dataId);
            if (result && bnpc.BattleNpcKind != Dalamud.Game.ClientState.Objects.Enums.BattleNpcSubKind.Enemy)
                result = false;

            PositionalStatusCache[dataId] = result;
            return result;
        }

        public static bool HasTrueNorthEffect()
        {
            if (Svc.Objects.LocalPlayer == null)
                return false;

            foreach (var status in Svc.Objects.LocalPlayer.StatusList)
            {
                if (TrueNorthEffects.Contains(status.StatusId))
                    return true;
            }
            return false;
        }

        public static void ClearCaches()
        {
            PositionalStatusCache.Clear();
        }
    }
}
