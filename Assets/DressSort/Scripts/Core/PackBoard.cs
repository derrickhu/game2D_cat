using System;
using System.Collections.Generic;

namespace DressSort
{
    /// <summary>
    /// 活动页「同款装箱」。不碰 Unity。
    /// 先点一列，再点另一列：只倒最上面连着的同款。
    /// 对面是空列，或者顶上也是同款，就能倒。空位有几件就倒几件，倒满为止，剩下的留在原列。
    /// 一列被同款放满就可以装箱。三箱越装越大：4 格、6 格、8 格，装满过关。
    /// 第 4 列装完 1 箱打开，第 5 列装完 2 箱打开，第 6 列看广告临时开。
    /// 开局照参考局：一列同色全亮、顶上压 1 件别的；旁边几件能补满的，底下几张问号。空列留着挪那件压着的。
    /// 每局换列、换裙子，也换亮着的件数、问号张数、一次补上来的件数。倒法不变：差几件就倒几件，凑满 8 件装箱。
    /// 问号翻开才是刚才那件的颜色。第 4 列打开以后，一次摆出两列快装满的，连装两次。
    /// 摆新局面的那一叠，只在目标列都空着时才放。补满的那一叠，只接到已经露出的同色上。
    /// </summary>
    public class PackBoard
    {
        public const int ColumnCount = 6;
        public const int ColumnHeight = 8;
        public const int StartOpen = 3;
        public const int ProgressColumn = 3;
        public const int SecondColumn = 4;
        public const int AdColumn = 5;
        public static readonly int[] BoxPlan = { 4, 6, 8 };
        public const int BoxesToWin = 3;
        public const int GroupsToWin = 18;
        public const int MaxSeats = 8;

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
        readonly List<Drop> queue = new List<Drop>();
        readonly List<int> chunkSize = new List<int>();
        readonly List<bool> chunkOnEmpty = new List<bool>();
        readonly List<WavePlan> plans = new List<WavePlan>();
        readonly bool[] open = new bool[ColumnCount];
        int queueHead;
        int nextChunk;
        bool scriptedPuzzle;

        public int Selected { get; private set; } = -1;
        public int BoxFilled { get; private set; }
        public int BoxesDone { get; private set; }
        public bool Won { get; private set; }
        public int ReserveCount => queue.Count - queueHead;

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

        public bool IsAdColumn(int column) => column == AdColumn;

        public static int SeatsOf(int boxIndex)
        {
            if (boxIndex < 0) boxIndex = 0;
            if (boxIndex >= BoxPlan.Length) boxIndex = BoxPlan.Length - 1;
            return BoxPlan[boxIndex];
        }

        public int CurrentSeats => SeatsOf(BoxesDone);

        public string LockHint(int column)
        {
            if (IsAdColumn(column)) return "临时解锁";
            if (column == SecondColumn) return "装2箱后开";
            return "装1箱后开";
        }

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
            if (Selected == column)
                Selected = -1;
            list.Clear();

            int seats = CurrentSeats;
            BoxFilled++;
            if (BoxFilled >= seats)
            {
                BoxFilled = 0;
                BoxesDone++;
                result.Shipped = true;
                if (BoxesDone >= 1)
                    open[ProgressColumn] = true;
                if (BoxesDone >= 2)
                    open[SecondColumn] = true;
                if (BoxesDone >= BoxesToWin)
                {
                    Won = true;
                    result.Won = true;
                }
            }
            return result;
        }

        /// <summary>下一叠能整叠放上去：目标列有空位，空着或者顶上已经是同一件。</summary>
        public bool CanRefill => !Won && ChunkFits();

        /// <summary>
        /// 放下预先排好的下一叠。一叠要么全放下，要么一件都不放。
        /// 广告列不收。放下以后，顶上新露出来的同色会翻开。
        /// </summary>
        public int Refill()
        {
            if (!CanRefill) return 0;
            int size = chunkSize[nextChunk];
            for (int i = 0; i < size; i++)
            {
                Drop drop = queue[queueHead + i];
                columns[drop.Column].Insert(0, new Cell { Type = drop.Type, Revealed = drop.Revealed });
            }
            queueHead += size;
            nextChunk++;
            RevealReady();
            return size;
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
                    if (BoxPlan[0] + BoxPlan[1] + BoxPlan[2] != GroupsToWin) return "plan";
                    if (dealt.Column(AdColumn).Count != 0) return "ad start";
                    if (palette == 1)
                    {
                        if (dealt.Column(0).Count != ColumnHeight || !dealt.CanPack) return "pure";
                        if (dealt.Column(2).Count != 0) return "pure buffer";
                        if (dealt.ReserveCount != ColumnHeight * (GroupsToWin - 1))
                            return "pure reserve " + dealt.ReserveCount;
                    }
                    else
                    {
                        if (!OpeningShape(dealt)) return "opening " + palette + ":" + seed;
                        if (dealt.CanPack || dealt.CanRefill) return "opening free";
                    }
                    int onBoard = 0;
                    for (int c = 0; c < ColumnCount; c++)
                        onBoard += dealt.Column(c).Count;
                    if (onBoard + dealt.ReserveCount != ColumnHeight * GroupsToWin)
                        return "reserve " + palette + ":" + seed;
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
                    if (again.Column(c)[i].Revealed != same.Column(c)[i].Revealed) return "seed face";
                }
            }

            bool buried = false;
            for (int c = 0; c < StartOpen; c++)
            {
                for (int i = 0; i < again.Column(c).Count; i++)
                {
                    if (i == 0 && !again.Column(c)[i].Revealed) return "top buried";
                    if (!again.Column(c)[i].Revealed) buried = true;
                }
            }
            if (!buried) return "no mystery";

            var board = new PackBoard(1, 3);
            if (!board.Proven) return "deal";
            if (board.Selected != -1) return "preselected";
            if (!board.IsOpen(0) || board.IsOpen(ProgressColumn) || board.IsOpen(SecondColumn) || board.IsOpen(AdColumn))
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
            if (!board.UnlockAd(AdColumn) || !board.IsOpen(AdColumn)) return "ad";
            if (board.UnlockAd(ProgressColumn) || board.UnlockAd(SecondColumn)) return "progress unlocked early";

            var live = new PackBoard(3, 4);
            int pile, donor, buffer;
            if (!FindOpening(live, out pile, out donor, out buffer)) return "live";
            int blocker = live.Column(pile)[0].Type;
            int body = live.Column(pile)[1].Type;
            int keys = 0;
            while (keys < live.Column(donor).Count && live.Column(donor)[keys].Type == body)
                keys++;
            int hidden = live.Column(donor).Count - keys;
            if (live.Tap(pile) != TapResult.Selected) return "arm pile";
            if (live.Tap(buffer) != TapResult.Moved) return "to buffer";
            if (live.Column(buffer).Count != 1 || live.Column(buffer)[0].Type != blocker) return "held";
            if (live.CanRefill) return "refill early";
            if (live.Tap(donor) != TapResult.Selected) return "arm donor";
            if (live.Tap(pile) != TapResult.Moved) return "complete";
            if (!live.IsFullUniform(pile) || live.Column(pile)[0].Type != body) return "ready body";
            if (!live.Column(donor)[0].Revealed || live.Column(donor)[0].Type != blocker) return "flip";
            if (!live.CanRefill) return "refill late";
            if (live.Tap(buffer) != TapResult.Selected) return "arm buffer";
            if (live.Tap(donor) != TapResult.Moved || live.Column(donor).Count != hidden + 1) return "merge";
            if (!live.UnlockAd(AdColumn)) return "ad live";
            if (live.Refill() != 7 - hidden || live.Column(AdColumn).Count != 0) return "ad refill";
            if (!live.IsFullUniform(donor) || live.Column(donor)[0].Type != blocker) return "second";

            var shapes = new HashSet<string>();
            var pileSizes = new HashSet<int>();
            var donorSizes = new HashSet<int>();
            for (int s = 1; s <= 30; s++)
            {
                var varied = new PackBoard(4, s * 13 + 5);
                if (!varied.Proven) return "variety proof " + s;
                int vp, vd, vb;
                if (!FindOpening(varied, out vp, out vd, out vb)) return "variety shape";
                pileSizes.Add(varied.Column(vp).Count);
                donorSizes.Add(varied.Column(vd).Count);
                shapes.Add(vp + ":" + vd + ":" + vb + ":" + varied.Column(vp).Count
                    + ":" + varied.Column(vd).Count + ":" + varied.Column(vp)[0].Type);
            }
            if (shapes.Count < 6) return "variety " + shapes.Count;
            if (pileSizes.Count < 2 || donorSizes.Count < 2) return "qty";
            return "ok";
        }

        static bool OpeningShape(PackBoard dealt)
        {
            int pile, donor, buffer;
            if (!FindOpening(dealt, out pile, out donor, out buffer)) return false;
            int blocker = dealt.Column(pile)[0].Type;
            int body = dealt.Column(pile)[1].Type;
            if (blocker == body || !dealt.Column(pile)[0].Revealed) return false;
            for (int i = 1; i < dealt.Column(pile).Count; i++)
            {
                if (dealt.Column(pile)[i].Type != body || !dealt.Column(pile)[i].Revealed) return false;
            }
            int keys = 0;
            while (keys < dealt.Column(donor).Count
                && dealt.Column(donor)[keys].Type == body
                && dealt.Column(donor)[keys].Revealed)
                keys++;
            if (keys < 1 || dealt.Column(pile).Count != ColumnHeight - keys + 1) return false;
            if (keys >= dealt.Column(donor).Count) return false;
            for (int i = keys; i < dealt.Column(donor).Count; i++)
            {
                if (dealt.Column(donor)[i].Type != blocker || dealt.Column(donor)[i].Revealed) return false;
            }
            return true;
        }

        static bool FindOpening(PackBoard dealt, out int pile, out int donor, out int buffer)
        {
            pile = -1;
            donor = -1;
            buffer = -1;
            for (int c = 0; c < StartOpen; c++)
            {
                IReadOnlyList<Cell> column = dealt.Column(c);
                if (column.Count == 0)
                {
                    if (buffer >= 0) return false;
                    buffer = c;
                    continue;
                }
                bool hidden = false;
                for (int i = 0; i < column.Count; i++)
                {
                    if (!column[i].Revealed) hidden = true;
                }
                if (hidden)
                {
                    if (donor >= 0) return false;
                    donor = c;
                }
                else
                {
                    if (pile >= 0) return false;
                    pile = c;
                }
            }
            return pile >= 0 && donor >= 0 && buffer >= 0;
        }

        void SetColumn(int column, params int[] types)
        {
            columns[column].Clear();
            for (int i = 0; i < types.Length; i++)
                columns[column].Add(new Cell { Type = types[i], Revealed = true });
        }

        struct Drop
        {
            public int Column;
            public int Type;
            public bool Revealed;
        }

        struct Part
        {
            public int Type;
            public bool Revealed;
            public int Count;
        }

        void Deal(int paletteSize, int seed)
        {
            Build(paletteSize, seed);
            var ghost = new PackBoard();
            ghost.Build(paletteSize, seed);
            Proven = ghost.PlayAll();
        }

        struct WavePlan
        {
            public int Pile;
            public int Donor;
            public int Buffer;
            public int Merge;
            public int Body;
            public int Blocker;
            public int Hidden;
            public int Keys;
            public bool Wide;
        }

        void Build(int paletteSize, int seed)
        {
            scriptedPuzzle = paletteSize > 1;
            if (!scriptedPuzzle)
            {
                Lay(Stack(0, P(0, true, ColumnHeight)));
                for (int i = 1; i < GroupsToWin; i++)
                    Commit(Stack(2, P(0, true, ColumnHeight)), true);
                return;
            }

            plans.AddRange(MakePlans(paletteSize, seed));
            Lay(Layout(plans[0]));
            Commit(Finish(plans[0]), false);
            for (int w = 1; w < plans.Count; w++)
            {
                Commit(Layout(plans[w]), true);
                if (!plans[w].Wide)
                    Commit(Finish(plans[w]), false);
            }
        }

        static List<WavePlan> MakePlans(int paletteSize, int seed)
        {
            var random = new Random(seed);
            var color = new int[paletteSize];
            for (int i = 0; i < paletteSize; i++)
                color[i] = i;
            Shuffle(color, random);

            int waves = GroupsToWin / 2;
            int narrowWaves = BoxPlan[0] / 2;
            int cursor = random.Next(paletteSize);
            var list = new List<WavePlan>(waves);
            for (int w = 0; w < waves; w++)
            {
                int body = color[cursor % paletteSize];
                int blocker = color[(cursor + 1) % paletteSize];
                if (random.Next(2) == 0)
                {
                    int tmp = body;
                    body = blocker;
                    blocker = tmp;
                }
                cursor++;
                if (paletteSize > 2 && random.Next(3) == 0)
                    cursor++;

                bool wide = w >= narrowWaves;
                int[] cols = wide
                    ? new[] { 0, 1, 2, ProgressColumn }
                    : new[] { 0, 1, 2 };
                Shuffle(cols, random);
                int hidden = 2 + random.Next(3);
                int keysMax = Math.Min(3, 6 - hidden);
                list.Add(new WavePlan
                {
                    Pile = cols[0],
                    Donor = cols[1],
                    Buffer = cols[2],
                    Merge = wide ? cols[3] : -1,
                    Body = body,
                    Blocker = blocker,
                    Hidden = hidden,
                    Keys = 1 + random.Next(keysMax),
                    Wide = wide,
                });
            }
            return list;
        }

        static void Shuffle(int[] values, Random random)
        {
            for (int i = values.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int tmp = values[i];
                values[i] = values[j];
                values[j] = tmp;
            }
        }

        /// <summary>一列同色全亮、顶上压 1 件。旁边若干件补满用的压在问号上。宽局再加一列同色，方便连装。</summary>
        static List<Drop> Layout(WavePlan plan)
        {
            var drops = new List<Drop>();
            if (plan.Wide)
                drops.AddRange(Stack(plan.Merge, P(plan.Blocker, true, 7 - plan.Hidden)));
            drops.AddRange(Stack(plan.Pile, P(plan.Blocker, true, 1), P(plan.Body, true, ColumnHeight - plan.Keys)));
            drops.AddRange(Stack(plan.Donor, P(plan.Body, true, plan.Keys), P(plan.Blocker, false, plan.Hidden)));
            return drops;
        }

        static List<Drop> Finish(WavePlan plan)
        {
            int count = 7 - plan.Hidden;
            if (count <= 1)
                return Stack(plan.Donor, P(plan.Blocker, true, count));
            return Stack(plan.Donor, P(plan.Blocker, true, 1), P(plan.Blocker, false, count - 1));
        }

        static Part P(int type, bool revealed, int count)
        {
            return new Part { Type = type, Revealed = revealed, Count = count };
        }

        /// <summary>parts 从顶上往下写。存进列表时改成从底下往上，方便之后插到列顶。</summary>
        static List<Drop> Stack(int column, params Part[] topFirst)
        {
            var list = new List<Drop>();
            for (int p = topFirst.Length - 1; p >= 0; p--)
            {
                for (int n = 0; n < topFirst[p].Count; n++)
                {
                    list.Add(new Drop
                    {
                        Column = column,
                        Type = topFirst[p].Type,
                        Revealed = topFirst[p].Revealed,
                    });
                }
            }
            return list;
        }

        void Lay(List<Drop> drops)
        {
            for (int i = 0; i < drops.Count; i++)
            {
                columns[drops[i].Column].Insert(0, new Cell
                {
                    Type = drops[i].Type,
                    Revealed = drops[i].Revealed,
                });
            }
            RevealReady();
        }

        void Commit(List<Drop> drops, bool onEmpty)
        {
            if (drops.Count == 0) return;
            queue.AddRange(drops);
            chunkSize.Add(drops.Count);
            chunkOnEmpty.Add(onEmpty);
        }

        bool ChunkFits()
        {
            if (nextChunk >= chunkSize.Count) return false;
            int size = chunkSize[nextChunk];
            if (queueHead + size > queue.Count) return false;
            var incoming = new int[ColumnCount];
            var newTop = new int[ColumnCount];
            var touched = new bool[ColumnCount];
            for (int i = 0; i < size; i++)
            {
                Drop drop = queue[queueHead + i];
                if (drop.Column < 0 || drop.Column >= ColumnCount) return false;
                if (!IsOpen(drop.Column) || IsAdColumn(drop.Column)) return false;
                incoming[drop.Column]++;
                newTop[drop.Column] = drop.Type;
                touched[drop.Column] = true;
            }
            bool onEmpty = chunkOnEmpty[nextChunk];
            for (int c = 0; c < ColumnCount; c++)
            {
                if (!touched[c]) continue;
                if (columns[c].Count + incoming[c] > ColumnHeight) return false;
                if (onEmpty)
                {
                    if (columns[c].Count != 0) return false;
                }
                else if (columns[c].Count == 0 || columns[c][0].Type != newTop[c])
                {
                    return false;
                }
            }
            return true;
        }

        bool PlayAll()
        {
            if (!scriptedPuzzle) return PlayPure();
            if (plans.Count != GroupsToWin / 2) return false;
            if (!SolveWave3(plans[0])) return false;
            for (int w = 1; w < plans.Count; w++)
            {
                WavePlan plan = plans[w];
                int expect = plan.Wide ? 16 : 9 + plan.Hidden;
                if (plan.Wide && !IsOpen(ProgressColumn)) return false;
                if (Refill() != expect) return false;
                bool solved = plan.Wide ? SolveWave4(plan) : SolveWave3(plan);
                if (!solved) return false;
            }
            return Won && ReserveCount == 0 && Column(AdColumn).Count == 0;
        }

        bool PlayPure()
        {
            if (PackAt(0).Column != 0) return false;
            for (int i = 1; i < GroupsToWin; i++)
            {
                if (Refill() != ColumnHeight) return false;
                if (PackAt(2).Column != 2) return false;
            }
            return Won && ReserveCount == 0 && Column(AdColumn).Count == 0;
        }

        bool SolveWave3(WavePlan plan)
        {
            if (PourCount(plan.Pile, plan.Buffer) != 1) return false;
            Move(plan.Pile, plan.Buffer);
            if (PourCount(plan.Donor, plan.Pile) != plan.Keys) return false;
            Move(plan.Donor, plan.Pile);
            if (PackAt(plan.Pile).Column != plan.Pile) return false;
            if (PourCount(plan.Buffer, plan.Donor) != 1) return false;
            Move(plan.Buffer, plan.Donor);
            if (Refill() != 7 - plan.Hidden) return false;
            if (PackAt(plan.Donor).Column != plan.Donor) return false;
            if (Column(plan.Pile).Count != 0 || Column(plan.Donor).Count != 0 || Column(plan.Buffer).Count != 0)
                return false;
            return true;
        }

        bool SolveWave4(WavePlan plan)
        {
            int merge = 7 - plan.Hidden;
            if (PourCount(plan.Pile, plan.Buffer) != 1) return false;
            Move(plan.Pile, plan.Buffer);
            if (PourCount(plan.Donor, plan.Pile) != plan.Keys) return false;
            Move(plan.Donor, plan.Pile);
            if (PackAt(plan.Pile).Column != plan.Pile) return false;
            if (PourCount(plan.Buffer, plan.Donor) != 1) return false;
            Move(plan.Buffer, plan.Donor);
            if (PourCount(plan.Merge, plan.Donor) != merge) return false;
            Move(plan.Merge, plan.Donor);
            if (PackAt(plan.Donor).Column != plan.Donor) return false;
            if (Column(plan.Pile).Count != 0 || Column(plan.Donor).Count != 0
                || Column(plan.Buffer).Count != 0 || Column(plan.Merge).Count != 0)
                return false;
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


        void RevealReady()
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                List<Cell> list = columns[c];
                if (list.Count == 0) continue;
                bool all = IsFullUniform(c);
                int top = GroupSize(c);
                for (int i = 0; i < list.Count; i++)
                {
                    if (!all && i >= top) continue;
                    Cell cell = list[i];
                    if (cell.Revealed) continue;
                    cell.Revealed = true;
                    list[i] = cell;
                }
            }
        }
    }
}
