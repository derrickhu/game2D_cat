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
    ///
    /// 关卡机制：
    /// 包裹：发牌时盖住的格子，看不到是哪款，落到列底才拆开。
    /// 防尘罩：罩住的列不能点，也看不到里面，叠好指定数量的其它列后拉开，之后一直开着。
    /// 专属列：列顶挂着款式气泡，这列只认这一款。
    /// 锁和钥匙：锁住的列看得见但不能点；带钥匙的那件被顶出来时，从左往右开一把锁。
    /// 限时闹钟：挂闹钟的那件要在倒数归零前被顶出来，否则闹钟响了这局失败。
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

        /// <summary>格子上的机制标记跟着格子一起移动，所以直接编码在格子的值里。</summary>
        const int TypeMask = 0xFF;
        const int WrapBit = 1 << 8;
        const int KeyBit = 1 << 9;
        const int AlarmShift = 10;
        const int AlarmMask = 0xF << AlarmShift;
        const int MaxAlarms = 15;

        /// <summary>只在发牌自检时用：给每格编号，回放时看它第几步被顶出来。</summary>
        const int TagShift = 16;
        const int TagMask = 0xFFF << TagShift;

        /// <summary>一步记录。Row 为 -1 是普通插列，否则是「交换」换走的那一格。</summary>
        readonly struct Move
        {
            public readonly int Column;
            public readonly int Row;

            public Move(int column, int row)
            {
                Column = column;
                Row = row;
            }
        }

        readonly List<int>[] columns;
        readonly int[] coverNeed;
        readonly bool[] opened;
        readonly bool[] lockAtStart;
        readonly bool[] unlocked;
        readonly int[] target;
        readonly int[] alarmDeadline = new int[MaxAlarms + 1];
        readonly Stack<Move> history = new Stack<Move>();
        readonly Stack<Settle[]> settleHistory = new Stack<Settle[]>();
        readonly List<int> recipe = new List<int>();
        int lastEjectedRaw;

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

        /// <summary>最近一步刚拉开防尘罩的列。</summary>
        public int[] LastOpened { get; private set; } = Array.Empty<int>();

        /// <summary>最近一步用钥匙打开的列。</summary>
        public int[] LastUnlocked { get; private set; } = Array.Empty<int>();

        /// <summary>最近一步顶出来的那件挂着闹钟，也就是关掉了一个。</summary>
        public bool LastAlarmOff { get; private set; }

        /// <summary>发牌时倒推的走法正着排一遍。编辑器自检拿它确认能解。</summary>
        public IReadOnlyList<int> Recipe => recipe;

        /// <summary>发牌后按倒推走法正着回放过一遍，确认在步数和闹钟限制内能解开。</summary>
        public bool Verified { get; private set; }

        public SortBoard(int columnCount, int columnHeight, int paletteSize, int moveLimit)
        {
            ColumnCount = columnCount;
            ColumnHeight = columnHeight;
            PaletteSize = paletteSize;
            MoveLimit = moveLimit;

            columns = new List<int>[columnCount];
            for (int i = 0; i < columnCount; i++)
                columns[i] = new List<int>(columnHeight + 1);
            coverNeed = new int[columnCount];
            opened = new bool[columnCount];
            lockAtStart = new bool[columnCount];
            unlocked = new bool[columnCount];
            target = new int[columnCount];
            for (int i = 0; i < columnCount; i++)
                target[i] = Empty;
        }

        SortBoard(SortBoard src, bool openLocks)
            : this(src.ColumnCount, src.ColumnHeight, src.PaletteSize, src.MoveLimit)
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                columns[c].AddRange(src.columns[c]);
                coverNeed[c] = src.coverNeed[c];
                opened[c] = src.opened[c];
                lockAtStart[c] = src.lockAtStart[c];
                unlocked[c] = openLocks || src.unlocked[c];
                target[c] = src.target[c];
            }
            Array.Copy(src.alarmDeadline, alarmDeadline, alarmDeadline.Length);
            Held = src.Held;
        }

        /// <summary>每列要先叠好几列才拉开防尘罩，0 表示没有罩。发牌前设。</summary>
        public void SetCovers(IReadOnlyList<int> needs)
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                coverNeed[c] = needs != null && c < needs.Count ? Math.Max(0, needs[c]) : 0;
                opened[c] = false;
            }
        }

        /// <summary>大于 0 的列开局上锁。发牌前设。</summary>
        public void SetLocks(IReadOnlyList<int> flags)
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                lockAtStart[c] = flags != null && c < flags.Count && flags[c] > 0 && coverNeed[c] == 0;
                unlocked[c] = !lockAtStart[c];
            }
        }

        /// <summary>大于 0 的列挂专属气泡。已解状态下第 c 列就是第 c 款，所以气泡款式就是列号。</summary>
        public void SetTargets(IReadOnlyList<int> flags)
        {
            for (int c = 0; c < ColumnCount; c++)
                target[c] = flags != null && c < flags.Count && flags[c] > 0 ? c : Empty;
        }

        /// <summary>只给画面读，返回去掉标记的款式。</summary>
        public IReadOnlyList<int> Column(int index)
        {
            List<int> raw = columns[index];
            var types = new int[raw.Count];
            for (int i = 0; i < raw.Count; i++)
                types[i] = raw[i] & TypeMask;
            return types;
        }

        int Cell(int column, int row)
        {
            List<int> list = columns[column];
            return row >= 0 && row < list.Count ? list[row] : 0;
        }

        public bool IsWrapped(int column, int row) => (Cell(column, row) & WrapBit) != 0;

        public bool HasKey(int column, int row) => (Cell(column, row) & KeyBit) != 0;

        /// <summary>这格闹钟还剩几步，没有闹钟返回 -1。</summary>
        public int AlarmLeft(int column, int row)
        {
            int alarm = (Cell(column, row) & AlarmMask) >> AlarmShift;
            return alarm > 0 ? alarmDeadline[alarm] - Steps : -1;
        }

        public bool IsCovered(int column) => coverNeed[column] > 0 && !opened[column];

        public bool HasLock(int column) => lockAtStart[column];

        public bool IsLocked(int column) => !unlocked[column];

        /// <summary>专属列认的款式，没有返回 -1。</summary>
        public int TargetOf(int column) => target[column];

        public int CoverNeed(int column) => coverNeed[column];

        /// <summary>防尘罩还差几列才拉开。</summary>
        public int CoverLeft(int column) =>
            IsCovered(column) ? Math.Max(0, coverNeed[column] - SolvedCount) : 0;

        /// <summary>已经叠好的列数，罩着的不算。</summary>
        public int SolvedCount
        {
            get
            {
                int count = 0;
                for (int c = 0; c < ColumnCount; c++)
                {
                    if (!IsCovered(c) && IsColumnSolved(c)) count++;
                }
                return count;
            }
        }

        /// <summary>有闹钟倒数归零还没被顶出来，也就是响了。</summary>
        public bool AlarmRang
        {
            get
            {
                for (int c = 0; c < ColumnCount; c++)
                {
                    List<int> list = columns[c];
                    for (int r = 0; r < list.Count; r++)
                    {
                        int alarm = (list[r] & AlarmMask) >> AlarmShift;
                        if (alarm > 0 && alarmDeadline[alarm] - Steps <= 0) return true;
                    }
                }
                return false;
            }
        }

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
                unlocked[c] = !lockAtStart[c];
            }
            Held = held;
            Steps = 0;
            history.Clear();
            settleHistory.Clear();
            recipe.Clear();
            ResetLast();
        }

        /// <summary>编辑器自检用：给某格套上包裹。</summary>
        public void Wrap(int column, int row)
        {
            columns[column][row] |= WrapBit;
        }

        /// <summary>编辑器自检用：给某格挂钥匙。</summary>
        public void PutKey(int column, int row)
        {
            columns[column][row] |= KeyBit;
        }

        /// <summary>编辑器自检用：给某格挂闹钟，steps 步内要顶出来。</summary>
        public void PutAlarm(int column, int row, int steps)
        {
            int id = 1;
            while (id < MaxAlarms && alarmDeadline[id] > 0) id++;
            alarmDeadline[id] = Steps + steps;
            columns[column][row] = (columns[column][row] & ~AlarmMask) | (id << AlarmShift);
        }

        public int CountIn(int index) => columns[index].Count;

        public bool CanUndo => history.Count > 0;

        public bool OutOfMoves => Steps >= MoveLimit;

        void ResetLast()
        {
            LastSettles = NoSettles;
            LastOpened = Array.Empty<int>();
            LastUnlocked = Array.Empty<int>();
            LastAlarmOff = false;
        }

        // ------------------------------------------------------------ 生成

        /// <summary>
        /// 从已解状态倒推生成。倒着走的每一步都是正向走法的逆操作，
        /// 所以把这串操作正着走一遍就能解开。
        ///
        /// 有防尘罩或锁时分两段倒推，正着走就是：先只动没罩没锁的列，
        /// 叠好够数的列拉开罩子、顶出钥匙开锁，再动其余的列。
        /// 生成完正着回放一遍，自动沉底让局面走岔、或者闹钟来不及关的，换下一个种子重来。
        /// 同一个种子永远发出同一盘。
        /// </summary>
        public void Deal(int scrambleMoves, int seed, int parcels = 0, int alarms = 0)
        {
            Verified = false;
            for (int attempt = 0; attempt < 80 && !Verified; attempt++)
            {
                var random = new Random(unchecked(seed + attempt * 7919));
                Generate(scrambleMoves, random, parcels);
                Verified = PlaceKeysAndAlarms(random, alarms);
            }
            if (!Verified)
            {
                for (int c = 0; c < ColumnCount; c++)
                    unlocked[c] = true;
            }

            Steps = 0;
            history.Clear();
            settleHistory.Clear();
            ResetLast();
        }

        void Generate(int scrambleMoves, Random random, int parcels)
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                columns[c].Clear();
                for (int r = 0; r < ColumnHeight; r++)
                    columns[c].Add(c);
                opened[c] = false;
                unlocked[c] = !lockAtStart[c];
            }
            Array.Clear(alarmDeadline, 0, alarmDeadline.Length);
            Held = MysteryIndex;
            recipe.Clear();

            var free = new List<int>();
            bool anyLock = false;
            int maxNeed = 0;
            for (int c = 0; c < ColumnCount; c++)
            {
                if (coverNeed[c] > 0)
                    maxNeed = Math.Max(maxNeed, coverNeed[c]);
                else if (lockAtStart[c])
                    anyLock = true;
                else
                    free.Add(c);
            }
            maxNeed = Math.Min(maxNeed, free.Count);

            if (maxNeed == 0 && !anyLock)
            {
                Scramble(AllColumns(), scrambleMoves, random);
            }
            else
            {
                Shuffle(free, random);
                var keep = new HashSet<int>(free.GetRange(0, maxNeed));
                var late = new List<int>();
                for (int c = 0; c < ColumnCount; c++)
                {
                    if (!keep.Contains(c)) late.Add(c);
                }
                free.Sort();
                int lateMoves = scrambleMoves * 2 / 5;
                Scramble(late, lateMoves, random);
                Scramble(free, scrambleMoves - lateMoves, random);
            }
            recipe.Reverse();

            WrapRandom(parcels, random);
            Unwrap();
        }

        /// <summary>
        /// 按倒推走法正着回放，记下每格第几步被顶出来，
        /// 钥匙挂在锁住的列第一次被点之前就会顶出来的格子上，闹钟的倒数留出余量。
        /// 再按真实规则回放一遍确认能解。
        /// </summary>
        bool PlaceKeysAndAlarms(Random random, int alarms)
        {
            int locks = 0;
            for (int c = 0; c < ColumnCount; c++)
            {
                if (lockAtStart[c]) locks++;
            }
            alarms = Math.Min(alarms, MaxAlarms);

            for (int c = 0; c < ColumnCount; c++)
            {
                List<int> list = columns[c];
                for (int r = 0; r < list.Count; r++)
                    list[r] |= (c * 16 + r + 1) << TagShift;
            }

            var ejectStep = new Dictionary<int, int>();
            int firstLockedPlay = int.MaxValue;
            var trace = new SortBoard(this, true);
            foreach (int column in recipe)
            {
                if (trace.IsWin()) break;
                if (!trace.CanPlay(column))
                    return Fail();
                if (lockAtStart[column] && firstLockedPlay == int.MaxValue)
                    firstLockedPlay = trace.Steps + 1;
                trace.Play(column);
                int tag = (trace.lastEjectedRaw & TagMask) >> TagShift;
                if (tag > 0 && !ejectStep.ContainsKey(tag))
                    ejectStep[tag] = trace.Steps;
            }
            if (!trace.IsWin() || trace.Steps > MoveLimit)
                return Fail();

            if (locks > 0 || alarms > 0)
            {
                var keyCells = new List<int>();
                var alarmCells = new List<int>();
                for (int c = 0; c < ColumnCount; c++)
                {
                    if (coverNeed[c] > 0) continue;
                    List<int> list = columns[c];
                    for (int r = 0; r < list.Count; r++)
                    {
                        if ((list[r] & WrapBit) != 0) continue;
                        int tag = (list[r] & TagMask) >> TagShift;
                        if (!ejectStep.TryGetValue(tag, out int step)) continue;
                        if (step < firstLockedPlay && !lockAtStart[c]) keyCells.Add(c * 1000 + r);
                        if (step >= 4) alarmCells.Add(c * 1000 + r);
                    }
                }

                Shuffle(keyCells, random);
                if (keyCells.Count < locks)
                    return Fail();
                var used = new HashSet<int>();
                for (int i = 0; i < locks; i++)
                {
                    int cell = keyCells[i];
                    columns[cell / 1000][cell % 1000] |= KeyBit;
                    used.Add(cell);
                }

                Shuffle(alarmCells, random);
                int placed = 0;
                for (int i = 0; i < alarmCells.Count && placed < alarms; i++)
                {
                    int cell = alarmCells[i];
                    if (used.Contains(cell)) continue;
                    List<int> list = columns[cell / 1000];
                    int r = cell % 1000;
                    int step = ejectStep[(list[r] & TagMask) >> TagShift];
                    placed++;
                    alarmDeadline[placed] = Math.Min(MoveLimit, step + Math.Max(6, step * 3 / 5) + random.Next(0, 4));
                    list[r] |= placed << AlarmShift;
                }
                if (placed < alarms)
                    return Fail();
            }

            StripTags();

            var check = new SortBoard(this, false);
            foreach (int column in recipe)
            {
                if (check.IsWin()) break;
                if (!check.CanPlay(column))
                    return Fail();
                check.Play(column);
                if (check.AlarmRang)
                    return Fail();
            }
            return check.IsWin() || Fail();
        }

        bool Fail()
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                List<int> list = columns[c];
                for (int r = 0; r < list.Count; r++)
                    list[r] &= TypeMask | WrapBit;
            }
            Array.Clear(alarmDeadline, 0, alarmDeadline.Length);
            return false;
        }

        void StripTags()
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                List<int> list = columns[c];
                for (int r = 0; r < list.Count; r++)
                    list[r] &= ~TagMask;
            }
        }

        List<int> AllColumns()
        {
            var all = new List<int>(ColumnCount);
            for (int c = 0; c < ColumnCount; c++)
                all.Add(c);
            return all;
        }

        void Scramble(List<int> pool, int moves, Random random)
        {
            if (pool.Count == 0) return;
            int previous = -1;
            for (int k = 0; k < moves; k++)
            {
                int c = pool[random.Next(pool.Count)];
                if (c == previous && pool.Count > 1)
                {
                    int at = (pool.IndexOf(c) + 1 + random.Next(pool.Count - 1)) % pool.Count;
                    c = pool[at];
                }
                previous = c;
                ReverseMove(c);
                recipe.Add(c);
            }
        }

        static void Shuffle(List<int> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        /// <summary>包裹只盖没罩的列，列底那格不盖，盖了也会马上拆开。</summary>
        void WrapRandom(int parcels, Random random)
        {
            if (parcels <= 0) return;
            var cells = new List<int>();
            for (int c = 0; c < ColumnCount; c++)
            {
                if (coverNeed[c] > 0) continue;
                for (int r = 0; r < columns[c].Count - 1; r++)
                    cells.Add(c * 1000 + r);
            }
            Shuffle(cells, random);
            int count = Math.Min(parcels, cells.Count);
            for (int i = 0; i < count; i++)
                columns[cells[i] / 1000][cells[i] % 1000] |= WrapBit;
        }

        // ------------------------------------------------------------ 走子

        public bool CanPlay(int column) =>
            column >= 0 && column < ColumnCount
            && Held != Empty
            && !OutOfMoves
            && !IsCovered(column)
            && !IsLocked(column)
            && !IsColumnSolved(column)
            && !AlarmRang;

        /// <summary>
        /// 一列只剩一件异款、手里又是这列的款时，把那件异款挪到列底，
        /// 这样下一步交换会直接把它顶出来。已经在列底则不必挪。
        /// 还有包裹、罩着或锁着的列不挪，免得提前露底；专属列只为它认的款挪。
        /// </summary>
        public bool TryAutoRotate(int column, out int oddIndex)
        {
            oddIndex = -1;
            if (Held == Empty || Held == MysteryIndex) return false;
            if (column < 0 || column >= ColumnCount) return false;
            if (IsCovered(column) || IsLocked(column)) return false;
            if (target[column] != Empty && target[column] != Held) return false;
            List<int> list = columns[column];
            if (list.Count < 2) return false;

            int odd = -1;
            int matches = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if ((list[i] & WrapBit) != 0) return false;
                if ((list[i] & TypeMask) == Held)
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
            int raw = list[last];
            list.RemoveAt(last);
            lastEjectedRaw = raw;

            Held = raw & TypeMask;
            Steps++;
            LastAlarmOff = (raw & AlarmMask) != 0;
            LastUnlocked = (raw & KeyBit) != 0 ? UnlockNext() : Array.Empty<int>();
            history.Push(new Move(column, -1));
            settleHistory.Push(SettleReady());
            AfterMove();
            return Held;
        }

        int[] UnlockNext()
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                if (unlocked[c]) continue;
                unlocked[c] = true;
                return new[] { c };
            }
            return Array.Empty<int>();
        }

        public bool CanSwap(int column, int row)
        {
            if (column < 0 || column >= ColumnCount) return false;
            if (IsCovered(column) || IsLocked(column)) return false;
            if (row < 0 || row >= columns[column].Count || Held == Empty) return false;
            int cell = columns[column][row];
            if ((cell & (WrapBit | KeyBit | AlarmMask)) != 0) return false;
            return (cell & TypeMask) != Held;
        }

        /// <summary>「交换」道具：手里那件和架上任意一格直接对调，不算步数。</summary>
        public int Swap(int column, int row)
        {
            List<int> list = columns[column];
            int taken = list[row] & TypeMask;
            list[row] = Held;
            Held = taken;
            LastAlarmOff = false;
            LastUnlocked = Array.Empty<int>();
            history.Push(new Move(column, row));
            settleHistory.Push(SettleReady());
            AfterMove();
            return taken;
        }

        /// <summary>最近一次撤回的是不是「交换」，面板据此退还道具次数。</summary>
        public bool LastUndoWasSwap { get; private set; }

        /// <summary>拆开的包裹、拉开的防尘罩、打开的锁、拆掉的闹钟，撤回时都不复原。</summary>
        public int Undo()
        {
            if (history.Count == 0) return Empty;
            Move move = history.Pop();
            Settle[] settles = settleHistory.Count > 0 ? settleHistory.Pop() : NoSettles;
            for (int i = settles.Length - 1; i >= 0; i--)
                MoveBottomTo(columns[settles[i].Column], settles[i].FromIndex);
            ResetLast();
            LastUndoWasSwap = move.Row >= 0;
            if (LastUndoWasSwap)
            {
                List<int> list = columns[move.Column];
                int back = list[move.Row] & TypeMask;
                list[move.Row] = Held;
                Held = back;
                Unwrap();
                return back;
            }
            int outgoing = ReverseMove(move.Column);
            if (Steps > 0) Steps--;
            Unwrap();
            return outgoing;
        }

        void AfterMove()
        {
            Unwrap();
            int solved = SolvedCount;
            List<int> now = null;
            for (int c = 0; c < ColumnCount; c++)
            {
                if (!IsCovered(c) || solved < coverNeed[c]) continue;
                opened[c] = true;
                (now ??= new List<int>()).Add(c);
            }
            LastOpened = now == null ? Array.Empty<int>() : now.ToArray();
            if (now != null)
            {
                for (int i = 0; i < now.Count; i++)
                    Unwrap(now[i]);
            }
        }

        /// <summary>每列最下面那格拆开；整列同款的也全部拆开。</summary>
        void Unwrap()
        {
            for (int c = 0; c < ColumnCount; c++)
                Unwrap(c);
        }

        void Unwrap(int column)
        {
            List<int> list = columns[column];
            if (list.Count == 0) return;
            int last = list.Count - 1;
            list[last] &= ~WrapBit;
            if (!IsColumnSolved(column)) return;
            for (int i = 0; i < list.Count; i++)
                list[i] &= ~WrapBit;
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

            int outgoing = list[0] & TypeMask;
            list.RemoveAt(0);

            Held = outgoing;
            return outgoing;
        }

        // ------------------------------------------------------------ 判定

        public bool IsColumnSolved(int column)
        {
            List<int> list = columns[column];
            if (list.Count != ColumnHeight) return false;
            int first = list[0] & TypeMask;
            if (target[column] != Empty && first != target[column]) return false;
            for (int i = 1; i < list.Count; i++)
            {
                if ((list[i] & TypeMask) != first) return false;
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
