using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>Sets a fixed-column grid's cell width from the width it really gets, so cards never overflow.</summary>
    [RequireComponent(typeof(GridLayoutGroup))]
    [ExecuteAlways]
    public sealed class GridAutoFit : MonoBehaviour
    {
        private GridLayoutGroup _grid;
        private float _width = -1f;

        private void OnEnable()
        {
            _grid = GetComponent<GridLayoutGroup>();
            Fit();
        }

        private void OnRectTransformDimensionsChange()
        {
            Fit();
        }

        private void Fit()
        {
            if (_grid == null || _grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount) return;
            float width = ((RectTransform)transform).rect.width;
            if (width <= 0f || Mathf.Approximately(width, _width)) return;
            _width = width;
            int columns = Mathf.Max(1, _grid.constraintCount);
            float cell = (width - _grid.padding.horizontal - _grid.spacing.x * (columns - 1)) / columns;
            _grid.cellSize = new Vector2(Mathf.Floor(cell), _grid.cellSize.y);
        }
    }
}
