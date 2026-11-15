using System.Collections.Generic;
using UnityEngine;

namespace Cevir.Core.LanguagePacks
{
    [CreateAssetMenu(menuName = "Chevir/Language Pack/Sign Dictionary", fileName = "SignDictionary")]
    public sealed class SignDictionary : ScriptableObject
    {
        [SerializeField] private List<SignDefinition> signs = new List<SignDefinition>();

        private Dictionary<string, SignDefinition> _byId;

        public IReadOnlyList<SignDefinition> Signs => signs;

        private void OnValidate()
        {
            _byId = null;
        }

        public SignDefinition Find(string signId)
        {
            if (_byId == null || _byId.Count != signs.Count)
            {
                _byId = new Dictionary<string, SignDefinition>();
                foreach (SignDefinition sign in signs)
                {
                    if (sign != null && !string.IsNullOrEmpty(sign.SignId))
                    {
                        _byId[sign.SignId] = sign;
                    }
                }
            }

            SignDefinition result;
            return _byId.TryGetValue(signId, out result) ? result : null;
        }

        /// <summary>Distractors for "İşarəni Tap": same similarity group first, then same category, then same kind.</summary>
        public List<SignDefinition> PickDistractors(SignDefinition target, int count, System.Random random)
        {
            var pool = new List<SignDefinition>();
            AddMatching(pool, target, s => !string.IsNullOrEmpty(target.SimilarityGroup) && s.SimilarityGroup == target.SimilarityGroup);
            if (pool.Count < count) AddMatching(pool, target, s => s.Category == target.Category);
            if (pool.Count < count) AddMatching(pool, target, s => s.Kind == target.Kind);

            Shuffle(pool, random);
            if (pool.Count > count) pool.RemoveRange(count, pool.Count - count);
            return pool;
        }

        private void AddMatching(List<SignDefinition> pool, SignDefinition target, System.Predicate<SignDefinition> predicate)
        {
            foreach (SignDefinition sign in signs)
            {
                if (sign != null && sign != target && !pool.Contains(sign) && sign.Gloss != target.Gloss && predicate(sign))
                {
                    pool.Add(sign);
                }
            }
        }

        private static void Shuffle<T>(IList<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

#if UNITY_EDITOR
        public void EditorAdd(SignDefinition sign)
        {
            if (!signs.Contains(sign)) signs.Add(sign);
            _byId = null;
        }

        /// <summary>Drops signs that are no longer part of the pack, and entries whose asset was deleted.</summary>
        public void EditorRemoveWhere(System.Predicate<SignDefinition> match)
        {
            signs.RemoveAll(s => s == null || match(s));
            _byId = null;
        }
#endif
    }
}
