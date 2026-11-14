using System;

namespace Cevir.Core.Gamification
{
    /// <summary>
    /// Hearts: a wrong answer costs one (camera attempts after <see cref="GamificationConfig.softFailuresPerHeart"/>
    /// failures on the same exercise). One heart comes back every <see cref="GamificationConfig.heartRegenMinutes"/>
    /// minutes until the hearts are full; the regeneration clock is persisted so it keeps running while the app is closed.
    /// </summary>
    public sealed class HeartSystem
    {
        private readonly GamificationConfig _config;
        private readonly Func<int> _get;
        private readonly Action<int> _set;
        private readonly Func<long> _getRegenStart;
        private readonly Action<long> _setRegenStart;
        private readonly Func<DateTime> _utcNow;

        public event Action<int> HeartsChanged;

        public HeartSystem(GamificationConfig config, Func<int> getHearts, Action<int> setHearts)
            : this(config, getHearts, setHearts, null, null, null)
        {
        }

        /// <param name="getRegenStart">UTC ticks when the current regeneration period started (0 = hearts full).</param>
        public HeartSystem(GamificationConfig config, Func<int> getHearts, Action<int> setHearts,
            Func<long> getRegenStart, Action<long> setRegenStart, Func<DateTime> utcNow)
        {
            _config = config;
            _get = getHearts;
            _set = setHearts;
            long local = 0;
            _getRegenStart = getRegenStart ?? (() => local);
            _setRegenStart = setRegenStart ?? (v => local = v);
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public TimeSpan RegenInterval => TimeSpan.FromMinutes(Math.Max(1, _config.heartRegenMinutes));
        public bool RetryCameraSignInPlace => _config.retryCameraSignInPlace;

        /// <summary>Adds the hearts earned since the last check. Call on start, on resume and periodically.</summary>
        public void Tick()
        {
            DateTime now = _utcNow();
            int hearts = _get();
            if (hearts >= _config.maxHearts)
            {
                _setRegenStart(0);
                return;
            }

            long start = _getRegenStart();
            if (start <= 0 || start > now.Ticks)
            {
                _setRegenStart(now.Ticks);
                return;
            }

            long period = RegenInterval.Ticks;
            long earned = (now.Ticks - start) / period;
            if (earned <= 0) return;

            int value = (int)Math.Min(_config.maxHearts, hearts + earned);
            _setRegenStart(value >= _config.maxHearts ? 0 : start + earned * period);
            Set(value);
        }

        /// <summary>Time until the next heart, or zero when the hearts are full.</summary>
        public TimeSpan TimeToNextHeart()
        {
            if (IsFull) return TimeSpan.Zero;
            long start = _getRegenStart();
            DateTime now = _utcNow();
            if (start <= 0) return RegenInterval;
            TimeSpan left = new DateTime(start, DateTimeKind.Utc) + RegenInterval - now;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }

        public int Hearts => _get();
        public int MaxHearts => _config.maxHearts;
        public bool HasHearts => _get() > 0;
        public bool IsFull => _get() >= _config.maxHearts;

        public int SoftFailuresPerHeart => _config.softFailuresPerHeart;

        /// <summary>Wrong answer in a quiz, or a failed gate question.</summary>
        public bool LoseHeart()
        {
            int hearts = _get();
            if (hearts <= 0) return false;
            Set(hearts - 1);
            return true;
        }

        /// <summary>Returns true when this soft failure (1-based count on the current exercise) costs a heart.</summary>
        public bool RegisterSoftFailure(int failuresOnExercise)
        {
            if (failuresOnExercise > 0 && failuresOnExercise % _config.softFailuresPerHeart == 0)
            {
                return LoseHeart();
            }

            return false;
        }

        public void Restore(int amount)
        {
            if (amount <= 0) return;
            Set(Math.Min(_config.maxHearts, _get() + amount));
        }

        public void RefillAll()
        {
            Set(_config.maxHearts);
        }

        private void Set(int value)
        {
            int before = _get();
            if (value == before) return;

            // The regeneration clock starts when the first heart is lost and stops when they are full again.
            if (before >= _config.maxHearts && value < _config.maxHearts) _setRegenStart(_utcNow().Ticks);
            else if (value >= _config.maxHearts) _setRegenStart(0);

            _set(value);
            if (HeartsChanged != null) HeartsChanged(value);
        }
    }
}
