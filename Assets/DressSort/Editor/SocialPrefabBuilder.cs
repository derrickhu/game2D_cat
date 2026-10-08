using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort.EditorTools
{
    /// <summary>
    /// 首页左侧三个入口的弹窗：签到、排行榜、游戏圈。全部用 Art/Ui/Social 的糖果风切图拼，
    /// 写到 Resources/Prefabs，运行时 PopupView.Open 直接实例化。
    /// </summary>
    public static class SocialPrefabBuilder
    {
        const string Art = "Assets/DressSort/Art/";
        const string PrefabDir = "Assets/DressSort/Prefabs/Social";
        const string ResourceDir = "Assets/DressSort/Resources/Prefabs";

        // 和切图轮廓线同一个深梅紫，字和画是一家人。
        static readonly Color Ink = new Color32(0x3A, 0x1C, 0x35, 0xFF);
        static readonly Color Sub = new Color32(0x8A, 0x4A, 0x5E, 0xFF);
        static readonly Color Berry = new Color32(0xD0, 0x40, 0x5E, 0xFF);
        static readonly Color Cocoa = new Color32(0x4A, 0x26, 0x2C, 0xFF);

        static Sprite boardCheckIn, boardClub, boardRank, ribbonCheckIn, ribbonClub, ribbonRank, close;
        static Sprite dayIdle, dayToday, dayDone, day7Idle, day7Today, stamp;
        static Sprite medal1, medal2, medal3, badge, plate, ring, row, rowMine;
        static Sprite btnGreen, btnOrange, btnPurple, energy, club;

        [MenuItem("一裙又一裙/重建签到排行游戏圈预制", priority = 6)]
        public static void Rebuild()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "DressSort/Prefabs/Social"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "DressSort/Resources/Prefabs"));
            LoadSprites();

            Save(BuildCheckIn(), "CheckInPopup");
            Save(BuildClub(), "ClubPopup");
            Save(BuildRank(), "RankPopup");
            Save(BuildQuest(), "QuestPopup");
            Save(BuildEnergy(), "EnergyAdPopup");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[一裙又一裙] 签到 / 排行榜 / 游戏圈预制已写入 Resources/Prefabs");
        }

        static void LoadSprites()
        {
            boardCheckIn = Load("Ui/Social/board_checkin");
            boardClub = Load("Ui/Social/board_club");
            boardRank = Load("Ui/Social/board_rank");
            ribbonCheckIn = Load("Ui/Social/ribbon_checkin");
            ribbonClub = Load("Ui/Social/ribbon_club");
            ribbonRank = Load("Ui/Social/ribbon_rank");
            close = Load("Ui/Social/social_close");
            dayIdle = Load("Ui/Social/day_idle");
            dayToday = Load("Ui/Social/day_today");
            dayDone = Load("Ui/Social/day_done");
            day7Idle = Load("Ui/Social/day7_idle");
            day7Today = Load("Ui/Social/day7_today");
            stamp = Load("Ui/Social/day_stamp");
            medal1 = Load("Ui/Social/rank_medal_1");
            medal2 = Load("Ui/Social/rank_medal_2");
            medal3 = Load("Ui/Social/rank_medal_3");
            badge = Load("Ui/Social/rank_badge");
            plate = Load("Ui/Social/rank_plate");
            ring = Load("Ui/Social/rank_ring");
            row = Load("Ui/Social/rank_row");
            rowMine = Load("Ui/Social/rank_row_mine");
            btnGreen = Load("Ui/Social/btn_green");
            btnOrange = Load("Ui/Social/btn_orange");
            btnPurple = Load("Ui/Social/btn_purple");
            energy = Load("Ui/Home/icon_home_energy");
            club = Load("Ui/Home/icon_home_circle");
        }

        static Sprite Load(string pathNoExt)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + pathNoExt + ".png");
            if (sprite == null) Debug.LogWarning("[一裙又一裙] 缺切图 " + pathNoExt);
            return sprite;
        }

        static void Save(GameObject go, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + name + ".prefab");
            PrefabUtility.SaveAsPrefabAsset(go, ResourceDir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
        }

        // ------------------------------------------------------------ 公共外框

        /// <summary>遮罩 + 花布边框底板 + 缎带标题 + 右上关闭 + 提示条。返回底板，内容都按底板中心摆。</summary>
        static RectTransform Frame<T>(string name, Sprite boardSprite, Sprite ribbonSprite, string title,
            Vector2 boardSize, float boardY, out T view)
            where T : PopupView
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var dim = UiKit.Stretch(rect, "Dim").gameObject.AddComponent<Image>();
            dim.color = new Color(0.16f, 0.08f, 0.14f, 0.6f);
            dim.raycastTarget = true;

            Image boardImg = Img(rect, "Board", boardSprite, new Vector2(0f, boardY), boardSize, true);
            boardImg.raycastTarget = true;
            RectTransform b = boardImg.rectTransform;

            float top = boardSize.y * 0.5f;
            Image ribbon = Img(b, "Title", ribbonSprite, new Vector2(0f, top + 10f), new Vector2(700f, 172f));
            Shadowed(Txt(ribbon.rectTransform, "Text", title, new Vector2(0f, 16f), new Vector2(560f, 90f), 56,
                Color.white));

            Image x = Img(b, "Close", close, new Vector2(boardSize.x * 0.5f - 40f, top - 36f), new Vector2(104f, 111f));
            x.raycastTarget = true;
            var closeBtn = x.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = x;
            Pressed(closeBtn);

            Image toast = Img(rect, "Toast", rowMine, new Vector2(0f, boardY), new Vector2(760f, 120f), true);
            Text toastText = Txt(toast.rectTransform, "Text", "", new Vector2(0f, 4f), new Vector2(700f, 100f), 34, Ink);
            toast.gameObject.SetActive(false);

            view = root.AddComponent<T>();
            view.board = b;
            view.closeButton = closeBtn;
            view.toastPlate = toast;
            view.toastLabel = toastText;
            return b;
        }

        // ------------------------------------------------------------ 签到

        static GameObject BuildCheckIn()
        {
            var size = new Vector2(920f, 1330f);
            RectTransform b = Frame<CheckInPopup>("CheckInPopup", boardCheckIn, ribbonCheckIn, "每日签到", size, -30f,
                out var view);
            float top = size.y * 0.5f;

            Txt(b, "Sub", "连续签到领体力，断签从第 1 天重来", new Vector2(0f, top - 170f), new Vector2(700f, 44f), 30, Sub);

            var cardSize = new Vector2(224f, 286f);
            float row1 = top - 215f - cardSize.y * 0.5f;
            float row2 = row1 - 300f;
            for (int i = 0; i < 6; i++)
            {
                float x = (i % 3 - 1) * 262f;
                float y = i < 3 ? row1 : row2;
                Image card = Img(b, "Day" + (i + 1), dayIdle, new Vector2(x, y), cardSize);
                RectTransform c = card.rectTransform;
                view.dayLabels[i] = Txt(c, "Label", "第 " + (i + 1) + " 天", new Vector2(0f, 120f),
                    new Vector2(220f, 40f), 26, Cocoa);
                Img(c, "Energy", energy, new Vector2(0f, 14f), new Vector2(60f, 94f));
                view.amounts[i] = Txt(c, "Amount", "×2", new Vector2(0f, -62f), new Vector2(200f, 50f), 34, Ink);
                view.stamps[i] = Stamp(c, new Vector2(64f, 30f), 96f);
                view.cards[i] = card;
            }

            float wideY = row2 - cardSize.y * 0.5f - 25f - 75f;
            Image wide = Img(b, "Day7", day7Idle, new Vector2(0f, wideY), new Vector2(700f, 150f), true);
            wide.pixelsPerUnitMultiplier = 1.5f;
            RectTransform w = wide.rectTransform;
            Txt(w, "Label", "第 7 天", new Vector2(-210f, 10f), new Vector2(200f, 56f), 38, Ink);
            Img(w, "Energy", energy, new Vector2(-20f, 10f), new Vector2(64f, 100f));
            view.amounts[6] = Txt(w, "Amount", "×5", new Vector2(90f, 8f), new Vector2(160f, 70f), 52, Berry);
            view.stamps[6] = Stamp(w, new Vector2(240f, 6f), 110f);
            view.cards[6] = wide;

            float btnY = wideY - 75f - 30f - 70f;
            view.signButton = Capsule(b, "Sign", "签到", new Vector2(0f, btnY), new Vector2(440f, 140f), btnGreen,
                out view.signLabel);

            view.cardIdle = dayIdle;
            view.cardToday = dayToday;
            view.cardDone = dayDone;
            view.wideIdle = day7Idle;
            view.wideToday = day7Today;
            view.signReady = btnGreen;
            view.signDone = btnPurple;
            return view.gameObject;
        }

        static Image Stamp(RectTransform parent, Vector2 pos, float size)
        {
            Image s = Img(parent, "Stamp", stamp, pos, new Vector2(size, size * 1.06f));
            s.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            s.gameObject.SetActive(false);
            return s;
        }

        // ------------------------------------------------------------ 游戏圈

        static GameObject BuildClub()
        {
            var size = new Vector2(920f, 1100f);
            RectTransform b = Frame<ClubPopup>("ClubPopup", boardClub, ribbonClub, "游戏圈", size, -20f, out var view);

            Img(b, "Hero", club, new Vector2(0f, 320f), new Vector2(170f, 179f));
            Txt(b, "Sub", "每天在游戏圈发一条帖子就能领", new Vector2(0f, 200f), new Vector2(700f, 44f), 30, Sub);

            Image card = Img(b, "Reward", dayToday, new Vector2(0f, 15f), new Vector2(224f, 286f));
            RectTransform c = card.rectTransform;
            Txt(c, "Label", "今日奖励", new Vector2(0f, 120f), new Vector2(220f, 40f), 26, Color.white);
            Img(c, "Energy", energy, new Vector2(0f, 14f), new Vector2(60f, 94f));
            view.rewardLabel = Txt(c, "Amount", "×3", new Vector2(0f, -62f), new Vector2(200f, 50f), 36, Ink);

            view.taskLabel = Txt(b, "Task", "今日发帖  0/1", new Vector2(0f, -170f), new Vector2(700f, 52f), 34, Ink);

            view.goButton = Capsule(b, "Go", "去游戏圈", new Vector2(-180f, -282f), new Vector2(330f, 120f), btnOrange,
                out _);
            view.claimButton = Capsule(b, "Claim", "领取", new Vector2(180f, -282f), new Vector2(330f, 120f), btnGreen,
                out view.claimLabel);
            view.claimReady = btnGreen;
            view.claimDone = btnPurple;
            return view.gameObject;
        }

        // ------------------------------------------------------------ 排行榜

        const float RowW = 720f;
        const float RowH = 130f;

        static GameObject BuildRank()
        {
            var size = new Vector2(940f, 1440f);
            RectTransform b = Frame<RankPopup>("RankPopup", boardRank, ribbonRank, "排行榜", size, -40f, out var view);
            float top = size.y * 0.5f;
            float bottom = -top;

            Txt(b, "Sub", "按通关关数排名，同关数先到者居前", new Vector2(0f, top - 170f), new Vector2(700f, 44f), 28, Sub);

            float authY = bottom + 236f;
            float mineY = authY + 70f + 26f + RowH * 0.5f;
            float listBottom = mineY + RowH * 0.5f + 20f;
            float listTop = top - 205f;

            var vpGo = new GameObject("List", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            vpGo.transform.SetParent(b, false);
            var vp = (RectTransform)vpGo.transform;
            vp.pivot = new Vector2(0.5f, 0.5f);
            vp.anchorMin = new Vector2(0f, 0f);
            vp.anchorMax = new Vector2(1f, 1f);
            vp.offsetMin = new Vector2(100f, listBottom + top);
            vp.offsetMax = new Vector2(-100f, listTop - top);
            vpGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

            var content = UiKit.Rect(vp, "Rows", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(RowW + 20f, 0f));
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            var scroll = vpGo.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = vp;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            RankRowView template = Row(content, "RowTemplate", new Vector2(0f, -8f));
            var tr = (RectTransform)template.transform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            template.gameObject.SetActive(false);

            view.statusLabel = Txt(vp, "Status", "排行榜加载中…", new Vector2(0f, 40f), new Vector2(760f, 50f), 32, Sub);
            view.retryButton = Capsule(vp, "Retry", "重试", new Vector2(0f, -70f), new Vector2(260f, 100f), btnOrange,
                out _);

            view.mineRow = Row(b, "Mine", new Vector2(0f, mineY));
            view.authButton = Capsule(b, "Auth", "使用微信昵称头像上榜", new Vector2(0f, authY),
                new Vector2(600f, 124f), btnGreen, out _);

            view.viewport = vp;
            view.content = content;
            view.rowPrefab = template;
            view.rowStep = RowH + 14f;
            view.authSpace = 166f;
            view.medals = new[] { medal1, medal2, medal3 };
            view.rowIdle = row;
            view.rowMine = rowMine;
            return view.gameObject;
        }

        static RankRowView Row(RectTransform parent, string name, Vector2 pos)
        {
            Image bg = Img(parent, name, row, pos, new Vector2(RowW, RowH), true);
            bg.pixelsPerUnitMultiplier = 1.7f;
            RectTransform r = bg.rectTransform;
            var view = r.gameObject.AddComponent<RankRowView>();
            view.plate = bg;

            float left = -RowW * 0.5f;
            view.medal = Img(r, "Medal", medal1, new Vector2(left + 72f, 4f), new Vector2(78f, 104f));
            view.numberPlate = Img(r, "NumberPlate", badge, new Vector2(left + 72f, 2f), new Vector2(76f, 78f));
            view.numberLabel = Txt(view.numberPlate.rectTransform, "Number", "4", new Vector2(0f, 3f),
                new Vector2(76f, 50f), 34, Color.white);

            Image mask = Img(r, "Avatar", plate, new Vector2(left + 178f, 2f), new Vector2(70f, 70f));
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            view.avatarInitial = Txt(mask.rectTransform, "Initial", "裙", new Vector2(0f, 2f), new Vector2(70f, 70f), 32,
                Berry);
            view.avatarPicture = Img(mask.rectTransform, "Picture", null, Vector2.zero, new Vector2(70f, 70f));
            view.avatarPicture.preserveAspect = false;
            view.avatarPicture.gameObject.SetActive(false);
            Img(r, "Ring", ring, new Vector2(left + 178f, 2f), new Vector2(118f, 118f));

            view.nameLabel = Txt(r, "Name", "裙友", new Vector2(left + 254f + 140f, 6f), new Vector2(280f, 50f), 32, Ink,
                TextAnchor.MiddleLeft);
            view.tipLabel = Txt(r, "Tip", "", new Vector2(left + 254f + 140f, -22f), new Vector2(280f, 36f), 22, Sub,
                TextAnchor.MiddleLeft);
            view.tipLabel.gameObject.SetActive(false);
            view.countLabel = Txt(r, "Count", "0 关", new Vector2(RowW * 0.5f - 110f, 6f), new Vector2(160f, 56f), 36,
                Berry, TextAnchor.MiddleRight);
            return view;
        }

        static GameObject BuildQuest()
        {
            var size = new Vector2(940f, 1440f);
            RectTransform b = Frame<QuestPopup>("QuestPopup", boardRank, ribbonRank, "过关有礼", size, -40f, out var view);
            float top = size.y * 0.5f;
            float bottom = -top;

            Txt(b, "Sub", "通关后在这里领礼物", new Vector2(0f, top - 168f), new Vector2(700f, 40f), 28, Sub);

            Image chip = Img(b, "Progress", rowMine, new Vector2(0f, top - 230f), new Vector2(460f, 84f), true);
            view.progressLabel = Txt(chip.rectTransform, "Text", "已通关 0 关", new Vector2(0f, 4f),
                new Vector2(400f, 60f), 30, Ink);

            float listTop = top - 290f;
            float listBottom = bottom + 150f;
            var vpGo = new GameObject("List", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            vpGo.transform.SetParent(b, false);
            var vp = (RectTransform)vpGo.transform;
            vp.pivot = new Vector2(0.5f, 0.5f);
            vp.anchorMin = new Vector2(0f, 0f);
            vp.anchorMax = new Vector2(1f, 1f);
            vp.offsetMin = new Vector2(70f, listBottom + top);
            vp.offsetMax = new Vector2(-70f, listTop - top);
            vpGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

            var content = UiKit.Rect(vp, "Rows", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(780f, 0f));
            content.pivot = new Vector2(0.5f, 1f);
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            var scroll = vpGo.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = vp;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            Image foot = Img(b, "Footer", rowMine, new Vector2(0f, bottom + 86f), new Vector2(760f, 96f), true);
            view.footerLabel = Txt(foot.rectTransform, "Text", "距离下一份还差 2 关", new Vector2(0f, 12f),
                new Vector2(700f, 64f), 34, Ink);

            view.content = content;
            view.scroll = scroll;
            view.rowSprite = row;
            view.badgeSprite = badge;
            view.claimSprite = btnGreen;
            view.doneSprite = btnPurple;
            return view.gameObject;
        }

        static GameObject BuildEnergy()
        {
            var size = new Vector2(860f, 760f);
            RectTransform b = Frame<EnergyAdPopup>("EnergyAdPopup", boardClub, ribbonClub, "体力不够", size, 0f,
                out var view);
            Img(b, "Heart", energy, new Vector2(0f, 90f), new Vector2(120f, 188f));
            Txt(b, "Body", "看完一条广告，恢复 1 点体力", new Vector2(0f, -70f), new Vector2(640f, 56f), 32, Ink);
            view.watchButton = Capsule(b, "Watch", "看广告", new Vector2(0f, -210f), new Vector2(420f, 130f), btnOrange,
                out _);
            return view.gameObject;
        }

        // ------------------------------------------------------------ 小件

        static Image Img(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size, bool sliced = false)
        {
            RectTransform rect = UiKit.Rect(parent, name, new Vector2(0.5f, 0.5f), pos, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            if (sliced)
            {
                image.type = Image.Type.Sliced;
                if (sprite != null)
                {
                    Vector4 bd = sprite.border;
                    image.pixelsPerUnitMultiplier = Mathf.Max(1f,
                        (bd.y + bd.w) / (size.y * 0.95f), (bd.x + bd.z) / (size.x * 0.95f));
                }
            }
            else
                image.preserveAspect = true;
            return image;
        }

        static Text Txt(Transform parent, string name, string text, Vector2 pos, Vector2 size, int fontSize, Color color,
            TextAnchor align = TextAnchor.MiddleCenter)
        {
            Text t = UiKit.Label(parent, name, text, new Vector2(0.5f, 0.5f), pos, size, fontSize, color, align);
            // UiFont 已是 700 字重。FontStyle.Bold 会再错位叠一层，字发虚。
            t.fontStyle = FontStyle.Normal;
            t.resizeTextForBestFit = false;
            return t;
        }

        /// <summary>彩色底上的白字压一层往下 3px 的深梅紫投影，跟切图的厚底边一个方向。</summary>
        static Text Shadowed(Text t)
        {
            var shadow = t.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.23f, 0.11f, 0.21f, 0.75f);
            shadow.effectDistance = new Vector2(0f, -3f);
            return t;
        }

        static Button Capsule(Transform parent, string name, string caption, Vector2 pos, Vector2 size, Sprite face,
            out Text label)
        {
            Image img = Img(parent, name, face, pos, size, true);
            img.raycastTarget = true;
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            Pressed(button);
            int fontSize = Mathf.Min(44, Mathf.RoundToInt(size.y * 0.32f));
            label = Txt(img.rectTransform, "Text", caption, new Vector2(0f, size.y * 0.06f), size, fontSize, Cocoa);
            return button;
        }

        static void Pressed(Button button)
        {
            var colors = button.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f);
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.05f;
            button.colors = colors;
        }
    }
}
