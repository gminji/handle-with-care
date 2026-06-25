using System;
using UnityEngine;

namespace SlopCo.Core
{
    /// <summary>
    /// Persistent personal bests (local PlayerPrefs) — the cross-run "beat your record, one more run"
    /// hook the end-of-run card chases. Stores the best survived day, best cash, and best chain across
    /// sessions, exactly like the other PlayerPrefs-backed state (<see cref="SettingsManager"/>,
    /// Localization). No netcode: this is a *personal* record (not a leaderboard), and the day it stores
    /// comes from the already-replicated RoundManager.RoundNumber, so co-op peers record the same run.
    /// </summary>
    public static class BestRecords
    {
        private const string KDay   = "slop.best.day";
        private const string KCash  = "slop.best.cash";
        private const string KChain = "slop.best.chain";
        private const string KGrade = "slop.best.grade";

        /// <summary>Sentinel for "no grade recorded yet" — worse than any real grade (D = 4).</summary>
        public const int NoGrade = 99;

        /// <summary>Raised when the card celebrates a genuine record beat — drives the audio stinger.</summary>
        public static event Action OnNewRecord;

        public static int BestDay   => PlayerPrefs.GetInt(KDay, 0);
        public static int BestCash  => PlayerPrefs.GetInt(KCash, 0);
        public static int BestChain => PlayerPrefs.GetInt(KChain, 0);
        /// <summary>Best (= lowest) grade value ever earned, or <see cref="NoGrade"/> if none yet.</summary>
        public static int BestGrade => PlayerPrefs.GetInt(KGrade, NoGrade);

        /// <summary>Save only when strictly greater than the stored best. Returns true on a new record.</summary>
        public static bool SubmitDay(int day)     => Submit(KDay, day);
        public static bool SubmitCash(int cash)   => Submit(KCash, cash);
        public static bool SubmitChain(int chain) => Submit(KChain, chain);

        /// <summary>Grade is lower-is-better — save only when strictly lower than the stored best.
        /// Returns true on a new best grade.</summary>
        public static bool SubmitGrade(int gradeValue)
        {
            if (gradeValue >= PlayerPrefs.GetInt(KGrade, NoGrade)) return false;
            PlayerPrefs.SetInt(KGrade, gradeValue);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Fire the record cue (called once the card decides a beat is worth celebrating).</summary>
        public static void RaiseNewRecord() => OnNewRecord?.Invoke();

        private static bool Submit(string key, int value)
        {
            if (value <= PlayerPrefs.GetInt(key, 0)) return false;
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
            return true;
        }
    }
}
