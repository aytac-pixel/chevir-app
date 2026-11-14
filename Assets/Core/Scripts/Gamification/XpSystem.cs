using System;
using UnityEngine;

namespace Cevir.Core.Gamification
{
    [Serializable]
    public sealed class XpState
    {
        /// <summary>Lifetime XP, never decreases.</summary>
        public int total;

        /// <summary>Spendable XP (streak freezes). Spending does not reduce league or lifetime XP.</summary>
        public int balance;

        /// <summary>XP earned in the current league week, and which ISO week that is.</summary>
        public int weekly;
        public string week = "";
    }

    public sealed class XpSystem
    {
        private readonly GamificationConfig _config;
        private readonly XpState _state;

        public event Action<XpState, int> XpGained;

        public XpSystem(GamificationConfig config, XpState state)
        {
            _config = config;
            _state = state;
        }

        public int Total => _state.total;
        public int Balance => _state.balance;
        public int Weekly => _state.weekly;
        public XpState State => _state;

        /// <summary>XP earned in the ISO week of <paramref name="today"/> (0 if nothing was earned yet that week).</summary>
        public int WeeklyFor(DateTime today)
        {
            return _state.week == StreakSystem.IsoWeek(today) ? _state.weekly : 0;
        }

        public int LessonReward(bool perfectCamera)
        {
            return _config.lessonXp + (perfectCamera ? _config.perfectCameraBonusXp : 0);
        }

        /// <summary>Mini-games pay between the configured min and max based on a 0..1 performance score.</summary>
        public int MiniGameReward(float normalizedScore)
        {
            return Mathf.RoundToInt(Mathf.Lerp(_config.miniGameMinXp, _config.miniGameMaxXp, Mathf.Clamp01(normalizedScore)));
        }

        public void Add(int amount, DateTime today)
        {
            if (amount <= 0) return;

            string week = StreakSystem.IsoWeek(today);
            if (_state.week != week)
            {
                _state.week = week;
                _state.weekly = 0;
            }

            _state.total += amount;
            _state.balance += amount;
            _state.weekly += amount;
            if (XpGained != null) XpGained(_state, amount);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || _state.balance < amount) return false;
            _state.balance -= amount;
            return true;
        }
    }
}
