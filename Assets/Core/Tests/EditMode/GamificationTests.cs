using System;
using Cevir.Core.Gamification;
using NUnit.Framework;

namespace Cevir.Core.Tests
{
    public sealed class GamificationTests
    {
        private static readonly DateTime Day = new DateTime(2026, 10, 26);

        [Test]
        public void EveryWrongCameraAttemptCostsAHeart()
        {
            int hearts = 10;
            var system = new HeartSystem(GamificationConfig.CreateDefault(), () => hearts, v => hearts = v);

            Assert.IsTrue(system.RegisterSoftFailure(1));
            Assert.IsTrue(system.RegisterSoftFailure(2));
            Assert.AreEqual(8, hearts);
        }

        [Test]
        public void HeartsNeverExceedMax()
        {
            int hearts = 4;
            var system = new HeartSystem(GamificationConfig.CreateDefault(), () => hearts, v => hearts = v);
            system.Restore(20);
            Assert.AreEqual(10, hearts);
        }

        [Test]
        public void OneHeartComesBackEveryTwoHours()
        {
            DateTime now = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);
            int hearts = 10;
            long regen = 0;
            var system = new HeartSystem(GamificationConfig.CreateDefault(), () => hearts, v => hearts = v, () => regen, v => regen = v, () => now);

            system.LoseHeart();
            system.LoseHeart();
            Assert.AreEqual(8, hearts);

            now = now.AddMinutes(119);
            system.Tick();
            Assert.AreEqual(8, hearts, "not yet");
            Assert.AreEqual(1, (int)Math.Round(system.TimeToNextHeart().TotalMinutes));

            now = now.AddMinutes(1);
            system.Tick();
            Assert.AreEqual(9, hearts);

            now = now.AddHours(10);
            system.Tick();
            Assert.AreEqual(10, hearts, "never above the maximum");
            Assert.AreEqual(0, regen, "the clock stops while full");
        }

        [Test]
        public void StreakGrowsOncePerDay()
        {
            var streak = new StreakSystem(GamificationConfig.CreateDefault(), new StreakState());
            Assert.IsTrue(streak.RegisterActivity(Day));
            Assert.IsFalse(streak.RegisterActivity(Day.AddHours(5)));
            Assert.IsTrue(streak.RegisterActivity(Day.AddDays(1)));
            Assert.AreEqual(2, streak.State.current);
        }

        [Test]
        public void OneMissedDayKeepsTheFlame()
        {
            var streak = new StreakSystem(GamificationConfig.CreateDefault(), new StreakState());
            for (int i = 0; i < 5; i++) streak.RegisterActivity(Day.AddDays(i)); // days 1-5
            streak.Evaluate(Day.AddDays(5)); // day 6 missed so far
            Assert.AreEqual(5, streak.State.current, "day 6 missed: the flame stays");
            Assert.IsFalse(streak.IsAtRisk(Day.AddDays(5)));
            Assert.IsTrue(streak.IsAtRisk(Day.AddDays(6)), "day 7: last chance");
            Assert.IsTrue(streak.RegisterActivity(Day.AddDays(6)));
            Assert.AreEqual(6, streak.State.current);
        }

        [Test]
        public void TwoMissedDaysPutTheFlameOut()
        {
            var streak = new StreakSystem(GamificationConfig.CreateDefault(), new StreakState());
            for (int i = 0; i < 5; i++) streak.RegisterActivity(Day.AddDays(i)); // days 1-5
            streak.Evaluate(Day.AddDays(7)); // days 6 and 7 missed
            Assert.AreEqual(0, streak.State.current);
            Assert.AreEqual(5, streak.State.longest);
        }

        [Test]
        public void FreezeCoversASecondMissedDay()
        {
            GamificationConfig config = GamificationConfig.CreateDefault();
            var xp = new XpSystem(config, new XpState());
            xp.Add(100, Day);
            var streak = new StreakSystem(config, new StreakState());
            streak.RegisterActivity(Day);

            Assert.IsTrue(streak.TryBuyFreeze(Day, xp));
            Assert.IsFalse(streak.TryBuyFreeze(Day, xp), "one freeze per week");
            Assert.AreEqual(50, xp.Balance);
            Assert.AreEqual(100, xp.Total, "spending does not reduce lifetime XP");

            streak.RegisterActivity(Day.AddDays(3));
            Assert.AreEqual(2, streak.State.current);
            Assert.AreEqual(0, streak.State.freezes);
        }

        [Test]
        public void XpRewardsFollowConfig()
        {
            var xp = new XpSystem(GamificationConfig.CreateDefault(), new XpState());
            Assert.AreEqual(10, xp.LessonReward(false));
            Assert.AreEqual(15, xp.LessonReward(true));
            Assert.AreEqual(5, xp.MiniGameReward(0f));
            Assert.AreEqual(20, xp.MiniGameReward(1f));
        }

        [Test]
        public void IsoWeekHandlesYearBoundary()
        {
            Assert.AreEqual("2026-W53", StreakSystem.IsoWeek(new DateTime(2027, 1, 1)));
            Assert.AreEqual("2026-W44", StreakSystem.IsoWeek(Day));
        }
    }
}
