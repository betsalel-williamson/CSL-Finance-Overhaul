using System.Collections.Generic; // List
using ColossalFramework.UI; // UIComponent, UIHorizontalAlignment, UILabel, UIPanel
using UnityEngine; // Color32, Mathf, Vector2, Vector3

namespace FinanceOverhaul
{
    /// <summary>
    /// A grid of key / value pairs (Figma: FO/StatGrid): rows of equal cells, each a muted key
    /// on the left, its value right-aligned, and a thin line under it, as the Stock Monitor's
    /// stats. A cell can span the cells to its right. A value that does not fit is scaled down
    /// (never below 0.6 of its size) instead of being cut off. Knows no feature: the caller sets
    /// the text and colour of each cell.
    /// </summary>
    internal sealed class FoStatGrid
    {
        private const float Gap = 6f;
        private const float MinScale = 0.6f;

        private readonly List<Cell> cells = new List<Cell>();
        private readonly int columns;
        private readonly float cellWidth;
        private readonly float spacing;
        private readonly float valueScale;

        private sealed class Cell
        {
            public UILabel Key;
            public UILabel Value;
            public UIPanel Line;
        }

        public UIPanel Panel { get; private set; }

        private FoStatGrid(int columns, float cellWidth, float spacing, float valueScale)
        {
            this.columns = columns;
            this.cellWidth = cellWidth;
            this.spacing = spacing;
            this.valueScale = valueScale;
        }

        /// <summary>
        /// A grid as wide as <paramref name="width"/>; value labels are named
        /// <c>name + "-" + row + "-" + column</c>, key labels <c>name + "Key-" + row + "-" + column</c>.
        /// </summary>
        public static FoStatGrid Add(
            UIComponent parent,
            string name,
            float width,
            int rows,
            int columns,
            float rowHeight,
            float keyScale,
            float valueScale,
            Color32 keyColour,
            Color32 lineColour
        )
        {
            const float spacing = 12f;
            float cellWidth = (width - spacing * (columns - 1)) / columns;
            var g = new FoStatGrid(columns, cellWidth, spacing, valueScale);
            g.Panel = parent.AddUIComponent<UIPanel>();
            g.Panel.name = name;
            g.Panel.size = new Vector2(width, rows * rowHeight);
            g.Panel.isInteractive = false;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    float x = col * (cellWidth + spacing),
                        y = row * rowHeight;
                    var cell = new Cell();
                    cell.Key = FoText.Label(
                        g.Panel,
                        name + "Key-" + row + "-" + col,
                        string.Empty,
                        keyScale,
                        keyColour
                    );
                    cell.Key.relativePosition = new Vector3(x, y);
                    cell.Value = FoText.Label(
                        g.Panel,
                        name + "-" + row + "-" + col,
                        string.Empty,
                        valueScale,
                        keyColour
                    );
                    cell.Value.textAlignment = UIHorizontalAlignment.Right;
                    cell.Value.relativePosition = new Vector3(x, y);
                    cell.Line = g.Panel.AddUIComponent<UIPanel>();
                    cell.Line.backgroundSprite = "EmptySprite"; // not shown: a game sprite name
                    cell.Line.color = lineColour;
                    cell.Line.size = new Vector2(cellWidth, 1f);
                    cell.Line.relativePosition = new Vector3(x, y + rowHeight - 4f);
                    cell.Line.isInteractive = false;
                    g.cells.Add(cell);
                }
            }

            return g;
        }

        /// <summary>
        /// One cell's key and value; <paramref name="span"/> cells wide (the cells it covers are
        /// hidden).
        /// </summary>
        public void Set(int row, int col, string key, string value, Color32 colour, int span = 1)
        {
            span = Mathf.Clamp(span, 1, columns - col);
            Cell cell = cells[row * columns + col];
            for (int c = 1; c < columns - col; c++)
            {
                Cell covered = cells[row * columns + col + c];
                bool shown = c >= span;
                covered.Key.isVisible = shown;
                covered.Value.isVisible = shown;
                covered.Line.isVisible = shown;
            }

            float x = col * (cellWidth + spacing),
                width = cellWidth * span + spacing * (span - 1);
            cell.Key.text = key;
            cell.Value.text = value;
            cell.Value.textColor = colour;
            cell.Line.width = width;
            Fit(cell, x, width);
        }

        /// <summary>The label a cell's value is shown in (for tests and tooltips).</summary>
        public UILabel Value(int row, int col)
        {
            return cells[row * columns + col].Value;
        }

        // The value right-aligned in the space the key leaves, scaled down if it needs more.
        private void Fit(Cell cell, float x, float width)
        {
            float available = width - cell.Key.width - Gap;
            cell.Value.textScale = valueScale;
            cell.Value.autoSize = true;
            float needed = cell.Value.width;
            if (needed > available && needed > 0f)
            {
                cell.Value.textScale = Mathf.Max(MinScale, valueScale * available / needed);
            }

            float height = cell.Value.height;
            cell.Value.autoSize = false;
            cell.Value.size = new Vector2(Mathf.Max(0f, available), height);
            cell.Value.relativePosition = new Vector3(
                x + cell.Key.width + Gap,
                cell.Key.relativePosition.y + (cell.Key.height - height) / 2f
            );
        }
    }
}
