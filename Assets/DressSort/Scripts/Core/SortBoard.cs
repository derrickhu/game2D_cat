using System;
using System.Collections.Generic;

namespace DressSort
{
    /// <summary>
    /// 棋盘的纯逻辑，不碰任何 Unity 类型。
    ///
    /// 每一列用一个 List 表示，索引 0 是视觉上的最上面一件，最后一个是最下面一件。
    /// 玩家点一列：手里那件插到该列最上面，该列最下面那件顶出来到手里。
    /// 手里那件落到中间后，若某列只剩一件异款且正好是这列的款，异款会立刻转到列底。
    /// </summary>
    public class SortBoard
    {
        public readonly struct Settle
        {
            public readonly int Column;
            public readonly int FromIndex;

            public Settle(int column, int fromIndex)
            {
                Column = column;
                FromIndex = fromIndex;
            }
        }
        public const int Empty = -1;

        readonly List<int>[] columns;
        readonly Stack<int> history = new Stack<int>();
        readonly Stack<Settle[]> settleHistory = new Stack<Settle[]>();

        static readonly Settle[] NoSettles = Array.Empty<Settle>();

        public int ColumnCount { get; }
        public int ColumnHeight { get; }

        /// <summary>款式数量。问号那件的编号就等于它，排在所有款式之后。</summary>
        public int PaletteSize { get; }

        public int MysteryIndex => PaletteSize;

        public int Held { get; private set; } = Empty;
        public int Steps { get; private set; }
        public int MoveLimit { get; }

        /// <summary>最近一次手里的裙子落下后，自动换到列底的那些异款。</summary>
        public Settle[] LastSettles { get; private set; } = NoSettles;

        public SortBoard(int columnCount, int columnHeight, int paletteSize, int moveLimit)
        {
            ColumnCount = columnCount;
            ColumnHeight = columnHeight;
            PaletteSize = paletteSize;
            MoveLimit = moveLimit;

            columns = new List<int>[columnCount];
            for (int i = 0; i < columnCount; i++)
                columns[i] = new List<int>(columnHeight + 1);
        }

        public IReadOnlyList<int> Column(int index) => columns[index];

        /// <summary>编辑器自检用：直接铺一盘指定局面。</summary>
        public void Load(int[][] src, int held)
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                columns[c].Clear();
                if (src != null && c < src.Length && src[c] != null)
                {
                    for (int i = 0; i < src[c].Length; i++)
                        columns[c].Add(src[c][i]);
                }
            }
            Held = held;
            Steps = 0;
            history.Clear();
            settleHistory.Clear();
            LastSettles = NoSettles;
        }

        public int CountIn(int index) => columns[index].Count;

        public bool CanUndo => history.Count > 0;

        public bool OutOfMoves => Steps >= MoveLimit;

        // ------------------------------------------------------------ 生成

        /// <summary>
        /// 从已解状态倒推生成。倒着走的每一步都是正向走法的逆操作，
        /// 所以把这串操作正着走一遍就能解开。
        /// </summary>
        public void Deal(int scrambleMoves, int seed)
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                columns[c].Clear();
                for (int r = 0; r < ColumnHeight; r++)
                    columns[c].Add(c);
            }
            Held = MysteryIndex;

            var random = new System.Random(seed);
            int previous = -1;
            for (int k = 0; k < scrambleMoves; k++)
            {
                int c = random.Next(ColumnCount);
                if (c == previous && ColumnCount > 1)
                    c = (c + 1 + random.Next(ColumnCount - 1)) % ColumnCount;
                previous = c;
                ReverseMove(c);
            }

            Steps = 0;
            history.Clear();
            settleHistory.Clear();
            SettleReady();
        }

        // ------------------------------------------------------------ 走子

        public bool CanPlay(int column) =>
            column >= 0 && column < ColumnCount
            && Held != Empty
            && !OutOfMoves
            && !IsColumnSolved(column);

        /// <summary>
        /// 一列只剩一件异款、手里又是这列的款时，把那件异款挪到列底，
        /// 这样下一步交换会直接把它顶出来。已经在列底则不必挪。
        /// </summary>
        public bool TryAutoRotate(int column, out int oddIndex)
        {
            oddIndex = -1;
            if (Held == Empty || Held == MysteryIndex) return false;
            if (column < 0 || column >= ColumnCount) return false;
            List<int> list = columns[column];
            if (list.Count < 2) return false;

            int odd = -1;
            int matches = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == Held)
                {
                    matches++;
                    continue;
                }
                if (odd >= 0) return false;
                odd = i;
            }

            if (odd < 0 || matches != list.Count - 1) return false;
            if (odd == list.Count - 1) return false;
            oddIndex = odd;
            return true;
        }

        /// <summary>
        /// 手里的裙子已经在中间时调用：把所有「只剩一件异款」的列里，
        /// 那件异款挪到列底。发牌和每次交换后都会走一遍。
        /// </summary>
        public Settle[] SettleReady()
        {
            var list = new List<Settle>();
            for (int c = 0; c < ColumnCount; c++)
            {
                if (!TryAutoRotate(c, out int odd)) continue;
                RotateToBottom(columns[c], odd);
                list.Add(new Settle(c, odd));
            }
            LastSettles = list.Count == 0 ? NoSettles : list.ToArray();
            return LastSettles;
        }

        /// <summary>手里那件插到列顶，列底那件顶出来。新到手的那件若对上某列，异款立刻沉底。</summary>
        public int Play(int column)
        {
            List<int> list = columns[column];
            list.Insert(0, Held);

            int last = list.Count - 1;
            int outgoing = list[last];
            list.RemoveAt(last);

            Held = outgoing;
            Steps++;
            history.Push(column);
            settleHistory.Push(SettleReady());
            return outgoing;
        }

        public int Undo()
        {
            if (history.Count == 0) return Empty;
            int column = history.Pop();
            Settle[] settles = settleHistory.Count > 0 ? settleHistory.Pop() : NoSettles;
            for (int i = settles.Length - 1; i >= 0; i--)
                MoveBottomTo(columns[settles[i].Column], settles[i].FromIndex);
            int outgoing = ReverseMove(column);
            LastSettles = NoSettles;
            if (Steps > 0) Steps--;
            return outgoing;
        }

        static void RotateToBottom(List<int> list, int index)
        {
            int item = list[index];
            list.RemoveAt(index);
            list.Add(item);
        }

        static void MoveBottomTo(List<int> list, int index)
        {
            if (list.Count == 0) return;
            int item = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            index = MathfClamp(index, 0, list.Count);
            list.Insert(index, item);
        }

        static int MathfClamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        int ReverseMove(int column)
        {
            List<int> list = columns[column];
            list.Add(Held);

            int outgoing = list[0];
            list.RemoveAt(0);

            Held = outgoing;
            return outgoing;
        }

        // ------------------------------------------------------------ 判定

        public bool IsColumnSolved(int column)
        {
            List<int> list = columns[column];
            if (list.Count != ColumnHeight) return false;
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i] != list[0]) return false;
            }
            return true;
        }

        /// <summary>
        /// 每种款式的数量正好等于列高，所以只要每列都同款，
        /// 各列的款式必然互不相同，不用额外查重。
        /// </summary>
        public bool IsWin()
        {
            if (Held != MysteryIndex) return false;
            for (int c = 0; c < ColumnCount; c++)
            {
                if (!IsColumnSolved(c)) return false;
            }
            return true;
        }
    }
}
