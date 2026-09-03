namespace Avarice.StaticData;

internal static class PositionalPotencies
{
    internal record Row(int Id, int Percent, bool IsHit, string Name, string Position, string Comment);

    // ── Percent 欄的量綱 ──────────────────────────────────────────────
    // Percent 不是傷害倍率,是 action-effect 結構裡 EffectEntry.param2 的**觀測值**
    // (Memory.cs 直接拿 entry.param2 來查這張表)。2026-09-04 以台服 7.20 EXD
    // 逐項回推出的模型是:
    //
    //     param2 = floor(100 * (P_applied - P_base) / P_applied)
    //
    //   P_base    = 該技能在**當下狀態**下的「未連擊、未命中方位」威力
    //   P_applied = 實際套用的威力(含連擊加成與方位加成)
    //   狀態類威力增益(風纏/猛虎功力/絞決效果提高/銳牙…)會同時抬高兩邊,
    //   所以**不進分子、只進分母** —— 這是最容易寫錯的一點。
    //
    // 威力數字唯一的離線來源是 ActionTransient.Description 的文字
    // (Action 表沒有威力欄)。校驗工具:
    //   ~/.claude/tools/exd/avarice_positional_calib.py
    // 該工具對本表 17 個技能推導出的 28 個值全部命中現行表,唯二的分歧就是
    // 下面 34621/34622 那兩筆,而分歧方向與使用者實機探針回報的值一致。
    //
    // ⚠️ 34610~34613(參之牙四式)實機探針回報 param2=40,表內沒有這個值。
    //    這四招是「轉化型連擊按鍵」,tooltip 只列連擊後威力(340/方位 400),
    //    未連擊威力**不在任何 EXD 欄位**⇒ P_base 離線取不到 ⇒ 本輪不動數字。
    //    (若 P_base=200 則非銳牙態=50[已在表內]、銳牙態=40[缺];詳見交接報告。)
    // ─────────────────────────────────────────────────────────────────
    internal static readonly Row[] Records =
    {
        //     Id  Percent  IsHit  ActionName               ActionPosition  Comment
        new(   56, 18, true,  "Snap Punch",            "flank", "50/90 Unbuffed"),
        new(   56, 14, true,  "Snap Punch",            "flank", "50 Buffed"),
        new(   56, 13, true,  "Snap Punch",            "flank", "60 buffed, 90 Buffed"),
        new(   56, 20, true,  "Snap Punch",            "flank", "60 Unbuffed"),
        new(   56, 12, true,  "Snap Punch",            "flank", "90 Buffed"),
        new(   66, 18, true,  "Demolish",              "rear",  "lv 50"),
        new(   66, 17, true,  "Demolish",              "rear",  "lv 50"),
        new(   66, 15, true,  "Demolish",              "rear",  "lv 90"),
        new(   66, 14, true,  "Demolish",              "rear",  "lv 100"),
        new(   79,  0, false, "Heavy Thrust",          "flank", "lol"),
        new(   88, 28, true,  "Chaos Thrust",          "rear",  "50 Uncomboed"),
        new(   88, 61, true,  "Chaos Thrust",          "rear",  "50 Comboed"),
        new( 2255, 70, true,  "Aeolian Edge",          "rear",  "31 Comboed"),
        new( 2255, 54, true,  "Aeolian Edge",          "rear",  "60 Uncomboed with Kazematoi"),
        new( 2255, 23, true,  "Aeolian Edge",          "rear",  "60 Comboed with Kazematoi"),
        new( 2255, 37, true,  "Aeolian Edge",          "rear",  "60 Comboed no Kazematoi"),
        new( 2255, 20, true,  "Aeolian Edge",          "rear",  "90 Uncomboed with Kazematoi"),
        new( 2255, 30, true,  "Aeolian Edge",          "rear",  "90 Uncomboed no Kazematoi"),
        new( 2255, 50, true,  "Aeolian Edge",          "rear",  "90 Comboed with Kazematoi"),
        new( 2255, 63, true,  "Aeolian Edge",          "rear",  "90 Comboed no Kazematoi"),
        new( 2255, 15, true,  "Aeolian Edge",          "rear",  "100 Uncomboed with Kazematoi"),
        new( 2255, 21, true,  "Aeolian Edge",          "rear",  "100 Uncomboed no Kazematoi"),
        new( 2255, 42, true,  "Aeolian Edge",          "rear",  "100 Comboed with Kazematoi"),
        new( 2255, 52, true,  "Aeolian Edge",          "rear",  "100 Comboed no Kazematoi"),
        new( 2258, 25, true,  "Trick Attack",          "rear",  ""),
        new( 3554, 28, true,  "Fang and Claw",         "flank", "90 Uncomboed"),
        new( 3554, 66, true,  "Fang and Claw",         "flank", "90 Comboed"),
        new( 3554, 22, true,  "Fang and Claw",         "flank", "100 Uncomboed"),
        new( 3554, 58, true,  "Fang and Claw",         "flank", "100 Comboed"),
        new( 3556, 28, true,  "Wheeling Thrust",       "rear",  "90 Uncomboed"),
        new( 3556, 66, true,  "Wheeling Thrust",       "rear",  "90 Comboed"),
        new( 3556, 22, true,  "Wheeling Thrust",       "rear",  "100 Uncomboed"),
        new( 3556, 58, true,  "Wheeling Thrust",       "rear",  "100 Comboed"),
        new( 3563, 30, true,  "Armor Crush",           "flank", "90 Uncomboed"),
        new( 3563, 65, true,  "Armor Crush",           "flank", "90 Comboed"),
        new( 3563, 20, true,  "Armor Crush",           "flank", "100 Uncomboed"),
        new( 3563, 52, true,  "Armor Crush",           "flank", "100 Comboed"),
        new( 3563, 37, true,  "Armor Crush",           "flank", "<74 Uncomboed"),
        new( 3563, 72, true,  "Armor Crush",           "flank", "<74 Comboed"),
        new( 7481, 33, true,  "Gekko",                 "rear",  "50 Uncomboed"),
        new( 7481, 72, true,  "Gekko",                 "rear",  "50 Comboed"),
        new( 7481, 31, true,  "Gekko",                 "rear",  "90 Uncomboed"),
        new( 7481, 70, true,  "Gekko",                 "rear",  "90 Comboed"),
        new( 7481, 23, true,  "Gekko",                 "rear",  "100 Uncomboed"),
        new( 7481, 61, true,  "Gekko",                 "rear",  "100 Comboed"),
        new( 7482, 33, true,  "Kasha",                 "flank", "50 Uncomboed"),
        new( 7482, 72, true,  "Kasha",                 "flank", "50 Comboed"),
        new( 7482, 31, true,  "Kasha",                 "flank", "90 Uncomboed"),
        new( 7482, 70, true,  "Kasha",                 "flank", "90 Comboed"),
        new( 7482, 23, true,  "Kasha",                 "flank", "100 Uncomboed"),
        new( 7482, 61, true,  "Kasha",                 "flank", "100 Comboed"),
        new(24382, 11, true,  "Gibbet",                "flank", "90 Enhanced"),
        new(24382, 13, true,  "Gibbet",                "flank", "90 Non-enhanced"),
        new(24382,  9, true,  "Gibbet",                "flank", "100 Enhanced"),
        new(24382, 10, true,  "Gibbet",                "flank", "100 Non-enhanced"),
        new(24383, 11, true,  "Gallows",               "rear",  "90 Enhanced"),
        new(24383, 13, true,  "Gallows",               "rear",  "90 Non-enhanced"),
        new(24383,  9, true,  "Gallows",               "rear",  "100 Enhanced"),
        new(24383, 10, true,  "Gallows",               "rear",  "100 Non-enhanced"),
        new(25772, 28, true,  "Chaotic Spring",        "rear",  "90 Uncomboed"),
        new(25772, 66, true,  "Chaotic Spring",        "rear",  "90 Comboed"),
        new(25772, 22, true,  "Chaotic Spring",        "rear",  "100 Uncomboed"),
        new(25772, 58, true,  "Chaotic Spring",        "rear",  "100 Comboed"),
        new(34610, 48, true,  "Flanksting Strike",     "flank", "100 Enhanced"),
        new(34610, 54, true,  "Flanksting Strike",     "flank", ""),
        new(34610, 60, true,  "Flanksting Strike",     "flank", "100 Non-enhanced"),
        new(34610, 70, true,  "Flanksting Strike",     "flank", ""),
        new(34610, 50, true,  "Flanksting Strike",     "flank", "80 ?"),
        new(34610, 63, true,  "Flanksting Strike",     "flank", "80 ?"),
        new(34611, 48, true,  "Flanksbane Fang",       "flank", "100 Enhanced"),
        new(34611, 54, true,  "Flanksbane Fang",       "flank", ""),
        new(34611, 60, true,  "Flanksbane Fang",       "flank", "100 Non-enhanced"),
        new(34611, 70, true,  "Flanksbane Fang",       "flank", ""),
        new(34611, 50, true,  "Flanksbane Fang",       "flank", "80 ?"),
        new(34611, 63, true,  "Flanksbane Fang",       "flank", "80 ?"),
        new(34612, 48, true,  "Hindsting Strike",      "rear",  "100 Enhanced"),
        new(34612, 54, true,  "Hindsting Strike",      "rear",  ""),
        new(34612, 60, true,  "Hindsting Strike",      "rear",  "100 Non-enhanced"),
        new(34612, 70, true,  "Hindsting Strike",      "rear",  ""),
        new(34612, 50, true,  "Hindsting Strike",      "rear",  "80 ?"),
        new(34612, 63, true,  "Hindsting Strike",      "rear",  "80 ?"),
        new(34613, 48, true,  "Hindsbane Fang",        "rear",  "100 Enhanced"),
        new(34613, 54, true,  "Hindsbane Fang",        "rear",  ""),
        new(34613, 60, true,  "Hindsbane Fang",        "rear",  "100 Non-enhanced"),
        new(34613, 70, true,  "Hindsbane Fang",        "rear",  ""),
        new(34613, 50, true,  "Hindsbane Fang",        "rear",  "80 ?"),
        new(34613, 63, true,  "Hindsbane Fang",        "rear",  "80 ?"),
        // 台服 7.20 實測 param2=8,而表內只有 7 ⇒ 這兩招的方位命中永遠判不出來。
        // 8 的來源:ActionTransient.Description(貳之蛇【猛襲】/【疾速】)
        //   「威力：570 / 側面(背面)攻擊威力：620」⇒ floor(100*(620-570)/620) = 8。
        // 7 是舊版威力留下的歷史值,一併保留(台服 7.20 不會再產生 7)。
        new(34621,  7, true,  "Hunter's Coil",         "flank", "lv 100"),
        new(34621,  8, true,  "Hunter's Coil",         "flank", "TC 7.20 570->620"),
        new(34622,  7, true,  "Swiftskin's Coil",      "rear",  "lv 100"),
        new(34622,  8, true,  "Swiftskin's Coil",      "rear",  "TC 7.20 570->620"),
        new(36947, 16, true,  "Pouncing Coeurl",       "flank", "100 Unbuffed"),
        new(36947, 11, true,  "Pouncing Coeurl",       "flank", "100 Buffed"),
        new(36970,  7, true,  "Executioner's Gibbet",  "flank", ""),
        new(36971,  7, true,  "Executioner's Gallows", "rear",  ""),
    };
}
