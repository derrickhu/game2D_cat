using System;
using System.Collections.Generic;

namespace DressSort
{
    /// <summary>
    /// 活动页「同款装箱」。不碰 Unity。
    /// 先点一列，再点另一列：把第一列最上面连续的同款倒到第二列上面。
    /// 第二列是空的，或者最上面也是同款，并且还有空位，才能倒。倒不下的留在原列。
    /// 一列被同款放满就可以装箱。收纳箱满 4 格算一箱，装满 3 箱过关。
    /// </summary>
    public class PackBoard
    {
        public const int ColumnCount = 6;
        public const int ColumnHeight = 6;
        public const int StartOpen = 3;
        public const int ProgressColumn = 3;
        public const int AdColumnA = 4;
        public const int AdColumnB = 5;
        public const int BoxSlots = 4;
        public const int BoxesToWin = 3;

        public struct Cell
        {
            public int Type;
            public bool Revealed;
        }

        public enum TapResult
        {
            Ignored,
            Moved,
            Selected,
        }

        public struct PackResult
        {
            public int Column;
            public int Type;
            public bool Shipped;
            public bool Won;
        }

        readonly List<Cell>[] columns;
        readonly List<int> reserve = new List<int>();
        readonly bool[] open = new bool[ColumnCount];

        public int Selected { get; private set; } = -1;
        public int BoxFilled { get; private set; }
        public int BoxesDone { get; private set; }
        public bool Won { get; private set; }
        public int ReserveCount => reserve.Count;

        public PackBoard(int paletteSize, int seed)
        {
            columns = new List<Cell>[ColumnCount];
            for (int i = 0; i < ColumnCount; i++)
                columns[i] = new List<Cell>(ColumnHeight);
            for (int i = 0; i < StartOpen; i++)
                open[i] = true;
            Deal(Math.Max(1, paletteSize), seed);
        }

        public bool IsOpen(int column) => column >= 0 && column < ColumnCount && open[column];

        public bool IsAdColumn(int column) => column == AdColumnA || column == AdColumnB;

        public IReadOnlyList<Cell> Column(int index) => columns[index];

        public bool UnlockAd(int column)
        {
            if (!IsAdColumn(column) || open[column]) return false;
            open[column] = true;
            return true;
        }

        /// <summary>最上面连续同款的件数。列表第 0 个就是画面上最上面那件。</summary>
        public int GroupSize(int column)
        {
            if (!IsOpen(column)) return 0;
            List<Cell> list = columns[column];
            if (list.Count == 0) return 0;
            int type = list[0].Type;
            int count = 1;
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i].Type != type) break;
                count++;
            }
            return count;
        }

        /// <summary>从 from 倒到 to 实际能过去的件数。倒不了就是 0。</summary>
        public int PourCount(int from, int to)
        {
            if (!CanMove(from, to)) return 0;
            int space = ColumnHeight - columns[to].Count;
            int group = GroupSize(from);
            return space < group ? space : group;
        }

        public bool CanMove(int from, int to)
        {
            if (!IsOpen(from) || !IsOpen(to) || from == to) return false;
            if (GroupSize(from) <= 0) return false;
            List<Cell> dest = columns[to];
            if (dest.Count >= ColumnHeight) return false;
            if (dest.Count == 0) return true;
            return dest[0].Type == columns[from][0].Type;
        }

        public TapResult Tap(int column)
        {
            if (Won || !IsOpen(column)) return TapResult.Ignored;
            if (Selected < 0 || column == Selected || !CanMove(Selected, column))
            {
                Selected = column == Selected ? -1 : column;
                return TapResult.Selected;
            }

            Move(Selected, column);
            Selected = -1;
            return TapResult.Moved;
        }

        public bool IsFullUniform(int column)
        {
            if (!IsOpen(column)) return false;
            List<Cell> list = columns[column];
            if (list.Count != ColumnHeight) return false;
            int type = list[0].Type;
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i].Type != type) return false;
            }
            return true;
        }

        /// <summary>优先当前高亮列，否则从左到右第一列排满的。</summary>
        public int ReadyColumn()
        {
            if (Won) return -1;
            if (IsFullUniform(Selected)) return Selected;
            for (int c = 0; c < ColumnCount; c++)
            {
                if (IsFullUniform(c)) return c;
            }
            return -1;
        }

        public bool CanPack => ReadyColumn() >= 0;

        public PackResult Pack()
        {
            var result = new PackResult { Column = -1, Type = -1 };
            int column = ReadyColumn();
            if (column < 0) return result;

            List<Cell> list = columns[column];
            result.Column = column;
            result.Type = list[0].Type;
            list.Clear();

            BoxFilled++;
            if (BoxFilled >= BoxSlots)
            {
                BoxFilled = 0;
                BoxesDone++;
                result.Shipped = true;
                if (BoxesDone >= 1)
                    open[ProgressColumn] = true;
                if (BoxesDone >= BoxesToWin)
                {
                    Won = true;
                    result.Won = true;
                }
            }
            return result;
        }

        public bool CanRefill
        {
            get
            {
                if (Won || reserve.Count == 0) return false;
                for (int c = 0; c < ColumnCount; c++)
                {
                    if (IsOpen(c) && columns[c].Count < ColumnHeight) return true;
                }
                return false;
            }
        }

        /// <summary>把队列里的衣服补进每个已开列的空位，新来的叠在最上面。</summary>
        public int Refill()
        {
            if (!CanRefill) return 0;
            int placed = 0;
            for (int c = 0; c < ColumnCount; c++)
            {
                if (!IsOpen(c)) continue;
                while (columns[c].Count < ColumnHeight && reserve.Count > 0)
                {
                    PlaceIncoming(c, Take());
                    placed++;
                }
            }
            RevealReady();
            return placed;
        }

        public bool IsStuck
        {
            get
            {
                if (Won || CanPack || CanRefill) return false;
                for (int from = 0; from < ColumnCount; from++)
                {
                    for (int to = 0; to < ColumnCount; to++)
                    {
                        if (CanMove(from, to)) return false;
                    }
                }
                return true;
            }
        }

        public static string SelfCheck()
        {
            var board = new PackBoard(1, 3);
            if (board.ReserveCount <= 0) return "reserve";
            if (board.Selected != -1) return "preselected";
            if (!board.IsOpen(0) || board.IsOpen(ProgressColumn) || board.IsOpen(AdColumnA))
                return "open";
            if (board.Column(0).Count != 4) return "deal " + board.Column(0).Count;
            if (!board.Column(0)[0].Revealed) return "top hidden";

            if (board.Tap(0) != TapResult.Selected || board.Selected != 0) return "select";
            if (board.Tap(0) != TapResult.Selected || board.Selected != -1) return "deselect";

            // 最上面两件同款倒到另一列的顶上，底下不同款留着。空列也能接。
            board.SetColumn(0, 1, 1, 0);
            board.SetColumn(1, 1, 2);
            board.SetColumn(2);
            if (board.Tap(0) != TapResult.Selected) return "arm";
            if (board.Tap(1) != TapResult.Moved) return "pour";
            if (board.Column(0).Count != 1 || board.Column(0)[0].Type != 0) return "src left";
            if (board.Column(1).Count != 4
                || board.Column(1)[0].Type != 1
                || board.Column(1)[2].Type != 1
                || board.Column(1)[3].Type != 2)
                return "dst order";
            if (board.Selected != -1) return "still selected";

            board.SetColumn(0, 0, 0);
            if (board.Tap(0) != TapResult.Selected) return "arm empty";
            if (board.Tap(2) != TapResult.Moved) return "empty dest";
            if (board.Column(2).Count != 2 || board.Column(2)[0].Type != 0) return "emptied";

            // 顶上不同款时不倒，第二次点击改成选中这一列。
            board.SetColumn(0, 1);
            board.SetColumn(1, 0);
            if (board.Tap(0) != TapResult.Selected) return "arm mismatch";
            if (board.Tap(1) != TapResult.Selected || board.Column(0).Count != 1) return "mismatch";

            board.SetColumn(0, 0, 0, 0, 0, 0, 0);
            board.SetColumn(1, 0);
            if (board.Tap(1) != TapResult.Selected) return "arm full";
            if (board.ReadyColumn() != 0) return "ready " + board.ReadyColumn();
            PackResult packed = board.Pack();
            if (packed.Column != 0 || packed.Type != 0 || board.Column(0).Count != 0)
                return "pack";
            if (!board.CanRefill) return "refill";
            if (board.Refill() <= 0) return "placed";
            if (board.Column(0).Count == 0 || board.Column(0)[0].Type < 0) return "refilled top";
            if (!board.UnlockAd(AdColumnA) || !board.IsOpen(AdColumnA)) return "ad";
            if (board.UnlockAd(ProgressColumn)) return "progress unlocked early";
            return "ok";
        }

        void SetColumn(int column, params int[] types)
        {
            columns[column].Clear();
            for (int i = 0; i < types.Length; i++)
                columns[column].Add(new Cell { Type = types[i], Revealed = true });
        }

        void Deal(int paletteSize, int seed)
        {
            var random = new Random(seed);
            int groups = BoxesToWin * BoxSlots;
            var order = new int[groups];
            for (int i = 0; i < groups; i++)
                order[i] = i % paletteSize;
            for (int i = groups - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int tmp = order[i];
                order[i] = order[j];
                order[j] = tmp;
            }

            var chunks = new Queue<int>[groups];
            for (int i = 0; i < groups; i++)
            {
                chunks[i] = new Queue<int>();
                for (int k = 0; k < ColumnHeight; k++)
                    chunks[i].Enqueue(order[i]);
            }

            bool pending = true;
            while (pending)
            {
                pending = false;
                for (int i = 0; i < groups; i++)
                {
                    if (chunks[i].Count == 0) continue;
                    reserve.Add(chunks[i].Dequeue());
                    pending = true;
                }
            }

            for (int c = 0; c < StartOpen; c++)
            {
                for (int n = 0; n < 4 && reserve.Count > 0; n++)
                    PlaceIncoming(c, Take());
            }
        }

        void Move(int from, int to)
        {
            int count = PourCount(from, to);
            if (count <= 0) return;
            List<Cell> source = columns[from];
            List<Cell> dest = columns[to];
            List<Cell> moving = source.GetRange(0, count);
            source.RemoveRange(0, count);
            dest.InsertRange(0, moving);
            RevealReady();
        }

        void PlaceIncoming(int column, int type)
        {
            columns[column].Insert(0, new Cell { Type = type, Revealed = true });
        }

        int Take()
        {
            int type = reserve[0];
            reserve.RemoveAt(0);
            return type;
        }

        void RevealReady()
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                if (!IsFullUniform(c)) continue;
                List<Cell> list = columns[c];
                for (int i = 0; i < list.Count; i++)
                {
                    Cell cell = list[i];
                    cell.Revealed = true;
                    list[i] = cell;
                }
            }
        }
    }
}
