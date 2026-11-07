using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppFinanceSystem;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppPlayerSystem.StatSystem;
using Il2CppTimeSystem;
using Il2CppVContainer;
using Il2CppVContainer.Unity;
using UnityEngine;

namespace FLSModMenu
{
    /// <summary>
    /// Finds the game's live services. Three routes, first hit wins:
    /// 1) Harmony hooks on methods the HUD calls every frame capture the live instance;
    /// 2) VContainer lifetime scopes;
    /// 3) for stats, the loaded stat-holder asset whose money matches the bank balance.
    /// </summary>
    internal static class Game
    {
        public static FinanceManager Finance;
        public static TimeManager Time;
        public static PlayerStatManager PlayerStats;
        static StatController fallbackController;
        static float nextRefresh;

        static StatController Controller => PlayerStats?.StatController ?? fallbackController;
        public static bool Ready => Finance != null || Controller != null;
        public static bool HasStats => Controller != null;

        // ---------- capture hooks ----------
        public static void InstallHooks(HarmonyLib.Harmony h)
        {
            Hook(h, typeof(FinanceManager), nameof(FinanceManager.CurrentMoney), nameof(CaptureFinance));
            Hook(h, typeof(FinanceManager), nameof(FinanceManager.HasMoney), nameof(CaptureFinance));
            Hook(h, typeof(TimeManager), nameof(TimeManager.GetTime), nameof(CaptureTime));
            Hook(h, typeof(TimeManager), nameof(TimeManager.GetNormalizedTimeOfDay), nameof(CaptureTime));
            foreach (var m in new[] { nameof(PlayerStatManager.Initialize), nameof(PlayerStatManager.GetOverall), nameof(PlayerStatManager.GetFameLevel),
                                      nameof(PlayerStatManager.CheckForStatThresholdChange), nameof(PlayerStatManager.OnStatsUpdated) })
                Hook(h, typeof(PlayerStatManager), m, nameof(CapturePlayerStats));
            Hook(h, typeof(StatController), nameof(StatController.OnStatModifiedValueChanged), nameof(CaptureController));
        }

        static void CapturePlayerStats(PlayerStatManager __instance) { if (__instance != null && (PlayerStats == null || PlayerStats.Pointer != __instance.Pointer)) PlayerStats = __instance; }
        // Fallback only: the first controller that reports changes and has needs (the player's).
        static void CaptureController(StatController __instance)
        {
            if (PlayerStats != null || __instance == null || fallbackController != null) return;
            try { if (__instance.m_StatsByType != null && __instance._stats != null && __instance._stats.Count > 10) fallbackController = __instance; } catch { }
        }

        static void Hook(HarmonyLib.Harmony h, Type t, string method, string postfix)
        {
            try
            {
                var m = AccessTools.Method(t, method);
                h.Patch(m, postfix: new HarmonyMethod(typeof(Game), postfix));
            }
            catch (Exception e) { Mod.Log.Warning($"Hook {t.Name}.{method} failed: {e.Message}"); }
        }

        static void CaptureFinance(FinanceManager __instance) { if (__instance != null && (Finance == null || Finance.Pointer != __instance.Pointer)) Finance = __instance; }
        static void CaptureTime(TimeManager __instance) { if (__instance != null && (Time == null || Time.Pointer != __instance.Pointer)) Time = __instance; }

        // ---------- refresh ----------
        public static void Refresh(bool force = false)
        {
            if (!force && UnityEngine.Time.unscaledTime < nextRefresh) return;
            nextRefresh = UnityEngine.Time.unscaledTime + 3f;
            try
            {
                Finance ??= Resolve<IFinanceManager>()?.TryCast<FinanceManager>();
                Time ??= Resolve<ITimeManager>()?.TryCast<TimeManager>();
                PlayerStats ??= Resolve<PlayerStatManager>();
            }
            catch (Exception e) { Mod.Log.Warning("Service lookup failed: " + e.Message); }
        }

        static T Resolve<T>() where T : Il2CppObjectBase
        {
            var type = Il2CppType.Of<T>();
            foreach (var scope in Scopes())
            {
                var c = scope.Container;
                if (c == null) continue;
                try
                {
                    Il2CppSystem.Object resolved = null;
                    if (c.TryResolve(type, out resolved, null) && resolved != null)
                    {
                        var t = resolved.TryCast<T>();
                        if (t != null) return t;
                    }
                }
                catch { }
            }
            return null;
        }

        static List<LifetimeScope> Scopes()
        {
            var res = new List<LifetimeScope>();
            try
            {
                var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<LifetimeScope>());
                foreach (var o in all) { var s = o?.TryCast<LifetimeScope>(); if (s != null) res.Add(s); }
            }
            catch (Exception e) { Mod.Log.Warning("Scope search failed: " + e.Message); }
            return res;
        }

        public static string Diagnose()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"playerStats={PlayerStats != null} fallbackController={fallbackController != null} liveStats={Controller?._stats?.Count} ");
            var scopes = Scopes();
            sb.Append($"scopes={scopes.Count}");
            foreach (var s in scopes)
            {
                string n; try { n = s.gameObject.name + ":" + s.Cast<Il2CppSystem.Object>().GetIl2CppType().Name; } catch { n = "?"; }
                sb.Append($" [{n} container={(s.Container != null)}");
                foreach (var t in new[] { Il2CppType.Of<IFinanceManager>(), Il2CppType.Of<FinanceManager>(), Il2CppType.Of<ITimeManager>(), Il2CppType.Of<TimeManager>(), Il2CppType.Of<PlayerStatManager>() })
                {
                    try
                    {
                        Il2CppSystem.Object r = null;
                        bool ok = s.Container != null && s.Container.TryResolve(t, out r, null);
                        sb.Append($" {t.Name}={(ok ? (r?.GetIl2CppType().Name ?? "null") : "no")}");
                    }
                    catch (Exception e) { sb.Append($" {t.Name}=EX({e.GetType().Name}: {e.Message.Split('\n')[0]})"); }
                }
                sb.Append("]");
            }
            int holders = 0;
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<BaseStatHolderSO>()))
            {
                var h = o?.TryCast<BaseStatHolderSO>(); holders++;
                string money = "-";
                var st = h?.Stats;
                if (st != null) for (int i = 0; i < st.Count; i++) { var ms = st[i]?.TryCast<MoneyStat>(); if (ms != null) money = ms.BaseValue.ToString("0"); }
                sb.Append($" holder[{h?.name} n={st?.Count} money={money}]");
            }
            sb.Append($" holders={holders} hookFinance={Finance != null} hookTime={Time != null}");
            return sb.ToString();
        }

        // ---------- money ----------
        static MoneyTransaction Tx(float amount) =>
            new MoneyTransaction(TransactionTopic.Social, amount, "Mod Menu", new Il2CppStringArray(0));

        public static float Money => Finance?.CurrentMoney() ?? 0f;
        public static void AddMoney(float amount)
        {
            if (amount >= 0) Finance.EarnMoney(Tx(amount));
            else Finance.TrySpendMoney(Tx(-amount), true, false);
        }
        public static void SetMoney(float amount) => Finance.SetMoney(Tx(amount));

        // ---------- stats ----------
        public static readonly string[] Needs = { "Energy", "Hunger", "Health", "Mood", "Stamina", "Toilet", "MatchCondition" };
        public static readonly string[] Skills = { "Agility", "Dribbling", "Fitness", "Positioning", "Power", "Shooting", "Technique" };
        public static readonly string[] Social = { "Charisma", "Fame", "Flirty" };

        public static string StatName(IStat s)
        {
            var n = s.Cast<Il2CppSystem.Object>().GetIl2CppType().Name;
            return n.EndsWith("Stat") ? n.Substring(0, n.Length - 4) : n;
        }

        public static Dictionary<string, LimitedFloatStat> LimitedStats()
        {
            var d = new Dictionary<string, LimitedFloatStat>();
            var list = Controller?._stats;
            if (list == null) return d;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s == null) continue;
                var lf = s.TryCast<LimitedFloatStat>();
                if (lf != null) d[StatName(s)] = lf;
            }
            return d;
        }

        public static void Max(LimitedFloatStat s) => s.SetBaseValue(s.MaxValue);
        public static void Set(LimitedFloatStat s, float v) => s.SetBaseValue(Mathf.Clamp(v, s.MinValue, s.MaxValue));

        public static int MaxGroup(IEnumerable<string> names)
        {
            var all = LimitedStats(); int n = 0;
            foreach (var name in names) if (all.TryGetValue(name, out var s)) { Max(s); n++; }
            return n;
        }

        // ---------- time ----------
        public static string Clock => Time != null && Time.HasTime ? Time.GetTime().ToString("ddd dd MMM yyyy  HH:mm") : "--";
    }
}
