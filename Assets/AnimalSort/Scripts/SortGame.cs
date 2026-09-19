using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SortGame : MonoBehaviour
{
    public const int ColumnCount = 5;
    public const int MaxHeight = 5;
    public const float CellSize = 1.3f;
    public const float GridTopY = 4.2f;
    public const float HoldY = -3.2f;
    public const float OrthoSize = 7.8f;
    public const float CameraCenterY = 0.2f;
    const float RefPixelsPerUnit = 1920f / (2f * OrthoSize);

    [SerializeField] RuntimeAnimatorController idleController;
    [SerializeField] int moveLimit = 60;
    [SerializeField] int scrambleMoves = 26;

    readonly AnimalColumn[] columns = new AnimalColumn[ColumnCount];
    readonly Stack<Move> history = new Stack<Move>();

    Transform boardRoot;
    AnimalPiece held;
    AnimalPiece parked;
    Text stepLabel;
    Text toastLabel;
    Text parkLabel;

    int steps;
    int shufflesLeft;
    bool busy;
    bool finished;
    int lastClickFrame = -1;

    enum MoveKind { Shift, Pull, Park }

    struct Move
    {
        public MoveKind kind;
        public AnimalColumn column;
    }

    static float GridBottomY => GridTopY - (MaxHeight - 1) * CellSize;
    static Vector3 HoldPosition => new Vector3(0f, HoldY, 0f);
    static Vector3 ParkPosition => new Vector3(2.7f, HoldY, 0f);

    void Start()
    {
        EnsureCamera();
        EnsureEventSystem();
        BuildHud();
        RebuildLevel();
    }

    public void RebuildLevel()
    {
        StopAllCoroutines();
        busy = false;
        finished = false;
        steps = 0;
        shufflesLeft = 3;
        history.Clear();
        held = null;
        parked = null;

        BuildBoard();

        Layout layout;
        do
        {
            layout = Generate(scrambleMoves, Random.Range(1, 999999));
        }
        while (layout.hand == AnimalType.Rainbow);

        Deal(layout);
        UpdateHud();
        Toast("点一列，手里的动物插到该列最上，最下面的顶出来");
    }

    // ---------------------------------------------------------------- level

    class Layout
    {
        public List<AnimalType>[] columns;
        public AnimalType hand;
    }

    // Start from the solved board and play the move in reverse, so every
    // generated level is guaranteed solvable.
    static Layout Generate(int scramble, int seed)
    {
        var layout = new Layout { columns = new List<AnimalType>[ColumnCount], hand = AnimalType.Rainbow };
        for (int i = 0; i < ColumnCount; i++)
        {
            layout.columns[i] = new List<AnimalType>();
            for (int j = 0; j < MaxHeight; j++)
                layout.columns[i].Add((AnimalType)i);
        }

        var rng = new System.Random(seed);
        int previous = -1;
        for (int k = 0; k < scramble; k++)
        {
            int c = rng.Next(ColumnCount);
            if (c == previous)
                c = (c + 1 + rng.Next(ColumnCount - 1)) % ColumnCount;
            previous = c;

            List<AnimalType> list = layout.columns[c];
            list.Add(layout.hand);
            layout.hand = list[0];
            list.RemoveAt(0);
        }

        return layout;
    }

    void Deal(Layout layout)
    {
        for (int i = 0; i < ColumnCount; i++)
        {
            for (int j = 0; j < layout.columns[i].Count; j++)
                columns[i].Push(Spawn(layout.columns[i][j]));
        }

        held = Spawn(layout.hand);
        held.transform.SetParent(boardRoot, true);
        held.transform.position = HoldPosition;
        held.SetSorting(60);
        held.SetHeld(true);
    }

    // --------------------------------------------------------------- input

    void Update()
    {
        if (busy || finished) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (IsOverUi()) return;

        AnimalColumn column = ColumnUnderPointer();
        if (column != null)
            ClickColumn(column);
    }

    public void ClickColumn(AnimalColumn column)
    {
        if (busy || finished || column == null) return;
        if (Time.frameCount == lastClickFrame) return;
        lastClickFrame = Time.frameCount;

        if (held == null)
            StartCoroutine(PullBottom(column));
        else
            StartCoroutine(Shift(column));
    }

    AnimalColumn ColumnUnderPointer()
    {
        Camera cam = Camera.main;
        if (cam == null) return null;

        Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
        Collider2D hit = Physics2D.OverlapPoint(new Vector2(world.x, world.y));
        return hit != null ? hit.GetComponentInParent<AnimalColumn>() : null;
    }

    static bool IsOverUi()
    {
        if (EventSystem.current == null) return false;

        var data = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);
        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject != null && results[i].gameObject.GetComponent<Selectable>() != null)
                return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- moves

    IEnumerator Shift(AnimalColumn column)
    {
        busy = true;

        AnimalPiece incoming = held;
        held = null;
        incoming.SetHeld(false);

        AnimalPiece outgoing = column.ShiftInFromTop(incoming);

        var targets = new List<KeyValuePair<Transform, Vector3>>();
        for (int i = 0; i < column.Count; i++)
        {
            AnimalPiece piece = column.Pieces[i];
            piece.SetSorting(10 + i);
            targets.Add(new KeyValuePair<Transform, Vector3>(piece.transform, column.SlotWorld(i)));
        }
        if (outgoing != null)
            targets.Add(new KeyValuePair<Transform, Vector3>(outgoing.transform, HoldPosition));

        yield return Glide(targets, 0.16f);

        column.SnapAll();
        if (outgoing != null)
        {
            held = outgoing;
            held.transform.SetParent(boardRoot, true);
            held.transform.position = HoldPosition;
            held.SetSorting(60);
            held.SetHeld(true);
        }

        steps++;
        history.Push(new Move { kind = MoveKind.Shift, column = column });
        busy = false;
        Settle();
    }

    IEnumerator PullBottom(AnimalColumn column)
    {
        if (column.IsEmpty)
        {
            Toast("这一列是空的");
            yield break;
        }

        busy = true;
        AnimalPiece outgoing = column.PopBottom();

        var targets = new List<KeyValuePair<Transform, Vector3>>
        {
            new KeyValuePair<Transform, Vector3>(outgoing.transform, HoldPosition)
        };
        yield return Glide(targets, 0.14f);

        held = outgoing;
        held.transform.SetParent(boardRoot, true);
        held.transform.position = HoldPosition;
        held.SetSorting(60);
        held.SetHeld(true);

        history.Push(new Move { kind = MoveKind.Pull, column = column });
        busy = false;
        UpdateHud();
    }

    IEnumerator Undo()
    {
        if (busy || history.Count == 0)
        {
            Toast("没有可撤回的步骤");
            yield break;
        }

        busy = true;
        Move move = history.Pop();

        if (move.kind == MoveKind.Shift)
        {
            AnimalPiece incoming = held;
            held = null;
            incoming.SetHeld(false);

            AnimalPiece outgoing = move.column.ShiftInFromBottom(incoming);

            var targets = new List<KeyValuePair<Transform, Vector3>>();
            for (int i = 0; i < move.column.Count; i++)
                targets.Add(new KeyValuePair<Transform, Vector3>(move.column.Pieces[i].transform, move.column.SlotWorld(i)));
            if (outgoing != null)
                targets.Add(new KeyValuePair<Transform, Vector3>(outgoing.transform, HoldPosition));

            yield return Glide(targets, 0.14f);

            move.column.SnapAll();
            if (outgoing != null)
            {
                held = outgoing;
                held.transform.SetParent(boardRoot, true);
                held.transform.position = HoldPosition;
                held.SetSorting(60);
                held.SetHeld(true);
            }

            steps = Mathf.Max(0, steps - 1);
        }
        else if (move.kind == MoveKind.Pull)
        {
            AnimalPiece piece = held;
            held = null;
            if (piece != null)
            {
                piece.SetHeld(false);
                var targets = new List<KeyValuePair<Transform, Vector3>>
                {
                    new KeyValuePair<Transform, Vector3>(piece.transform, move.column.SlotWorld(move.column.Count))
                };
                yield return Glide(targets, 0.14f);
                move.column.Push(piece);
            }
        }
        else if (move.kind == MoveKind.Park)
        {
            yield return SwapPark(false);
        }

        finished = false;
        busy = false;
        UpdateHud();
    }

    IEnumerator SwapPark(bool record)
    {
        AnimalPiece fromHand = held;
        AnimalPiece fromPark = parked;

        held = fromPark;
        parked = fromHand;

        var targets = new List<KeyValuePair<Transform, Vector3>>();
        if (parked != null)
        {
            parked.SetHeld(false);
            targets.Add(new KeyValuePair<Transform, Vector3>(parked.transform, ParkPosition));
        }
        if (held != null)
        {
            held.SetHeld(true);
            targets.Add(new KeyValuePair<Transform, Vector3>(held.transform, HoldPosition));
        }

        yield return Glide(targets, 0.14f);

        if (parked != null)
        {
            parked.transform.position = ParkPosition;
            parked.SetSorting(58);
        }
        if (held != null)
        {
            held.transform.position = HoldPosition;
            held.SetSorting(60);
        }

        if (record)
            history.Push(new Move { kind = MoveKind.Park, column = null });

        UpdateHud();
    }

    IEnumerator Glide(List<KeyValuePair<Transform, Vector3>> targets, float duration)
    {
        var starts = new Vector3[targets.Count];
        for (int i = 0; i < targets.Count; i++)
            starts[i] = targets[i].Key.position;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            for (int i = 0; i < targets.Count; i++)
                targets[i].Key.position = Vector3.LerpUnclamped(starts[i], targets[i].Value, k);
            yield return null;
        }

        for (int i = 0; i < targets.Count; i++)
            targets[i].Key.position = targets[i].Value;
    }

    void Settle()
    {
        UpdateHud();

        if (IsWin())
        {
            finished = true;
            Toast("通关啦！手里只剩五彩动物");
            return;
        }

        if (steps >= moveLimit)
        {
            finished = true;
            Toast("步数用完了，点「重开」再来一次");
        }
    }

    bool IsWin()
    {
        if (held == null || held.Type != AnimalType.Rainbow) return false;
        if (parked != null) return false;

        var used = new HashSet<AnimalType>();
        for (int i = 0; i < ColumnCount; i++)
        {
            if (!columns[i].IsSolved) return false;
            if (!used.Add(columns[i].Top.Type)) return false;
        }
        return used.Count == ColumnCount;
    }

    // ---------------------------------------------------------------- board

    void BuildBoard()
    {
        if (boardRoot != null)
            Destroy(boardRoot.gameObject);

        boardRoot = new GameObject("Board").transform;
        boardRoot.SetParent(transform, false);

        float gridWidth = ColumnCount * CellSize;
        float gridHeight = MaxHeight * CellSize;
        float panelCenterY = (GridTopY + GridBottomY) * 0.5f;

        Quad(boardRoot, "Panel", new Vector3(0f, panelCenterY, 0f),
            new Vector2(gridWidth + 0.5f, gridHeight + 0.5f), new Color(0.99f, 0.82f, 0.74f), -20);

        Quad(boardRoot, "HoldPad", new Vector3(0f, HoldY, 0f),
            new Vector2(gridWidth + 0.5f, CellSize + 0.9f), new Color(0.96f, 0.93f, 0.9f), -20);

        for (int i = 0; i < ColumnCount; i++)
        {
            float x = (i - (ColumnCount - 1) * 0.5f) * CellSize;
            var go = new GameObject("Column_" + i);
            go.transform.SetParent(boardRoot, false);

            var column = go.AddComponent<AnimalColumn>();
            column.Setup(this, i, MaxHeight, new Vector3(x, GridTopY, 0f), CellSize);

            Quad(go.transform, "Lane", new Vector3(x, panelCenterY, 0f),
                new Vector2(CellSize - 0.1f, gridHeight), new Color(1f, 0.92f, 0.88f), -10);

            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(CellSize, gridHeight);
            box.offset = new Vector2(0f, -(MaxHeight - 1) * CellSize * 0.5f);

            columns[i] = column;
        }
    }

    AnimalPiece Spawn(AnimalType type)
    {
        var go = new GameObject("Animal");
        var piece = go.AddComponent<AnimalPiece>();
        piece.Setup(type, idleController);
        go.transform.SetParent(boardRoot, false);
        return piece;
    }

    static void Quad(Transform parent, string name, Vector3 worldPosition, Vector2 size, Color color, int sorting)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldPosition;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = WhiteSprite();
        renderer.color = color;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
        renderer.sortingOrder = sorting;
    }

    static Sprite white;

    static Sprite WhiteSprite()
    {
        if (white != null) return white;

        var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        var pixels = new Color[64];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();

        white = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f, 0,
            SpriteMeshType.FullRect, new Vector4(2, 2, 2, 2));
        return white;
    }

    // ------------------------------------------------------------------ hud

    void BuildHud()
    {
        var existing = transform.Find("Hud");
        if (existing != null)
            Destroy(existing.gameObject);

        var canvasGo = new GameObject("Hud", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 5f;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 1f;

        Transform root = canvasGo.transform;
        Label(root, "Title", "动物排队", RefY(6.7f), 74, new Color(0.24f, 0.2f, 0.26f));
        stepLabel = Label(root, "Steps", "", RefY(5.6f), 44, new Color(0.36f, 0.32f, 0.38f));
        parkLabel = Label(root, "Hint", "", RefY(-4.4f), 40, new Color(0.45f, 0.4f, 0.45f));
        toastLabel = Label(root, "Toast", "", RefY(-6.65f), 38, new Color(0.3f, 0.36f, 0.4f));

        Button(root, "移出", -3.0f, () => { if (!busy && !finished) StartCoroutine(SwapPark(true)); });
        Button(root, "撤回", -1.0f, () => StartCoroutine(Undo()));
        Button(root, "随机", 1.0f, OnShuffle);
        Button(root, "重开", 3.0f, RebuildLevel);
    }

    static float RefY(float worldY)
    {
        return (worldY - CameraCenterY) * RefPixelsPerUnit;
    }

    static Text Label(Transform parent, string name, string content, float y, int size, Color color)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);

        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;

        var rect = text.rectTransform;
        rect.sizeDelta = new Vector2(1000f, 90f);
        rect.anchoredPosition = new Vector2(0f, y);
        return text;
    }

    void Button(Transform parent, string caption, float worldX, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject("Btn_" + caption, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        image.color = new Color(1f, 0.74f, 0.55f);

        var rect = image.rectTransform;
        rect.sizeDelta = new Vector2(220f, 110f);
        rect.anchoredPosition = new Vector2(worldX * RefPixelsPerUnit, RefY(-5.6f));

        var label = Label(go.transform, "Text", caption, 0f, 44, new Color(0.22f, 0.18f, 0.2f));
        label.rectTransform.sizeDelta = rect.sizeDelta;

        go.GetComponent<Button>().onClick.AddListener(action);
    }

    void OnShuffle()
    {
        if (busy || finished) return;
        if (shufflesLeft <= 0)
        {
            Toast("随机次数用完了");
            return;
        }

        shufflesLeft--;
        int keepSteps = steps;
        int keepShuffles = shufflesLeft;

        RebuildLevel();
        steps = keepSteps;
        shufflesLeft = keepShuffles;
        UpdateHud();
        Toast("重新洗牌");
    }

    void UpdateHud()
    {
        if (stepLabel != null)
            stepLabel.text = "步数 " + steps + " / " + moveLimit + "    随机 x" + shufflesLeft;
        if (parkLabel != null)
            parkLabel.text = parked != null ? "手里的动物（右侧暂存 1 只）" : "手里的动物";
    }

    void Toast(string message)
    {
        if (toastLabel != null)
            toastLabel.text = message;
    }

    // -------------------------------------------------------------- scaffold

    public static void EnsureCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";
            cam = go.GetComponent<Camera>();
        }

        cam.orthographic = true;
        cam.orthographicSize = OrthoSize;
        cam.transform.position = new Vector3(0f, CameraCenterY, -10f);
        cam.transform.rotation = Quaternion.identity;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.45f, 0.82f, 0.8f);

        if (cam.GetComponent<Physics2DRaycaster>() == null)
            cam.gameObject.AddComponent<Physics2DRaycaster>();
        if (cam.GetComponent<PortraitViewport>() == null)
            cam.gameObject.AddComponent<PortraitViewport>();
    }

    public static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        go.transform.position = Vector3.zero;
    }
}
