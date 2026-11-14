namespace Cevir.Core.Recognition
{
    /// <summary>
    /// Averages the user's neutral face over a short calibration window. Subtracting it from live
    /// samples keeps people with naturally high brows or an open-mouth resting face from passing
    /// (or failing) non-manual checks by default.
    /// </summary>
    public sealed class NonManualBaseline
    {
        private NonManualSample _sum;
        private int _count;

        public int SampleCount => _count;

        public void Reset()
        {
            _sum = default;
            _count = 0;
        }

        public void Add(NonManualSample sample)
        {
            if (!sample.present)
            {
                return;
            }

            _sum.browRaise += sample.browRaise;
            _sum.browFurrow += sample.browFurrow;
            _sum.mouthOpen += sample.mouthOpen;
            _sum.mouthRound += sample.mouthRound;
            _sum.cheekPuff += sample.cheekPuff;
            _sum.headRoll += sample.headRoll;
            _sum.headPitch += sample.headPitch;
            _sum.headYaw += sample.headYaw;
            _count++;
        }

        public NonManualSample Average()
        {
            if (_count == 0)
            {
                return default;
            }

            float inv = 1f / _count;
            return new NonManualSample
            {
                present = true,
                browRaise = _sum.browRaise * inv,
                browFurrow = _sum.browFurrow * inv,
                mouthOpen = _sum.mouthOpen * inv,
                mouthRound = _sum.mouthRound * inv,
                cheekPuff = _sum.cheekPuff * inv,
                headRoll = _sum.headRoll * inv,
                headPitch = _sum.headPitch * inv,
                headYaw = _sum.headYaw * inv
            };
        }
    }
}
