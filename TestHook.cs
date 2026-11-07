#if DEBUG
// Debug builds only: run commands from UserData/flsmm_test.txt (one per line) and log the results.
using System;
using System.IO;
using System.Linq;
using MelonLoader.Utils;
using UnityEngine;

namespace FLSModMenu
{
    internal static class TestHook
    {
        static float next;
        public static void Poll(Mod mod)
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1f;
            var path = Path.Combine(MelonEnvironment.UserDataDirectory, "flsmm_test.txt");
            if (!File.Exists(path)) return;
            var lines = File.ReadAllLines(path);
            File.Delete(path);
            foreach (var raw in lines)
            {
                var p = raw.Trim().Split(' ');
                if (p[0].Length == 0) continue;
                try
                {
                    switch (p[0])
                    {
                        case "refresh": Game.Refresh(true); break;
                        case "open": mod.DebugSetOpen(true); break;
                        case "close": mod.DebugSetOpen(false); break;
                        case "tab": mod.DebugTab(int.Parse(p[1])); break;
                        case "shot": ScreenCapture.CaptureScreenshot(p[1]); break;
                        case "addmoney": Game.AddMoney(float.Parse(p[1])); break;
                        case "setmoney": Game.SetMoney(float.Parse(p[1])); break;
                        case "lockmoney": mod.DebugLockMoney(p[1] == "on"); break;
                        case "freeze": mod.DebugFreezeNeeds(p[1] == "on"); break;
                        case "max": Game.MaxGroup(new[] { p[1] }); break;
                        case "set": Game.Set(Game.LimitedStats()[p[1]], float.Parse(p[2])); break;
                        case "maxneeds": Game.MaxGroup(Game.Needs); break;
                        case "maxskills": Game.MaxGroup(Game.Skills.Concat(Game.Social)); break;
                        case "pause": Game.Time.PauseTime(); break;
                        case "resume": Game.Time.ResumeTime(); break;
                        case "hour": Game.Time.PassTime(0, int.Parse(p[1]), 0, 0, 0); break;
                        case "speed": Game.Time.SetTimeSpeed(float.Parse(p[1])); break;
                        case "speedreset": Game.Time.RestartTimeSpeed(); break;
                        case "dump": break;
                        case "diag": Mod.Log.Msg("[diag] " + Game.Diagnose()); break;
                    }
                    var stats = Game.LimitedStats();
                    var sv = string.Join(" ", stats.Select(kv => $"{kv.Key}={kv.Value.BaseValue:0.#}/{kv.Value.MaxValue:0}"));
                    Mod.Log.Msg($"[test] {raw} -> ready={Game.Ready} fin={Game.Finance != null} time={Game.Time != null} stats={Game.HasStats} " +
                        $"money={Game.Money:0} clock={Game.Clock} paused={Game.Time?.IsTimePaused()} speed={Game.Time?.GetTimeSpeed()} | {sv}");
                }
                catch (Exception e) { Mod.Log.Error($"[test] {raw} failed: {e}"); }
            }
        }
    }
}
#endif
