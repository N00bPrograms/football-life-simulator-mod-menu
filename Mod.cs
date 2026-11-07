// Football Life Simulator mod menu: a MelonLoader (IL2CPP) mod. Press F8 in game.
// UI uses only basic GUI.* calls with manual layout: this game's build has GUILayout stripped.
using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

[assembly: MelonInfo(typeof(FLSModMenu.Mod), "FLS Mod Menu", "1.0.0", "N00bPrograms")]
[assembly: MelonGame("Ludu", "Football Life Simulator")]

namespace FLSModMenu
{
    public class Mod : MelonMod
    {
        internal static MelonLogger.Instance Log;
        MelonPreferences_Entry<string> toggleKey;

        bool open;
        int tab;
        string moneyText = "100000", status = "";
        float statusUntil;
        bool lockMoney; float lockedMoney;
        bool freezeNeeds; float nextFreeze;
        float timeSpeed = 1f;
        CursorLockMode prevLock; bool prevVisible;
        static readonly string[] Tabs = { "Money", "Needs", "Skills", "Time" };

        const float W = 420, Pad = 12, RowH = 26, Gap = 5;
        float cy; // layout cursor (y)

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            var cat = MelonPreferences.CreateCategory("FLSModMenu");
            toggleKey = cat.CreateEntry("ToggleKey", "F8", description: "Key that opens the menu (a UnityEngine.InputSystem.Key name: F8, F1, Insert, ...)");
            Game.InstallHooks(HarmonyInstance);
            Log.Msg($"Loaded. Press {toggleKey.Value} in game.");
        }

        // ---------- helpers ----------
        void Say(string s) { status = s; statusUntil = Time.unscaledTime + 3f; Log.Msg(s); }
        void Try(string label, Action a)
        {
            try { a(); Say(label); }
            catch (Exception e) { Say("Failed: " + label); Log.Error(label + ": " + e); }
        }

        // manual layout
        Rect Row(float h = RowH) { var r = new Rect(Pad, cy, W - Pad * 2, h); cy += h + Gap; return r; }
        static Rect[] Cols(Rect r, params float[] widths) // widths: >0 absolute, 0 = share the rest
        {
            float fixedW = 0; int flex = 0;
            foreach (var w in widths) { if (w > 0) fixedW += w; else flex++; }
            float flexW = flex > 0 ? (r.width - fixedW - Gap * (widths.Length - 1)) / flex : 0;
            var res = new Rect[widths.Length]; float x = r.x;
            for (int i = 0; i < widths.Length; i++)
            {
                float w = widths[i] > 0 ? widths[i] : flexW;
                res[i] = new Rect(x, r.y, w, r.height); x += w + Gap;
            }
            return res;
        }
        static Rect[] Even(Rect r, int n) { var w = new float[n]; return Cols(r, w); }
        void Label(string t) => GUI.Label(Row(), t);
        void Header(string t) { cy += 4; GUI.Label(Row(), $"<b><color=#ffd77a>{t}</color></b>"); }

        bool KeyPressed()
        {
            try
            {
                var kb = Keyboard.current;
                if (kb != null && Enum.TryParse(toggleKey.Value, true, out Key k)) return kb[k].wasPressedThisFrame;
            }
            catch { }
            return false;
        }

        void SetOpen(bool v)
        {
            open = v;
            if (open)
            {
                prevLock = Cursor.lockState; prevVisible = Cursor.visible;
                Game.Refresh(true);
            }
            else { Cursor.lockState = prevLock; Cursor.visible = prevVisible; }
        }

        // ---------- loop ----------
        public override void OnUpdate()
        {
            if (KeyPressed()) SetOpen(!open);
#if DEBUG
            TestHook.Poll(this);
#endif
            if (open) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (!lockMoney && !freezeNeeds && !open) return;

            Game.Refresh();
            try
            {
                if (lockMoney && Game.Finance != null && Game.Money < lockedMoney) Game.SetMoney(lockedMoney);
                if (freezeNeeds && Time.unscaledTime >= nextFreeze)
                {
                    nextFreeze = Time.unscaledTime + 0.5f;
                    var stats = Game.LimitedStats();
                    foreach (var n in Game.Needs)
                        if (stats.TryGetValue(n, out var s) && s.BaseValue < s.MaxValue) Game.Max(s);
                }
            }
            catch (Exception e) { Log.Warning("Toggle update failed: " + e.Message); }
        }

        public override void OnGUI()
        {
            if (!open) return;
            try { DrawMenu(); }
            catch (Exception e) { Log.Error("Menu draw failed: " + e.Message); open = false; }
        }

        void DrawMenu()
        {
            float sc = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.TRS(new Vector3(20 * sc, 60 * sc, 0), Quaternion.identity, new Vector3(sc, sc, 1f));
            var panel = new Rect(0, 0, W, 600);
            GUI.Box(panel, ""); GUI.Box(panel, ""); GUI.Box(panel, "");

            cy = Pad;
            Label($"<b><size=15>Football Life Simulator Mod Menu</size></b>    <color=#aaa>{toggleKey.Value} to close</color>");

            if (!Game.Ready)
            {
                Label("Load a save (get into the game world) to use the menu.");
                if (GUI.Button(Cols(Row(), 120)[0], "Retry")) Game.Refresh(true);
                return;
            }
            Label($"<b>${Game.Money:N0}</b>    ·    {Game.Clock}");

            var tabs = Even(Row(28), Tabs.Length);
            for (int i = 0; i < Tabs.Length; i++)
                if (GUI.Button(tabs[i], i == tab ? $"<b>[ {Tabs[i]} ]</b>" : Tabs[i])) tab = i;
            cy += 4;

            try
            {
                switch (tab)
                {
                    case 0: DrawMoney(); break;
                    case 1: DrawStats(Game.Needs, "Needs", true); break;
                    case 2: DrawStats(Game.Skills, "Football skills", false); DrawStats(Game.Social, "Social", false); break;
                    case 3: DrawTime(); break;
                }
            }
            catch (Exception e) { Label("<color=#f88>Error: " + e.Message + "</color>"); }

            if (Time.unscaledTime < statusUntil) GUI.Label(new Rect(Pad, panel.height - RowH - 6, W - Pad * 2, RowH), "<color=#9fe39f>" + status + "</color>");
        }

        // ---------- tabs ----------
        void DrawMoney()
        {
            if (Game.Finance == null) { Label("Finance system not found yet."); return; }
            Header("Money");
            var c = Cols(Row(), 0, 90);
            moneyText = GUI.TextField(c[0], moneyText);
            if (GUI.Button(c[1], "Set") && float.TryParse(moneyText, out var m))
                Try($"Money set to ${m:N0}", () => { Game.SetMoney(m); if (lockMoney) lockedMoney = m; });

            var amts = new[] { ("+1k", 1000f), ("+10k", 10000f), ("+100k", 100000f), ("+1M", 1000000f) };
            var b = Even(Row(), amts.Length);
            for (int i = 0; i < amts.Length; i++)
            {
                var (label, amt) = amts[i];
                if (GUI.Button(b[i], label)) Try($"+${amt:N0}", () => { Game.AddMoney(amt); if (lockMoney) lockedMoney = Game.Money; });
            }
            bool lm = GUI.Toggle(Row(), lockMoney, " Money never drops below current amount");
            if (lm != lockMoney) { lockMoney = lm; lockedMoney = Game.Money; Say(lm ? $"Money locked at ${lockedMoney:N0}" : "Money unlocked"); }
            Label("<color=#aaa>Shows up in your bank app as \"Mod Menu\".</color>");
        }

        void DrawStats(string[] names, string title, bool isNeeds)
        {
            var stats = Game.LimitedStats();
            if (stats.Count == 0) { Label("Player stats not found yet."); return; }
            cy += 4;
            var h = Cols(Row(), 0, 90);
            GUI.Label(h[0], $"<b><color=#ffd77a>{title}</color></b>");
            if (GUI.Button(h[1], "Max all")) Try($"{title}: all maxed", () => Game.MaxGroup(names));
            if (isNeeds)
            {
                bool fz = GUI.Toggle(Row(), freezeNeeds, " Keep all needs full");
                if (fz != freezeNeeds) { freezeNeeds = fz; Say(fz ? "Needs kept full" : "Needs back to normal"); }
            }
            foreach (var n in names)
            {
                if (!stats.TryGetValue(n, out var s)) continue;
                // GUI.HorizontalSlider is stripped in this game, so use step buttons.
                var r = Cols(Row(), 120, 42, 0, 42, 52);
                GUI.Label(r[0], Pretty(n));
                float v = s.BaseValue, step = Mathf.Max(1f, Mathf.Round((s.MaxValue - s.MinValue) / 10f));
                if (GUI.Button(r[1], "−")) Try($"{Pretty(n)} −{step:0}", () => Game.Set(s, v - step));
                GUI.Label(r[2], $"  {v:0} / {s.MaxValue:0}");
                if (GUI.Button(r[3], "+")) Try($"{Pretty(n)} +{step:0}", () => Game.Set(s, v + step));
                if (GUI.Button(r[4], "Max")) Try($"{Pretty(n)} maxed", () => Game.Max(s));
            }
        }

        static string Pretty(string n) => n == "MatchCondition" ? "Match condition" : n;

        void DrawTime()
        {
            var tm = Game.Time;
            if (tm == null) { Label("Time system not found yet."); return; }
            Header("Clock");
            bool paused = tm.IsTimePaused();
            if (GUI.Button(Row(), paused ? "Resume time" : "Pause time"))
                Try(paused ? "Time resumed" : "Time paused", () => { if (paused) tm.ResumeTime(); else tm.PauseTime(); });
            var b = Even(Row(), 3);
            if (GUI.Button(b[0], "+1 hour")) Try("Skipped 1 hour", () => tm.PassTime(0, 1, 0, 0, 0));
            if (GUI.Button(b[1], "+6 hours")) Try("Skipped 6 hours", () => tm.PassTime(0, 6, 0, 0, 0));
            if (GUI.Button(b[2], "+1 day")) Try("Skipped 1 day", () => tm.PassTime(0, 0, 1, 0, 0));

            Header($"Clock speed  (now ×{tm.GetTimeSpeed():0.##})");
            var speeds = new[] { 0.5f, 1f, 2f, 5f, 10f };
            var sb = Even(Row(), speeds.Length);
            for (int i = 0; i < speeds.Length; i++)
            {
                float sp = speeds[i];
                if (GUI.Button(sb[i], $"×{sp:0.#}"))
                    Try($"Clock speed ×{sp:0.#}", () => { timeSpeed = sp; if (sp == 1f) tm.RestartTimeSpeed(); else tm.SetTimeSpeed(sp); });
            }
        }

#if DEBUG
        internal void DebugSetOpen(bool v) => SetOpen(v);
        internal void DebugTab(int t) => tab = t;
        internal void DebugLockMoney(bool v) { lockMoney = v; lockedMoney = Game.Money; }
        internal void DebugFreezeNeeds(bool v) => freezeNeeds = v;
#endif
    }
}
