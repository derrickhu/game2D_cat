using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 活动装箱页的预制：底图、顶栏、六列、收纳箱和两个按钮。
    /// </summary>
    public class PackHud : MonoBehaviour
    {
        public const float LaneW = 152f;
        public const float LaneH = 760f;
        public const float LaneTop = 520f;
        public const float LanePitch = 164f;

        static readonly Color Cocoa = new Color32(0x4A, 0x26, 0x2C, 0xFF);
        static readonly Color Shade = new Color(0.29f, 0.08f, 0.14f, 0.7f);

        public Image backdrop;
        public RectTransform topBar;
        public Button backButton;
        public Button restartButton;
        public Text progressLabel;
        public Text queueLabel;
        public Text titleLabel;
        public Image[] queueIcons = new Image[8];
        public RectTransform pileRoot;
        public Image heapImage;
        public Sprite heapSprite;
        public Sprite binOpen;
        public Sprite binShut;
        public Image[] binFaces = new Image[PackBoard.MaxSeats];
        public Text boxLabel;
        public RectTransform rewardTrack;
        public Image[] rewardNodes = new Image[PackBoard.BoxesToWin];
        public Image[] rewardChecks = new Image[PackBoard.BoxesToWin];
        public Text[] rewardNumbers = new Text[PackBoard.BoxesToWin];
        public Image rewardPrize;
        public Image[] laneFaces = new Image[PackBoard.ColumnCount];
        public RectTransform[] stacks = new RectTransform[PackBoard.ColumnCount];
        public Image[] lockIcons = new Image[PackBoard.ColumnCount];
        public Text[] lockLabels = new Text[PackBoard.ColumnCount];
        public Image[] adBadges = new Image[PackBoard.ColumnCount];
        public Image[] readyMarks = new Image[PackBoard.ColumnCount];
        public Image box;
        public Image[] boxSlots = new Image[4];
        int shownSeats = PackBoard.BoxPlan[0];
        bool packHot;
        bool binCelebrating;
        float[] binPunch;
        public Button packButton;
        public Button refillButton;
        public Text toastLabel;
        public Sprite laneSprite;
        public Sprite laneOnSprite;
        public Sprite laneReadySprite;

        public static PackHud Assemble(RectTransform parent, GameDatabase db)
        {
            var go = new GameObject("PackScreen", typeof(RectTransform), typeof(PackHud));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var hud = go.GetComponent<PackHud>();
            hud.BuildChildren(db);
            return hud;
        }

        void BuildChildren(GameDatabase db)
        {
            laneSprite = db != null ? db.packLane : null;
            laneOnSprite = db != null ? db.packLaneOn : null;
            laneReadySprite = db != null ? db.packLaneReady : null;

            backdrop = UiKit.Backdrop(transform, db != null ? db.packCarpet : null);
            backdrop.preserveAspect = false;
            backdrop.raycastTarget = false;

            BuildLanes(db);
            BuildBox(db);
            BuildButtons(db);
            BuildTop(db);

            toastLabel = UiKit.Label(transform, "Toast", "", new Vector2(0.5f, 0f),
                new Vector2(0f, 1480f), new Vector2(900f, 56f), 32, Cocoa);
            var glow = toastLabel.gameObject.AddComponent<Outline>();
            glow.effectColor = new Color(1f, 0.98f, 0.93f, 0.92f);
            glow.effectDistance = new Vector2(2f, -2f);
        }

        void BuildTop(GameDatabase db)
        {
            topBar = UiKit.Stretch(transform, "TopBar");

            Image back = UiKit.Icon(topBar, "Back", db != null ? db.dressupBack : null,
                new Vector2(0f, 1f), new Vector2(36f + 48f, -20f - 52f), new Vector2(96f, 104f));
            back.preserveAspect = true;
            backButton = MakeHit(back);

            progressLabel = UiKit.Label(topBar, "Progress", "0/3 箱", new Vector2(1f, 1f),
                new Vector2(-36f - 90f, -20f - 48f), new Vector2(200f, 72f), 40, Cocoa);
            var rim = progressLabel.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(1f, 0.98f, 0.93f, 1f);
            rim.effectDistance = new Vector2(2f, -2f);

            restartButton = UiKit.Button(topBar, "重开", new Vector2(1f, 1f),
                new Vector2(-360f, -20f - 48f), new Vector2(120f, 64f), Chip.White, 28, () => { });

            queueLabel = UiKit.Label(topBar, "Queue", "待整理 0", new Vector2(0f, 1f),
                new Vector2(150f, -168f), new Vector2(200f, 48f), 28, Cocoa, TextAnchor.MiddleLeft);
            for (int i = 0; i < queueIcons.Length; i++)
            {
                queueIcons[i] = UiKit.Icon(topBar, "Queue_" + i, null, new Vector2(0f, 1f),
                    new Vector2(280f + i * 58f, -168f), new Vector2(52f, 52f));
                queueIcons[i].enabled = false;
            }

            titleLabel = UiKit.Label(topBar, "Title", "整理装箱", new Vector2(0.5f, 1f),
                new Vector2(0f, -36f), new Vector2(280f, 72f), 44, Cocoa);
            Text title = titleLabel;
            var titleRim = title.gameObject.AddComponent<Outline>();
            titleRim.effectColor = new Color(1f, 0.98f, 0.93f, 1f);
            titleRim.effectDistance = new Vector2(2f, -2f);
        }

        void BuildLanes(GameDatabase db)
        {
            for (int i = 0; i < PackBoard.ColumnCount; i++)
            {
                float x = (i - (PackBoard.ColumnCount - 1) * 0.5f) * LanePitch;
                Image face = UiKit.Icon(transform, "Lane_" + i, laneSprite, new Vector2(0.5f, 1f),
                    new Vector2(x, -LaneTop - LaneH * 0.5f), new Vector2(LaneW, LaneH));
                face.preserveAspect = false;
                face.type = Image.Type.Simple;
                face.raycastTarget = true;
                var button = face.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                button.transition = Selectable.Transition.None;
                laneFaces[i] = face;

                RectTransform stack = UiKit.Stretch(face.transform, "Stack");
                stacks[i] = stack;

                lockIcons[i] = UiKit.Icon(face.transform, "Lock", db != null ? db.packLock : null,
                    new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(72f, 84f));
                lockLabels[i] = UiKit.Label(face.transform, "LockText", "临时解锁", new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -50f), new Vector2(LaneW - 8f, 64f), 24, Cocoa);
                adBadges[i] = UiKit.Icon(face.transform, "Ad", db != null ? db.packAd : null,
                    new Vector2(0.5f, 0.5f), new Vector2(36f, 64f), new Vector2(40f, 40f));
                readyMarks[i] = UiKit.Icon(face.transform, "Ready", db != null ? db.packCheck : null,
                    new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(56f, 56f));
                readyMarks[i].enabled = false;
                lockIcons[i].gameObject.SetActive(false);
            }
        }

        /// <summary>六条铺地布收进地毯中间的空处。预制里的旧位置是按立板排的。</summary>
        public void FitOnCarpet()
        {
            for (int i = 0; i < laneFaces.Length; i++)
            {
                Image face = laneFaces[i];
                if (face == null) continue;
                float x = (i - (PackBoard.ColumnCount - 1) * 0.5f) * LanePitch;
                RectTransform rt = face.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(LaneW, LaneH);
                rt.anchoredPosition = new Vector2(x, -LaneTop - LaneH * 0.5f);
                if (lockLabels[i] != null)
                {
                    lockLabels[i].rectTransform.sizeDelta = new Vector2(LaneW + 8f, 64f);
                    lockLabels[i].fontSize = 20;
                }
            }
        }

        void BuildBox(GameDatabase db)
        {
            var boxSize = new Vector2(535f, 420f);
            box = UiKit.Icon(transform, "Box", db != null ? db.packBox : null, new Vector2(0.5f, 0f),
                new Vector2(0f, 406f), boxSize);
            box.preserveAspect = true;
            box.raycastTarget = false;
            // 四个窗格在箱子贴图上的相对位置（中心为 0）。
            float[] slotX = { -0.289f, -0.095f, 0.096f, 0.289f };
            const float slotY = -0.091f;
            for (int i = 0; i < boxSlots.Length; i++)
            {
                boxSlots[i] = UiKit.Icon(box.transform, "Slot_" + i, null, new Vector2(0.5f, 0.5f),
                    new Vector2(slotX[i] * boxSize.x, slotY * boxSize.y), new Vector2(78f, 78f));
                boxSlots[i].enabled = false;
            }
        }

        void BuildButtons(GameDatabase db)
        {
            packButton = ActionButton("装箱", db != null ? db.packBtn : null, -200f, Color.white);
            refillButton = ActionButton("补充衣服", db != null ? db.packRefill : null, 200f, Cocoa);
        }

        Button ActionButton(string caption, Sprite sprite, float x, Color ink)
        {
            Image face = UiKit.Icon(transform, "Btn_" + caption, sprite, new Vector2(0.5f, 0f),
                new Vector2(x, 100f), new Vector2(320f, 156f));
            face.preserveAspect = true;
            if (face.sprite == null)
                face.sprite = UiKit.Rounded;
            Text label = UiKit.Label(face.transform, "Text", caption, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 4f), new Vector2(320f, 72f), 40, ink);
            label.fontStyle = FontStyle.Normal;
            if (ink == Color.white)
            {
                var shadow = label.gameObject.AddComponent<Shadow>();
                shadow.effectColor = Shade;
                shadow.effectDistance = new Vector2(0f, -3f);
            }
            return MakeHit(face);
        }

        /// <summary>标题上移，衣服堆放在列上方，通关奖励放在底部。</summary>
        public void FitChrome()
        {
            if (topBar == null)
            {
                Transform found = transform.Find("TopBar");
                if (found != null)
                    topBar = (RectTransform)found;
            }
            if (topBar == null) return;

            if (titleLabel == null)
            {
                Transform title = topBar.Find("Title");
                if (title != null)
                    titleLabel = title.GetComponent<Text>();
            }
            float top = ScreenFit.TopPad;
            PinTop(titleLabel != null ? titleLabel.rectTransform : null, new Vector2(0.5f, 1f), new Vector2(0f, -(top + 28f)));
            PinTop(backButton != null ? (RectTransform)backButton.transform : null, new Vector2(0f, 1f), new Vector2(84f, -(top + 40f)));
            PinTop(restartButton != null ? (RectTransform)restartButton.transform : null, new Vector2(1f, 1f), new Vector2(-120f, -(top + 28f)));
            if (progressLabel != null)
                progressLabel.gameObject.SetActive(false);

            for (int i = 0; i < queueIcons.Length; i++)
            {
                if (queueIcons[i] != null)
                    queueIcons[i].gameObject.SetActive(false);
            }

            EnsurePile();
            LayoutBins(shownSeats);
            EnsureReward();
            if (box != null && binOpen != null)
                box.gameObject.SetActive(false);
            if (queueLabel != null)
                queueLabel.transform.SetAsLastSibling();
            if (titleLabel != null)
                titleLabel.transform.SetAsLastSibling();
            if (backButton != null)
                backButton.transform.SetAsLastSibling();
            if (restartButton != null)
                restartButton.transform.SetAsLastSibling();
            if (toastLabel != null)
            {
                RectTransform toast = toastLabel.rectTransform;
                toast.SetParent(transform, false);
                toast.anchorMin = new Vector2(0.5f, 1f);
                toast.anchorMax = new Vector2(0.5f, 1f);
                toast.anchoredPosition = new Vector2(0f, -492f);
                toast.SetAsLastSibling();
            }
        }

        public Vector3 PileOrigin()
        {
            if (pileRoot == null) return transform.position;
            return pileRoot.TransformPoint(new Vector3(0f, 24f, 0f));
        }

        public void SetPile(int count)
        {
            if (queueLabel != null)
                queueLabel.text = count > 0 ? "待整理 " + count : "衣服已经发完了";
            if (heapImage != null)
            {
                heapImage.sprite = heapSprite;
                heapImage.gameObject.SetActive(heapSprite != null && count > 0);
            }
        }

        public Vector3 BinAnchor(int index)
        {
            if (index >= 0 && index < binFaces.Length && binFaces[index] != null)
                return binFaces[index].transform.position;
            if (index >= 0 && index < boxSlots.Length && boxSlots[index] != null)
                return boxSlots[index].transform.position;
            return box != null ? box.transform.position : transform.position;
        }

        public void SetProgress(int done, int goal, Sprite prize)
        {
            if (rewardPrize != null && prize != null)
                rewardPrize.sprite = prize;
            if (progressLabel != null)
                progressLabel.text = done + "/" + goal + " 箱";
            for (int i = 0; i < rewardNodes.Length; i++)
            {
                bool filled = i < done;
                bool current = !filled && i == done && done < goal;
                if (rewardNodes[i] != null)
                    rewardNodes[i].color = filled
                        ? new Color(0.93f, 0.45f, 0.52f, 1f)
                        : current
                            ? new Color(1f, 0.78f, 0.52f, 1f)
                            : new Color(0.98f, 0.94f, 0.9f, 1f);
                if (rewardChecks[i] != null)
                    rewardChecks[i].enabled = filled;
                if (rewardNumbers[i] != null)
                    rewardNumbers[i].gameObject.SetActive(!filled);
            }
            if (rewardPrize != null)
                rewardPrize.color = done >= goal && goal > 0
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.55f);
        }

        void EnsurePile()
        {
            if (pileRoot == null)
            {
                var go = new GameObject("Pile", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                pileRoot = (RectTransform)go.transform;
                heapImage = UiKit.Icon(pileRoot, "Heap", heapSprite, new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(860f, 250f));
                heapImage.preserveAspect = true;
                heapImage.raycastTarget = false;
            }
            float heapTop = -(ScreenFit.TopPad + 140f);
            const float heapFloor = -500f;
            if (heapTop - heapFloor > 320f)
                heapTop = heapFloor + 320f;
            float heapH = heapTop - heapFloor;
            if (heapH < 160f)
            {
                heapH = 160f;
                heapTop = heapFloor + heapH;
            }
            pileRoot.anchorMin = new Vector2(0.5f, 1f);
            pileRoot.anchorMax = new Vector2(0.5f, 1f);
            pileRoot.pivot = new Vector2(0.5f, 0.5f);
            pileRoot.sizeDelta = new Vector2(900f, heapH);
            pileRoot.anchoredPosition = new Vector2(0f, heapTop - heapH * 0.5f);
            if (heapImage != null)
            {
                heapImage.rectTransform.sizeDelta = pileRoot.sizeDelta;
                if (heapSprite != null)
                    heapImage.sprite = heapSprite;
            }

            if (queueLabel == null && topBar != null)
            {
                Transform found = topBar.Find("Queue");
                if (found != null)
                    queueLabel = found.GetComponent<Text>();
            }
            if (queueLabel != null)
            {
                queueLabel.rectTransform.SetParent(transform, false);
                queueLabel.rectTransform.anchorMin = new Vector2(0.5f, 1f);
                queueLabel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                queueLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                queueLabel.rectTransform.anchoredPosition = new Vector2(0f, -(ScreenFit.TopPad + 96f));
                queueLabel.rectTransform.sizeDelta = new Vector2(420f, 40f);
                queueLabel.alignment = TextAnchor.MiddleCenter;
                queueLabel.fontSize = 26;
            }
        }

        public Sprite CheckSprite
        {
            get
            {
                if (readyMarks == null) return null;
                for (int i = 0; i < readyMarks.Length; i++)
                {
                    if (readyMarks[i] != null && readyMarks[i].sprite != null)
                        return readyMarks[i].sprite;
                }
                return null;
            }
        }

        public void SetActions(bool canPack, bool canRefill)
        {
            packHot = canPack;
            TintButton(packButton, canPack);
            TintButton(refillButton, canRefill);
            if (!canPack && packButton != null)
                packButton.transform.localScale = Vector3.one;
        }

        public void SetBoxCount(int filled, int seats, int boxIndex, bool won)
        {
            if (boxLabel == null) return;
            boxLabel.text = won ? "三箱都装满了" : "第" + (boxIndex + 1) + "箱  " + filled + "/" + seats;
        }

        public void LayoutBins(int seats)
        {
            if (seats < 1) seats = PackBoard.BoxPlan[0];
            if (seats > PackBoard.MaxSeats) seats = PackBoard.MaxSeats;
            shownSeats = seats;
            EnsureBins();
            if (binOpen == null || binFaces == null) return;

            float pitch = seats >= 8 ? 128f : seats >= 6 ? 160f : 180f;
            float width = seats >= 8 ? 116f : seats >= 6 ? 146f : 156f;
            float height = width * (190f / 156f);
            for (int i = 0; i < binFaces.Length; i++)
            {
                if (binFaces[i] == null) continue;
                var root = binFaces[i].transform.parent as RectTransform;
                if (root == null) continue;
                bool show = i < seats;
                root.gameObject.SetActive(show);
                if (!show) continue;
                float x = (i - (seats - 1) * 0.5f) * pitch;
                root.sizeDelta = new Vector2(width, height);
                root.anchoredPosition = new Vector2(x, 268f);
                if (!binCelebrating)
                    root.localScale = Vector3.one;
                RectTransform open = binFaces[i].rectTransform;
                open.sizeDelta = new Vector2(width, height);
                open.anchoredPosition = new Vector2(0f, height * 0.5f);
                Transform lid = root.Find("Shut");
                var shut = lid as RectTransform;
                if (shut != null)
                {
                    shut.sizeDelta = new Vector2(width, height * (124f / 190f));
                    shut.anchoredPosition = new Vector2(0f, height * (62f / 190f));
                    FitStamp(lid, width);
                }
            }
            EnsureBoxLabel(268f + height + 36f);
        }

        public void NudgeBin(int index)
        {
            if (binCelebrating || binFaces == null || index < 0 || index >= binFaces.Length || binFaces[index] == null)
                return;
            if (binPunch == null || binPunch.Length != binFaces.Length)
                binPunch = new float[binFaces.Length];
            binPunch[index] = 0.14f;
        }

        public IEnumerator PopNode(int index)
        {
            if (rewardNodes == null || index < 0 || index >= rewardNodes.Length || rewardNodes[index] == null)
                yield break;
            RectTransform rect = rewardNodes[index].rectTransform;
            float t = 0f;
            const float duration = 0.28f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                rect.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.35f);
                yield return null;
            }
            rect.localScale = Vector3.one;
        }

        public IEnumerator CelebrateBins()
        {
            binCelebrating = true;
            if (binPunch != null)
            {
                for (int i = 0; i < binPunch.Length; i++)
                    binPunch[i] = 0f;
            }
            var roots = new Transform[PackBoard.MaxSeats];
            int count = 0;
            for (int i = 0; i < shownSeats && binFaces != null && i < binFaces.Length; i++)
            {
                if (binFaces[i] == null) continue;
                roots[count++] = binFaces[i].transform.parent;
            }
            float t = 0f;
            const float duration = 0.28f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                for (int i = 0; i < count; i++)
                {
                    if (roots[i] == null) continue;
                    float local = Mathf.Clamp01(k * 1.15f - i * 0.05f);
                    roots[i].localScale = Vector3.one * (1f + Mathf.Sin(local * Mathf.PI) * 0.1f);
                }
                yield return null;
            }
            for (int i = 0; i < count; i++)
            {
                if (roots[i] != null)
                    roots[i].localScale = Vector3.one;
            }
            binCelebrating = false;
        }

        public IEnumerator IntroBins()
        {
            binCelebrating = true;
            var roots = new Transform[PackBoard.MaxSeats];
            int count = 0;
            for (int i = 0; i < shownSeats && binFaces != null && i < binFaces.Length; i++)
            {
                if (binFaces[i] == null) continue;
                roots[count++] = binFaces[i].transform.parent;
            }
            for (int i = 0; i < count; i++)
            {
                if (roots[i] != null)
                    roots[i].localScale = Vector3.one * 0.45f;
            }
            float t = 0f;
            const float duration = 0.24f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float s = k < 0.7f
                    ? Mathf.Lerp(0.45f, 1.08f, k / 0.7f)
                    : Mathf.Lerp(1.08f, 1f, (k - 0.7f) / 0.3f);
                for (int i = 0; i < count; i++)
                {
                    if (roots[i] != null)
                        roots[i].localScale = Vector3.one * s;
                }
                yield return null;
            }
            for (int i = 0; i < count; i++)
            {
                if (roots[i] != null)
                    roots[i].localScale = Vector3.one;
            }
            binCelebrating = false;
        }

        void LateUpdate()
        {
            if (packButton != null)
            {
                if (!packHot)
                    packButton.transform.localScale = Vector3.one;
                else
                {
                    float s = 1f + Mathf.Sin(Time.unscaledTime * 5.4f) * 0.045f;
                    packButton.transform.localScale = new Vector3(s, s, 1f);
                }
            }
            if (binCelebrating || binPunch == null || binFaces == null) return;
            for (int i = 0; i < binPunch.Length && i < binFaces.Length; i++)
            {
                if (binPunch[i] <= 0f || binFaces[i] == null) continue;
                binPunch[i] = Mathf.Max(0f, binPunch[i] - Time.deltaTime * 0.85f);
                Transform root = binFaces[i].transform.parent;
                if (root != null)
                    root.localScale = Vector3.one * (1f + binPunch[i]);
            }
        }

        static void TintButton(Button button, bool on)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image;
            if (image != null)
                image.color = on ? Color.white : new Color(0.62f, 0.6f, 0.62f, 1f);
        }

        void EnsureBins()
        {
            if (binFaces == null || binFaces.Length != PackBoard.MaxSeats)
            {
                var next = new Image[PackBoard.MaxSeats];
                if (binFaces != null)
                {
                    int n = Mathf.Min(binFaces.Length, next.Length);
                    for (int i = 0; i < n; i++)
                        next[i] = binFaces[i];
                }
                binFaces = next;
            }
            if (binOpen == null) return;
            for (int i = 0; i < binFaces.Length; i++)
            {
                if (binFaces[i] != null) continue;
                var slot = new GameObject("Bin_" + i, typeof(RectTransform));
                slot.transform.SetParent(transform, false);
                var root = (RectTransform)slot.transform;
                root.anchorMin = new Vector2(0.5f, 0f);
                root.anchorMax = new Vector2(0.5f, 0f);
                root.pivot = new Vector2(0.5f, 0f);
                root.sizeDelta = new Vector2(156f, 190f);
                root.anchoredPosition = new Vector2(0f, 268f);

                Image open = UiKit.Icon(root, "Open", binOpen, new Vector2(0.5f, 0f),
                    new Vector2(0f, 95f), new Vector2(156f, 190f));
                open.preserveAspect = true;
                Image shut = UiKit.Icon(root, "Shut", binShut != null ? binShut : binOpen, new Vector2(0.5f, 0f),
                    new Vector2(0f, 62f), new Vector2(156f, 124f));
                shut.preserveAspect = true;
                shut.gameObject.SetActive(false);
                binFaces[i] = open;
            }
        }

        void EnsureBoxLabel(float y)
        {
            if (boxLabel == null)
            {
                boxLabel = UiKit.Label(transform, "BoxCount", "", new Vector2(0.5f, 0f),
                    new Vector2(0f, y), new Vector2(520f, 40f), 28, Cocoa);
                var rim = boxLabel.gameObject.AddComponent<Outline>();
                rim.effectColor = new Color(1f, 0.98f, 0.93f, 0.92f);
                rim.effectDistance = new Vector2(2f, -2f);
            }
            boxLabel.rectTransform.anchoredPosition = new Vector2(0f, y);
        }

        void PinTop(RectTransform rect, Vector2 anchor, Vector2 position)
        {
            if (rect == null) return;
            rect.SetParent(transform, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = position;
        }

        void EnsureReward()
        {
            if (rewardNodes == null || rewardNodes.Length != PackBoard.BoxesToWin)
            {
                rewardNodes = new Image[PackBoard.BoxesToWin];
                rewardChecks = new Image[PackBoard.BoxesToWin];
                rewardNumbers = new Text[PackBoard.BoxesToWin];
            }
            if (rewardTrack != null) return;
            Image bar = UiKit.Icon(transform, "Reward", UiKit.Rounded, new Vector2(0.5f, 0f),
                new Vector2(0f, 196f), new Vector2(680f, 78f));
            bar.type = Image.Type.Sliced;
            bar.color = new Color(1f, 0.97f, 0.93f, 0.96f);
            bar.raycastTarget = false;
            rewardTrack = bar.rectTransform;

            Image line = UiKit.Icon(rewardTrack, "Line", UiKit.Rounded, new Vector2(0.5f, 0.5f),
                new Vector2(-36f, 10f), new Vector2(520f, 10f));
            line.type = Image.Type.Sliced;
            line.color = new Color(0.84f, 0.62f, 0.58f, 1f);
            line.raycastTarget = false;

            Sprite check = null;
            for (int i = 0; i < readyMarks.Length; i++)
            {
                if (readyMarks[i] != null && readyMarks[i].sprite != null)
                {
                    check = readyMarks[i].sprite;
                    break;
                }
            }

            for (int i = 0; i < rewardNodes.Length; i++)
            {
                float x = -220f + i * 150f;
                Image node = UiKit.Icon(rewardTrack, "Node_" + i, UiKit.Circle, new Vector2(0.5f, 0.5f),
                    new Vector2(x, 10f), new Vector2(52f, 52f));
                node.color = new Color(0.98f, 0.94f, 0.9f, 1f);
                rewardNodes[i] = node;
                Image mark = UiKit.Icon(node.transform, "Check", check, new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(36f, 36f));
                mark.enabled = false;
                rewardChecks[i] = mark;
                rewardNumbers[i] = UiKit.Label(node.transform, "Num", (i + 1).ToString(),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 52f), 26, Cocoa);
            }

            rewardPrize = UiKit.Icon(rewardTrack, "Prize", null, new Vector2(0.5f, 0.5f),
                new Vector2(200f, 8f), new Vector2(56f, 56f));
            UiKit.Label(rewardTrack, "PrizeLabel", "奖励", new Vector2(0.5f, 0.5f),
                new Vector2(286f, 8f), new Vector2(80f, 36f), 24, Cocoa);
            if (box != null)
                rewardTrack.SetSiblingIndex(box.transform.GetSiblingIndex() + 1);
        }

        public void SetBoxSlot(int index, Sprite sprite)
        {
            if (binFaces != null && index >= 0 && index < binFaces.Length && binFaces[index] != null && binOpen != null)
            {
                bool shut = sprite != null;
                binFaces[index].gameObject.SetActive(!shut);
                Transform lid = binFaces[index].transform.parent.Find("Shut");
                if (lid != null)
                {
                    lid.gameObject.SetActive(shut);
                    ApplyStamp(lid, sprite);
                }
                return;
            }
            if (index < 0 || boxSlots == null || index >= boxSlots.Length || boxSlots[index] == null) return;
            boxSlots[index].sprite = sprite;
            boxSlots[index].enabled = sprite != null;
        }

        public void ClearBox()
        {
            int n = binFaces != null && binFaces.Length > 0 ? binFaces.Length : (boxSlots != null ? boxSlots.Length : 0);
            for (int i = 0; i < n; i++)
                SetBoxSlot(i, null);
        }

        void ApplyStamp(Transform lid, Sprite sprite)
        {
            Transform found = lid.Find("Stamp");
            if (sprite == null)
            {
                if (found != null)
                    found.gameObject.SetActive(false);
                return;
            }
            Image stamp;
            if (found == null)
            {
                stamp = UiKit.Icon(lid, "Stamp", sprite, new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 16f), new Vector2(72f, 72f));
                stamp.raycastTarget = false;
                Sprite check = CheckSprite;
                if (check != null)
                {
                    Image tick = UiKit.Icon(stamp.transform, "Tick", check, new Vector2(1f, 0f),
                        new Vector2(2f, 6f), new Vector2(34f, 34f));
                    tick.raycastTarget = false;
                }
            }
            else
            {
                found.gameObject.SetActive(true);
                stamp = found.GetComponent<Image>();
                if (stamp != null)
                    stamp.sprite = sprite;
            }
            var root = lid.parent as RectTransform;
            FitStamp(lid, root != null ? root.sizeDelta.x : 156f);
        }

        static void FitStamp(Transform lid, float width)
        {
            Transform stamp = lid.Find("Stamp");
            if (stamp == null) return;
            float s = Mathf.Clamp(width * 0.46f, 48f, 78f);
            var rt = (RectTransform)stamp;
            rt.sizeDelta = new Vector2(s, s);
            rt.anchoredPosition = new Vector2(0f, width * 0.1f);
            Transform tick = stamp.Find("Tick");
            if (tick is RectTransform tickRect)
                tickRect.sizeDelta = new Vector2(s * 0.46f, s * 0.46f);
        }

        public void ShowToast(string message)
        {
            if (toastLabel == null) return;
            toastLabel.text = message;
        }

        public void Wire(UnityAction onBack, UnityAction onRestart, UnityAction onPack, UnityAction onRefill,
            UnityAction<int> onLane)
        {
            Bind(backButton, onBack);
            Sfx.BindSilent(restartButton, onRestart);
            Sfx.BindSilent(packButton, onPack);
            Sfx.BindSilent(refillButton, onRefill);
            for (int i = 0; i < laneFaces.Length; i++)
            {
                int index = i;
                Button button = laneFaces[i] != null ? laneFaces[i].GetComponent<Button>() : null;
                Sfx.BindSilent(button, () => onLane?.Invoke(index));
            }
        }

        static Button MakeHit(Image image)
        {
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            return button;
        }

        static void Bind(Button button, UnityAction action)
        {
            Sfx.BindClick(button, action);
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
