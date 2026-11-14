using System.Collections.Generic;
using Cevir.Core.Stories;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>The name being fingerspelled, one row per word, with the current letter highlighted.</summary>
    public sealed class FingerspellTilesUI : MonoBehaviour
    {
        [SerializeField] private RectTransform rows;
        [SerializeField] private RectTransform rowPrefab;
        [SerializeField] private LetterTileUI tilePrefab;

        private readonly List<LetterTileUI> _tiles = new List<LetterTileUI>();
        private readonly List<RectTransform> _rows = new List<RectTransform>();
        private FingerspellingTask _task;

        public void Build(FingerspellingTask task)
        {
            _task = task;
            foreach (LetterTileUI tile in _tiles) Destroy(tile.gameObject);
            foreach (RectTransform row in _rows) Destroy(row.gameObject);
            _tiles.Clear();
            _rows.Clear();
            if (task == null) return;

            RectTransform currentRow = null;
            int word = -1;
            foreach (SpelledLetter letter in task.Letters)
            {
                if (letter.WordIndex != word)
                {
                    word = letter.WordIndex;
                    currentRow = Instantiate(rowPrefab, rows);
                    currentRow.gameObject.SetActive(true);
                    _rows.Add(currentRow);
                }

                LetterTileUI tile = Instantiate(tilePrefab, currentRow);
                tile.gameObject.SetActive(true);
                _tiles.Add(tile);
            }

            Refresh();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rows);
        }

        public void Refresh()
        {
            if (_task == null) return;
            for (int i = 0; i < _tiles.Count && i < _task.Letters.Count; i++)
            {
                _tiles[i].Bind(_task.Letters[i], i == _task.CurrentIndex);
            }
        }
    }
}
