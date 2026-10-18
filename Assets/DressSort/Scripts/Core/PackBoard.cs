using System;
using System.Collections.Generic;

namespace DressSort
{
    /// <summary>
    /// 活动页「衣服排排上车」。不碰 Unity。
    /// 七条衣架，一条装 8 件。先点一列再点另一列：最上面连着的同款倒过去，对面空着或顶上同款才行，倒满为止。
    /// 一列 8 件同款就能上车，一列坐一个车窗。四辆车 4、6、8、10 个座，坐满开走换下一辆，四辆都送走算赢。
    /// 第 5 列送走 1 辆车后开，第 6 列送走 2 辆后开，第 7 列看广告临时开，这辆车走了就收回。
    /// 「补充」从衣物门往开着的列顶上撒一把衣服，盖住的那几件变问号，露到顶上才翻开。
    /// 衣服按「每种 8 件一组」装进袋子再打散，所以场上每种总数一直凑得齐。
    /// 补充时多数衣服会盖到别的款式上，只留一小部分落到同款顶上。车越往后颜色越多、盖得越狠。
    /// 棋盘排满又没有整列同款时就会堵住，看广告加一列或收起顶上的衣服才能继续。
    /// 数值用机器人模拟调过：第一辆车自己能送走，从第二辆起几乎都要看广告才能继续，看了就能过。
    /// </summary>
    public class PackBoard
    {
        public const int ColumnCount = 7;
        public const int ColumnHeight = 8;
        public const int StartOpen = 4;
        public const int FirstUnlock = 4;
        public const int SecondUnlock = 5;
        public const int AdColumn = 6;
        public static readonly int[] CarSeats = { 4, 6, 8, 10 };
        public const int CarCount = 4;
        public const int MaxSeats = 10;
        public const int MaxColors = 5;

        static readonly int[] Colors = { 3, 5, 5, 5 };
        static readonly float[] Help = { 0.55f, 0.08f, 0.05f, 0.04f };
        static readonly float[] Clutter = { 0.25f, 0.9f, 0.95f, 0.97f };
        static readonly int[] RunMin = { 2, 1, 1, 1 };
        static readonly int[] RunMax = { 4, 2, 1, 1 };
        static readonly int[] RefillSize = { 8, 14, 16, 16 };
        const float EmptyBias = 0.4f;
        const int DealCount = 12;
        const int ReviveTake = 2;

        public struct Cell
        {
            public int Type;
            public bool Revealed;
        }

        public enum TapResult
        {
            Ignored,
            Selected,
            Moved,
        }

        /// <summary>补充时落下的一件，按落下先后排。</summary>
        public struct Drop
        {
            public int Column;
            public int Type;
        }

        /// <summary>一列坐进车里。CarDone 表示这一下把车坐满了。</summary>
        public struct Boarding
        {
            public int Column;
            public int Type;
            public int Car;
            public int Seat;
            public bool CarDone;
            public bool Won;
        }

        struct Chunk
        {
            public int Type;
            public int Count;
            public bool Back;
        }

        readonly List<Cell>[] columns = new List<Cell>[ColumnCount];
        readonly bool[] open = new bool[ColumnCount];
        readonly List<Chunk> bag = new List<Chunk>();
        readonly Random random;
        readonly int palette;

        public int Selected { get; private set; } = -1;
        public int Car { get; private set; }
        public int SeatsFilled { get; private set; }
        public bool Won { get; private set; }
        public int Moves { get; private set; }
        public int Refills { get; private set; }

        public int CurrentSeats => SeatsOf(Car);
        public int ColorCount => Math.Min(palette, Colors[Math.Min(Car, Colors.Length - 1)]);

        public PackBoard(int paletteSize, int seed)
        {
            palette = Math.Max(1, Math.Min(MaxColors, paletteSize));
            random = new Random(seed);
            for (int i = 0; i < ColumnCount; i++)
            {
                columns[i] = new List<Cell>(ColumnHeight);
                open[i] = i < StartOpen;
            }

            // 开局一列已经排满，第一下就能上车。另外三列撒一把。
            int ready = StartOpen - 1;
            for (int i = 0; i < ColumnHeight; i++)
                columns[ready].Add(new Cell { Type = 0, Revealed = true });
            var targets = new List<int>();
            for (int c = 0; c < ready; c++)
                targets.Add(c);
            Scatter(targets, DealCount, null);
            RevealTops();
        }

        public static int SeatsOf(int car)
        {
            if (car < 0) car = 0;
            if (car >= CarSeats.Length) car = CarSeats.Length - 1;
            return CarSeats[car];
        }

        public IReadOnlyList<Cell> Column(int index) => columns[index];

        public bool IsOpen(int column) => column >= 0 && column < ColumnCount && open[column];

        public bool IsAdColumn(int column) => column == AdColumn;

        /// <summary>锁着的列上竖着写的字。</summary>
        public string LockHint(int column)
        {
            if (IsAdColumn(column)) return "临时解锁";
            return column == SecondUnlock ? "2辆车后解锁" : "1辆车后解锁";
        }

        public bool UnlockAd()
        {
            if (open[AdColumn]) return false;
            open[AdColumn] = true;
            return true;
        }

        // ------------------------------------------------------------ 倒衣服

        /// <summary>最上面连续同款的件数。列表第 0 个是画面上最上面那件。</summary>
        public int GroupSize(int column)
        {
            if (!IsOpen(column)) return 0;
            List<Cell> list = columns[column];
            if (list.Count == 0) return 0;
            int type = list[0].Type;
            int count = 1;
            while (count < list.Count && list[count].Type == type)
                count++;
            return count;
        }

        public bool TopsMatch(int from, int to)
        {
            if (!IsOpen(from) || !IsOpen(to) || from == to) return false;
            if (columns[from].Count == 0 || IsFullUniform(from)) return false;
            List<Cell> dest = columns[to];
            return dest.Count == 0 || dest[0].Type == columns[from][0].Type;
        }

        public bool CanMove(int from, int to) => TopsMatch(from, to) && columns[to].Count < ColumnHeight;

        public int PourCount(int from, int to)
        {
            if (!CanMove(from, to)) return 0;
            return Math.Min(GroupSize(from), ColumnHeight - columns[to].Count);
        }

        public TapResult Tap(int column)
        {
            if (Won || !IsOpen(column)) return TapResult.Ignored;
            if (Selected >= 0 && Selected != column && CanMove(Selected, column))
            {
                Move(Selected, column);
                Selected = -1;
                Moves++;
                return TapResult.Moved;
            }
            if (column == Selected || columns[column].Count == 0 || IsFullUniform(column))
                Selected = -1;
            else
                Selected = column;
            return TapResult.Selected;
        }

        void Move(int from, int to)
        {
            int count = PourCount(from, to);
            if (count <= 0) return;
            List<Cell> moving = columns[from].GetRange(0, count);
            columns[from].RemoveRange(0, count);
            columns[to].InsertRange(0, moving);
            RevealTops();
        }

        // ------------------------------------------------------------ 上车

        public bool IsFullUniform(int column)
        {
            if (!IsOpen(column)) return false;
            List<Cell> list = columns[column];
            if (list.Count != ColumnHeight) return false;
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i].Type != list[0].Type) return false;
            }
            return true;
        }

        public bool CanBoard
        {
            get
            {
                if (Won) return false;
                for (int c = 0; c < ColumnCount; c++)
                {
                    if (IsFullUniform(c)) return true;
                }
                return false;
            }
        }

        /// <summary>所有排满的列从左到右依次上车。坐满一辆就换下一辆，剩下的接着坐。</summary>
        public List<Boarding> BoardAll()
        {
            var list = new List<Boarding>();
            for (int c = 0; c < ColumnCount && !Won; c++)
            {
                if (!IsFullUniform(c)) continue;
                var b = new Boarding
                {
                    Column = c,
                    Type = columns[c][0].Type,
                    Car = Car,
                    Seat = SeatsFilled,
                };
                columns[c].Clear();
                if (Selected == c) Selected = -1;
                SeatsFilled++;
                if (SeatsFilled >= CurrentSeats)
                {
                    b.CarDone = true;
                    SeatsFilled = 0;
                    Car++;
                    if (Car >= 1) open[FirstUnlock] = true;
                    if (Car >= 2) open[SecondUnlock] = true;
                    // 广告列是临时的：这辆车走的时候如果是空的，就收回，下一辆要再看一次。
                    if (columns[AdColumn].Count == 0) open[AdColumn] = false;
                    if (Car >= CarCount)
                    {
                        Won = true;
                        b.Won = true;
                    }
                }
                list.Add(b);
            }
            return list;
        }

        // ------------------------------------------------------------ 补充

        int FreeSpace()
        {
            int free = 0;
            for (int c = 0; c < ColumnCount; c++)
            {
                if (IsOpen(c) && !IsAdColumn(c))
                    free += ColumnHeight - columns[c].Count;
            }
            return free;
        }

        public int RefillAmount => Won ? 0 : Math.Min(RefillSize[Math.Min(Car, RefillSize.Length - 1)], FreeSpace());

        public bool CanRefill => RefillAmount > 0;

        /// <summary>按落下的先后返回。落完以后顶上那段同款翻开，其余是问号。</summary>
        public List<Drop> Refill()
        {
            var drops = new List<Drop>();
            int amount = RefillAmount;
            if (amount <= 0) return drops;
            var targets = new List<int>();
            for (int c = 0; c < ColumnCount; c++)
            {
                if (IsOpen(c) && !IsAdColumn(c))
                    targets.Add(c);
            }
            Selected = -1;
            Scatter(targets, amount, drops);
            Refills++;
            RevealTops();
            return drops;
        }

        void TopUpBag()
        {
            int colors = ColorCount;
            int step = Math.Min(Car, RunMin.Length - 1);
            while (bag.Count < 24)
            {
                var chunks = new List<Chunk>();
                for (int t = 0; t < colors; t++)
                {
                    int left = ColumnHeight;
                    while (left > 0)
                    {
                        int k = Math.Min(left, random.Next(RunMin[step], RunMax[step] + 1));
                        chunks.Add(new Chunk { Type = t, Count = k });
                        left -= k;
                    }
                }
                for (int i = chunks.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    Chunk tmp = chunks[i];
                    chunks[i] = chunks[j];
                    chunks[j] = tmp;
                }
                bag.AddRange(chunks);
            }
        }

        void Scatter(List<int> targets, int amount, List<Drop> record)
        {
            float help = Help[Math.Min(Car, Help.Length - 1)];
            float clutter = Clutter[Math.Min(Car, Clutter.Length - 1)];
            int placed = 0;
            var room = new List<int>();
            var match = new List<int>();
            var bury = new List<int>();
            var empty = new List<int>();
            while (placed < amount)
            {
                if (bag.Count == 0) TopUpBag();
                Chunk chunk = bag[0];
                room.Clear();
                match.Clear();
                bury.Clear();
                empty.Clear();
                for (int i = 0; i < targets.Count; i++)
                {
                    int c = targets[i];
                    int space = ColumnHeight - columns[c].Count;
                    if (space <= 0) continue;
                    room.Add(c);
                    if (columns[c].Count == 0) empty.Add(c);
                    else if (columns[c][0].Type == chunk.Type) match.Add(c);
                    else bury.Add(c);
                }
                if (room.Count == 0) break;

                int pick;
                if (chunk.Back && match.Count > 0)
                    pick = match[random.Next(match.Count)];
                else if (match.Count > 0 && random.NextDouble() < help)
                    pick = match[random.Next(match.Count)];
                else if (bury.Count > 0 && random.NextDouble() < clutter)
                    pick = Tightest(bury);
                else if (empty.Count > 0 && random.NextDouble() < EmptyBias)
                    pick = empty[random.Next(empty.Count)];
                else
                    pick = Weighted(room);

                int k = Math.Min(chunk.Count, Math.Min(amount - placed, ColumnHeight - columns[pick].Count));
                for (int i = 0; i < k; i++)
                {
                    columns[pick].Insert(0, new Cell { Type = chunk.Type, Revealed = false });
                    record?.Add(new Drop { Column = pick, Type = chunk.Type });
                }
                placed += k;
                chunk.Count -= k;
                if (chunk.Count <= 0) bag.RemoveAt(0);
                else bag[0] = chunk;
            }
        }

        int Tightest(List<int> bury)
        {
            int best = bury[0];
            int bestSpace = ColumnHeight - columns[best].Count;
            for (int i = 1; i < bury.Count; i++)
            {
                int space = ColumnHeight - columns[bury[i]].Count;
                if (space < bestSpace)
                {
                    bestSpace = space;
                    best = bury[i];
                }
            }
            return best;
        }

        int Weighted(List<int> room)
        {
            int total = 0;
            for (int i = 0; i < room.Count; i++)
            {
                int s = ColumnHeight - columns[room[i]].Count;
                total += s * s;
            }
            int roll = random.Next(Math.Max(1, total));
            for (int i = 0; i < room.Count; i++)
            {
                int s = ColumnHeight - columns[room[i]].Count;
                roll -= s * s;
                if (roll < 0) return room[i];
            }
            return room[room.Count - 1];
        }

        void RevealTops()
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                List<Cell> list = columns[c];
                if (list.Count == 0) continue;
                int top = GroupSize(c);
                if (!open[c]) top = 0;
                for (int i = 0; i < top; i++)
                {
                    Cell cell = list[i];
                    if (cell.Revealed) continue;
                    cell.Revealed = true;
                    list[i] = cell;
                }
            }
        }

        // ------------------------------------------------------------ 卡住与复活

        public bool HasMove
        {
            get
            {
                for (int from = 0; from < ColumnCount; from++)
                {
                    for (int to = 0; to < ColumnCount; to++)
                    {
                        if (CanMove(from, to)) return true;
                    }
                }
                return false;
            }
        }

        public bool IsStuck => !Won && !CanBoard && !CanRefill && !HasMove;

        /// <summary>
        /// 看广告收起：每列顶上最多两件放回袋子最前面，下次补充优先落回同款上。返回收走的件，按列。
        /// </summary>
        public List<Drop> Revive()
        {
            var taken = new List<Drop>();
            Selected = -1;
            for (int c = 0; c < ColumnCount; c++)
            {
                if (!IsOpen(c)) continue;
                List<Cell> list = columns[c];
                int n = Math.Min(ReviveTake, list.Count);
                if (IsFullUniform(c)) n = 0;
                for (int i = 0; i < n; i++)
                {
                    taken.Add(new Drop { Column = c, Type = list[0].Type });
                    bag.Insert(0, new Chunk { Type = list[0].Type, Count = 1, Back = true });
                    list.RemoveAt(0);
                }
            }
            RevealTops();
            return taken;
        }

        // ------------------------------------------------------------ 自检

        /// <summary>机器人照着玩完整局。返回 "ok" 或出错的原因。</summary>
        public static string SelfCheck()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var board = new PackBoard(MaxColors, seed * 31 + 7);
                if (!board.CanBoard) return "opening " + seed;
                var seen = new HashSet<string>();
                int guard = 0;
                while (!board.Won && guard++ < 4000)
                {
                    if (board.CanBoard) { board.BoardAll(); seen.Clear(); continue; }
                    if (BotStep(board, seen)) continue;
                    if (board.CanRefill) { board.Refill(); continue; }
                    if (!board.UnlockAd() && board.Revive().Count == 0) return "dead " + seed;
                }
                if (!board.Won) return "unfinished " + seed;
                for (int c = 0; c < ColumnCount; c++)
                {
                    if (board.Column(c).Count > ColumnHeight) return "overflow " + seed;
                }
            }
            return "ok";
        }

        /// <summary>挑一步没走到过的局面。同款叠高优先，其次挪去空列把下面翻出来。</summary>
        static bool BotStep(PackBoard b, HashSet<string> seen)
        {
            seen.Add(b.Snapshot());
            int bestFrom = -1, bestTo = -1, bestScore = 0;
            for (int from = 0; from < ColumnCount; from++)
            {
                for (int to = 0; to < ColumnCount; to++)
                {
                    if (!b.CanMove(from, to)) continue;
                    int n = b.PourCount(from, to);
                    bool whole = n == b.GroupSize(from);
                    int score;
                    if (b.columns[to].Count > 0)
                        score = 20 + b.columns[to].Count + n + (whole ? 5 : 0);
                    else if (whole && n < b.columns[from].Count)
                        score = 5 + n;
                    else
                        continue;
                    if (score <= bestScore || seen.Contains(b.SnapshotAfter(from, to))) continue;
                    bestScore = score;
                    bestFrom = from;
                    bestTo = to;
                }
            }
            if (bestFrom < 0) return false;
            b.Selected = -1;
            b.Tap(bestFrom);
            return b.Tap(bestTo) == TapResult.Moved;
        }

        string Snapshot()
        {
            var sb = new System.Text.StringBuilder();
            for (int c = 0; c < ColumnCount; c++)
            {
                foreach (Cell cell in columns[c])
                    sb.Append((char)('a' + cell.Type));
                sb.Append('|');
            }
            return sb.ToString();
        }

        string SnapshotAfter(int from, int to)
        {
            int n = PourCount(from, to);
            var sb = new System.Text.StringBuilder();
            for (int c = 0; c < ColumnCount; c++)
            {
                List<Cell> list = columns[c];
                if (c == to)
                {
                    for (int i = 0; i < n; i++)
                        sb.Append((char)('a' + columns[from][i].Type));
                }
                for (int i = c == from ? n : 0; i < list.Count; i++)
                    sb.Append((char)('a' + list[i].Type));
                sb.Append('|');
            }
            return sb.ToString();
        }
    }
}
