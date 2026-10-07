using System;
using System.Collections.Generic;

namespace DressSort
{
    /// <summary>
    /// 活动页「同款装箱」。不碰 Unity。
    /// 先点一列，再点另一列：只倒最上面连着的同款。
    /// 对面是空列，或者顶上也是同款，就能倒。空位有几件就倒几件，倒满为止，剩下的留在原列。
    /// 一列被同款放满就可以装箱。收纳箱满 4 格算一箱，装满 3 箱过关。
        /// 这一局裙子种类开局就定了。补充不用等台上清空：有空位的列各叠一件队列里的同款，原来的留在下面。
    /// </summary>
    public class PackBoard
    {
        public const int ColumnCount = 6;
        public const int ColumnHeight = 8;
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

        /// <summary>这一盘按记录的走法能装完。</summary>
        public bool Proven { get; private set; }

        public PackBoard(int paletteSize, int seed) : this()
        {
            Deal(Math.Max(1, paletteSize), seed);
        }

        PackBoard()
        {
            columns = new List<Cell>[ColumnCount];
            for (int i = 0; i < ColumnCount; i++)
                columns[i] = new List<Cell>(ColumnHeight);
            for (int i = 0; i < StartOpen; i++)
                open[i] = true;
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

        /// <summary>顶上同色，空列或者对方顶上也是同款。不管空位够不够。</summary>
        public bool TopsMatch(int from, int to)
        {
            if (!IsOpen(from) || !IsOpen(to) || from == to) return false;
            if (GroupSize(from) <= 0) return false;
            List<Cell> dest = columns[to];
            if (dest.Count == 0) return true;
            return dest[0].Type == columns[from][0].Type;
        }

        /// <summary>这次能倒过去的件数。顶上同色，有空位就倒，最多倒到把空位填满。</summary>
        public int PourCount(int from, int to)
        {
            if (!CanMove(from, to)) return 0;
            int space = ColumnHeight - columns[to].Count;
            int group = GroupSize(from);
            return space < group ? space : group;
        }

        public bool CanMove(int from, int to)
        {
            if (!TopsMatch(from, to)) return false;
            return columns[to].Count < ColumnHeight;
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

        public PackResult Pack() => PackAt(ReadyColumn());

        PackResult PackAt(int column)
        {
            var result = new PackResult { Column = -1, Type = -1 };
            if (column < 0 || !IsFullUniform(column)) return result;

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

        public bool HasOpenSpace
        {
            get
            {
                for (int c = 0; c < ColumnCount; c++)
                {
                    if (IsOpen(c) && columns[c].Count < ColumnHeight) return true;
                }
                return false;
            }
        }

        /// <summary>队列里还有这一局的裙子，并且已开的列上有空位，就可以补。不用等台上清空。</summary>
        public bool CanRefill => !Won && reserve.Count > 0 && HasOpenSpace;

        /// <summary>
        /// 每个还有空位的已开列叠一件。满列跳过，原来的衣服留在下面。
        /// 补上来的都是开局定好的那几种。
        /// </summary>
        public int Refill()
        {
            if (!CanRefill) return 0;
            int placed = 0;
            for (int c = 0; c < ColumnCount; c++)
            {
                if (!IsOpen(c) || columns[c].Count >= ColumnHeight) continue;
                if (reserve.Count == 0) break;
                PlaceIncoming(c, Take());
                placed++;
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
            for (int palette = 1; palette <= 4; palette++)
            {
                for (int seed = 1; seed <= 12; seed++)
                {
                    var dealt = new PackBoard(palette, seed * 17 + palette);
                    if (!dealt.Proven) return "unproven " + palette + ":" + seed;
                    if (dealt.Column(2).Count != 0) return "parking " + palette;
                    int onBoard = 0;
                    for (int c = 0; c < ColumnCount; c++)
                        onBoard += dealt.Column(c).Count;
                    if (onBoard != ColumnHeight * 2) return "board " + onBoard;
                    if (dealt.ReserveCount != ColumnHeight * 2 * 5) return "reserve " + dealt.ReserveCount;
                    if (!dealt.CanRefill) return "no refill";
                    if (palette > 1 && dealt.CanPack) return "already sorted";
                    var seen = new bool[palette];
                    for (int c = 0; c < 2; c++)
                    {
                        for (int i = 0; i < dealt.Column(c).Count; i++)
                            seen[dealt.Column(c)[i].Type] = true;
                    }
                    if (dealt.Refill() != 1 || dealt.Column(2).Count != 1) return "gap";
                    int added = dealt.Column(2)[0].Type;
                    if (added < 0 || added >= palette || !seen[added]) return "new dress";
                }
            }

            var again = new PackBoard(4, 42);
            var same = new PackBoard(4, 42);
            for (int c = 0; c < StartOpen; c++)
            {
                if (again.Column(c).Count != same.Column(c).Count) return "seed len";
                for (int i = 0; i < again.Column(c).Count; i++)
                {
                    if (again.Column(c)[i].Type != same.Column(c)[i].Type) return "seed";
                }
            }

            var board = new PackBoard(1, 3);
            if (!board.Proven) return "deal";
            if (board.Selected != -1) return "preselected";
            if (!board.IsOpen(0) || board.IsOpen(ProgressColumn) || board.IsOpen(AdColumnA))
                return "open";
            if (board.Column(0).Count != ColumnHeight) return "deal " + board.Column(0).Count;
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

            // 空位比这组少，就先把空位倒满。
            board.SetColumn(0, 0, 0, 0);
            board.SetColumn(1, 0, 0, 0, 0, 0, 0);
            if (board.Tap(0) != TapResult.Selected) return "arm tight";
            if (board.PourCount(0, 1) != 2) return "partial " + board.PourCount(0, 1);
            if (board.Tap(1) != TapResult.Moved || board.Column(0).Count != 1) return "split";
            if (board.Column(1).Count != ColumnHeight) return "filled";

            // 顶上不同款时不倒，第二次点击改成选中这一列。
            board.SetColumn(0, 1);
            board.SetColumn(1, 0);
            if (board.Tap(0) != TapResult.Selected) return "arm mismatch";
            if (board.Tap(1) != TapResult.Selected || board.Column(0).Count != 1) return "mismatch";

            board.SetColumn(0, 0, 0, 0, 0, 0, 0, 0, 0);
            board.SetColumn(1, 0);
            if (board.Tap(1) != TapResult.Selected) return "arm full";
            if (board.ReadyColumn() != 0) return "ready " + board.ReadyColumn();
            PackResult packed = board.Pack();
            if (packed.Column != 0 || packed.Type != 0 || board.Column(0).Count != 0)
                return "pack";
            // 台上还有衣服也能补。每个空位叠一件，底下原来的留着。
            board.SetColumn(0, 1, 1);
            board.SetColumn(1);
            board.SetColumn(2);
            if (!board.CanRefill) return "refill";
            if (board.Refill() != 3) return "placed";
            if (board.Column(0).Count != 3 || board.Column(0)[2].Type != 1) return "kept";
            if (!board.UnlockAd(AdColumnA) || !board.IsOpen(AdColumnA)) return "ad";
            if (board.UnlockAd(ProgressColumn)) return "progress unlocked early";

            EnsureCatalog();
            var scripted = new PackBoard();
            var fallback = scripted.MakeScreen(0, 1, new Random(1), true);
            var rest = new List<int> { 0, 0, 1, 1, 0, 1, 0, 1, 0, 1 };
            if (!scripted.Play(fallback, rest)) return "fallback";
            return "ok";
        }

        void SetColumn(int column, params int[] types)
        {
            columns[column].Clear();
            for (int i = 0; i < types.Length; i++)
                columns[column].Add(new Cell { Type = types[i], Revealed = true });
        }

        struct DealScreen
        {
            public int ColorA;
            public int ColorB;
            public int[] Top0;
            public int[] Top1;
            public int[] MoveFrom;
            public int[] MoveTo;
        }

        struct Pattern
        {
            public long State;
            public int[] Moves;
        }

        const int ColBits = 12;
        static List<Pattern> catalog;

        /// <summary>
        /// 12 组都是开局选定的那几种颜色。台上先摆两组混在一起，第三列空着。
        /// 其余留在队列里，补充时叠到有空位的列上，先补已经在台上的颜色。
        /// 空列连续补满就能装箱；台上那两列留到最后再倒开。同一种子同一盘。
        /// </summary>
        void Deal(int paletteSize, int seed)
        {
            var random = new Random(seed);
            int groups = BoxesToWin * BoxSlots;
            var colors = new int[paletteSize];
            for (int i = 0; i < paletteSize; i++)
                colors[i] = i;
            for (int i = paletteSize - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int tmp = colors[i];
                colors[i] = colors[j];
                colors[j] = tmp;
            }

            var count = new int[paletteSize];
            for (int i = 0; i < groups; i++)
                count[colors[i % paletteSize]]++;

            EnsureCatalog();
            int colorA = TakeColor(count, random, -1);
            int colorB = TakeColor(count, random, colorA);
            DealScreen screen = MakeScreen(colorA, colorB, random);
            List<int> rest = RemainingGroups(count, colorA, colorB);
            if (!Prove(screen, rest))
                screen = MakeScreen(colorA, colorB, random, true);
            if (!Prove(screen, rest)) return;

            Lay(screen, rest);
            Proven = true;
        }

        static List<int> RemainingGroups(int[] count, int colorA, int colorB)
        {
            var rest = new List<int>();
            for (int c = 0; c < count.Length; c++)
            {
                for (int n = 0; n < count[c]; n++)
                    rest.Add(c);
            }
            rest.Sort((a, b) =>
            {
                int pa = a == colorA || a == colorB ? 0 : 1;
                int pb = b == colorA || b == colorB ? 0 : 1;
                if (pa != pb) return pa - pb;
                return a - b;
            });
            return rest;
        }

        static int TakeColor(int[] count, Random random, int avoid)
        {
            int best = -1;
            int choices = 0;
            for (int i = 0; i < count.Length; i++)
            {
                if (count[i] <= 0 || i == avoid) continue;
                if (best < 0 || count[i] > count[best])
                {
                    best = i;
                    choices = 1;
                }
                else if (count[i] == count[best])
                {
                    choices++;
                    if (random.Next(choices) == 0) best = i;
                }
            }
            if (best < 0)
            {
                for (int i = 0; i < count.Length; i++)
                {
                    if (count[i] <= 0) continue;
                    best = i;
                    break;
                }
            }
            if (best >= 0) count[best]--;
            return best < 0 ? 0 : best;
        }

        static void EnsureCatalog()
        {
            if (catalog != null) return;
            long solved = StateOf(ColFrom(0, 0, 0, 0, 0, 0, 0, 0), ColFrom(1, 1, 1, 1, 1, 1, 1, 1), 0);
            var parent = new Dictionary<long, long>();
            var forward = new Dictionary<long, int>();
            var dist = new Dictionary<long, int>();
            var queue = new Queue<long>();
            parent[solved] = -1;
            dist[solved] = 0;
            queue.Enqueue(solved);
            var byDistance = new List<long>[11];
            for (int i = 0; i < byDistance.Length; i++)
                byDistance[i] = new List<long>();

            while (queue.Count > 0 && parent.Count < 200000)
            {
                long state = queue.Dequeue();
                if (dist[state] >= 10) continue;
                int nextDist = dist[state] + 1;
                for (int held = 0; held < 3; held++)
                {
                    for (int origin = 0; origin < 3; origin++)
                    {
                        if (held == origin) continue;
                        int group = GroupOf(StateCol(state, held));
                        for (int k = 1; k <= group; k++)
                        {
                            if (!Reverse(state, held, origin, k, out long next)) continue;
                            long source = StateCol(next, origin);
                            if (ColLen(source) == ColumnHeight && IsPure(source)) continue;
                            if (parent.ContainsKey(next)) continue;
                            parent[next] = state;
                            forward[next] = origin * 3 + held;
                            dist[next] = nextDist;
                            queue.Enqueue(next);
                            if (nextDist >= 4 && nextDist < byDistance.Length
                                && IsDealShape(next) && BothMixed(next)
                                && byDistance[nextDist].Count < 8)
                                byDistance[nextDist].Add(next);
                        }
                    }
                }
            }

            catalog = new List<Pattern>();
            catalog.Add(FallbackPattern());
            for (int d = 4; d < byDistance.Length; d++)
            {
                for (int i = 0; i < byDistance[d].Count; i++)
                {
                    long state = byDistance[d][i];
                    var moves = new List<int>();
                    long cursor = state;
                    while (parent[cursor] != -1)
                    {
                        moves.Add(forward[cursor]);
                        cursor = parent[cursor];
                    }
                    catalog.Add(new Pattern { State = state, Moves = moves.ToArray() });
                }
            }
        }

        static Pattern FallbackPattern()
        {
            return new Pattern
            {
                State = StateOf(ColFrom(0, 0, 0, 0, 1, 1, 1, 1), ColFrom(1, 1, 1, 1, 0, 0, 0, 0), 0),
                Moves = new[] { 0 * 3 + 2, 1 * 3 + 0, 2 * 3 + 1 },
            };
        }

        DealScreen MakeScreen(int colorA, int colorB, Random random, bool fallback = false)
        {
            if (colorA == colorB || catalog.Count == 0)
                return PureScreen(colorA);
            int pick = 0;
            if (!fallback && catalog.Count > 1)
                pick = 1 + random.Next(catalog.Count - 1);
            Pattern pattern = catalog[pick];
            var from = new int[pattern.Moves.Length];
            var to = new int[pattern.Moves.Length];
            for (int i = 0; i < pattern.Moves.Length; i++)
            {
                from[i] = pattern.Moves[i] / 3;
                to[i] = pattern.Moves[i] % 3;
            }
            return new DealScreen
            {
                ColorA = colorA,
                ColorB = colorB,
                Top0 = MapColumn(pattern.State, 0, colorA, colorB),
                Top1 = MapColumn(pattern.State, 1, colorA, colorB),
                MoveFrom = from,
                MoveTo = to,
            };
        }

        static DealScreen PureScreen(int color)
        {
            var row = new int[ColumnHeight];
            for (int i = 0; i < ColumnHeight; i++)
                row[i] = color;
            return new DealScreen
            {
                ColorA = color,
                ColorB = color,
                Top0 = row,
                Top1 = (int[])row.Clone(),
                MoveFrom = new int[0],
                MoveTo = new int[0],
            };
        }

        static int[] MapColumn(long state, int index, int colorA, int colorB)
        {
            long col = StateCol(state, index);
            int len = ColLen(col);
            var colors = new int[len];
            for (int i = 0; i < len; i++)
                colors[i] = ColColorAt(col, i) == 0 ? colorA : colorB;
            return colors;
        }

        bool Prove(DealScreen screen, List<int> groups)
        {
            var ghost = new PackBoard();
            return ghost.Play(screen, groups);
        }

        /// <summary>
        /// 前 4 组只进空着的第 3 列，每组 8 件同色，补满就装箱。第 4 箱打开第 4 列。
        /// 后面 6 组两列一起补，左一色右一色。最后再把开局那两列倒开装箱。
        /// </summary>
        bool Play(DealScreen screen, List<int> groups)
        {
            if (groups == null || groups.Count != 10) return false;
            PlaceTopFirst(0, screen.Top0);
            PlaceTopFirst(1, screen.Top1);
            EnqueueGroups(groups);

            for (int n = 0; n < 4; n++)
            {
                for (int i = 0; i < ColumnHeight; i++)
                {
                    if (!CanRefill || Refill() != 1) return false;
                }
                if (PackAt(2).Column != 2) return false;
            }
            if (!IsOpen(ProgressColumn)) return false;

            for (int n = 0; n < 3; n++)
            {
                for (int i = 0; i < ColumnHeight; i++)
                {
                    if (!CanRefill || Refill() != 2) return false;
                }
                if (PackAt(2).Column != 2 || PackAt(3).Column != 3) return false;
            }
            if (ReserveCount != 0) return false;
            if (!Matches(0, screen.Top0) || !Matches(1, screen.Top1)) return false;
            if (Column(2).Count != 0) return false;

            for (int m = 0; m < screen.MoveFrom.Length; m++)
            {
                if (PourCount(screen.MoveFrom[m], screen.MoveTo[m]) <= 0) return false;
                Move(screen.MoveFrom[m], screen.MoveTo[m]);
            }
            int packed = 0;
            if (!SweepPack(ref packed) || packed != 2) return false;
            return Won;
        }

        bool SweepPack(ref int packed)
        {
            for (int guard = 0; guard < 3; guard++)
            {
                int column = -1;
                for (int c = 0; c < StartOpen; c++)
                {
                    if (!IsFullUniform(c)) continue;
                    column = c;
                    break;
                }
                if (column < 0) return true;
                if (PackAt(column).Column < 0) return false;
                packed++;
            }
            return true;
        }

        void Lay(DealScreen screen, List<int> groups)
        {
            PlaceTopFirst(0, screen.Top0);
            PlaceTopFirst(1, screen.Top1);
            EnqueueGroups(groups);
        }

        void EnqueueGroups(List<int> groups)
        {
            for (int g = 0; g < 4; g++)
            {
                for (int i = 0; i < ColumnHeight; i++)
                    reserve.Add(groups[g]);
            }
            for (int p = 0; p < 3; p++)
            {
                int left = groups[4 + p * 2];
                int right = groups[4 + p * 2 + 1];
                for (int i = 0; i < ColumnHeight; i++)
                {
                    reserve.Add(left);
                    reserve.Add(right);
                }
            }
        }

        void PlaceTopFirst(int column, int[] topFirst)
        {
            for (int i = topFirst.Length - 1; i >= 0; i--)
                PlaceIncoming(column, topFirst[i]);
        }

        bool Matches(int column, int[] topFirst)
        {
            List<Cell> list = columns[column];
            if (list.Count != topFirst.Length) return false;
            for (int i = 0; i < topFirst.Length; i++)
            {
                if (list[i].Type != topFirst[i]) return false;
            }
            return true;
        }

        static bool IsDealShape(long state)
        {
            return ColLen(StateCol(state, 0)) == ColumnHeight
                && ColLen(StateCol(state, 1)) == ColumnHeight
                && ColLen(StateCol(state, 2)) == 0;
        }

        static bool BothMixed(long state)
        {
            return !IsPure(StateCol(state, 0)) && !IsPure(StateCol(state, 1));
        }

        static bool IsPure(long col)
        {
            int len = ColLen(col);
            if (len == 0) return false;
            int color = ColColorAt(col, 0);
            for (int i = 1; i < len; i++)
            {
                if (ColColorAt(col, i) != color) return false;
            }
            return true;
        }

        static long StateOf(long c0, long c1, long c2) => c0 | (c1 << ColBits) | (c2 << (ColBits * 2));

        static long StateCol(long state, int index) => (state >> (index * ColBits)) & 0xFFF;

        static int ColLen(long col) => (int)(col & 15);

        static int ColColorAt(long col, int index) => (int)((col >> (4 + index)) & 1);

        static long ColFrom(params int[] colors)
        {
            long packed = colors.Length;
            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i] != 0) packed |= 1L << (4 + i);
            }
            return packed;
        }

        static int GroupOf(long col)
        {
            int len = ColLen(col);
            if (len == 0) return 0;
            int color = ColColorAt(col, 0);
            int count = 1;
            while (count < len && ColColorAt(col, count) == color) count++;
            return count;
        }

        static long PushTop(long col, int color, int count)
        {
            int len = ColLen(col);
            long packed = len + count;
            for (int i = 0; i < count; i++)
            {
                if (color != 0) packed |= 1L << (4 + i);
            }
            for (int i = 0; i < len; i++)
            {
                if (ColColorAt(col, i) != 0) packed |= 1L << (4 + count + i);
            }
            return packed;
        }

        static long PopTop(long col, int count)
        {
            int len = ColLen(col);
            long packed = len - count;
            for (int i = 0; i < len - count; i++)
            {
                if (ColColorAt(col, i + count) != 0) packed |= 1L << (4 + i);
            }
            return packed;
        }

        /// <summary>
        /// 把 held 顶上 k 件挪到 origin。只有倒回去刚好还是这整组时才算合法逆操作。
        /// </summary>
        static bool Reverse(long state, int held, int origin, int k, out long next)
        {
            next = state;
            long source = StateCol(state, held);
            long dest = StateCol(state, origin);
            if (k <= 0 || k > GroupOf(source)) return false;
            if (ColLen(dest) + k > ColumnHeight) return false;
            int color = ColColorAt(source, 0);
            long sourceAfter = PopTop(source, k);
            long destAfter = PushTop(dest, color, k);
            int space = ColumnHeight - ColLen(sourceAfter);
            int group = GroupOf(destAfter);
            if (group != k || space < group) return false;
            if (ColLen(sourceAfter) > 0 && ColColorAt(sourceAfter, 0) != color) return false;
            long c0 = StateCol(state, 0);
            long c1 = StateCol(state, 1);
            long c2 = StateCol(state, 2);
            if (held == 0) c0 = sourceAfter;
            else if (held == 1) c1 = sourceAfter;
            else c2 = sourceAfter;
            if (origin == 0) c0 = destAfter;
            else if (origin == 1) c1 = destAfter;
            else c2 = destAfter;
            next = StateOf(c0, c1, c2);
            return true;
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
