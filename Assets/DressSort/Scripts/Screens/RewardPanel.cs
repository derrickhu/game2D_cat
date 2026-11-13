using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 过关领奖。外观在 Resources/Prefabs/RewardScreen，这里播开盒动画并填文案。
    /// 直送弹出衣服；图纸是一张浅色小纸，上面铺裙子小图；材料按个数各弹一个。
    /// </summary>
    public class RewardPanel : Panel
    {
        const float DressLift = 200f;
        const float PaperAspect = 596f / 774f;
        static readonly Color GlowGold = new Color(1f, 0.86f, 0.42f, 0.92f);

        static Sprite blueprintPaper;

        RewardHud hud;
        RectTransform stage;
        RectTransform gift;
        Image closed;
        Image boxBase;
        Image lid;
        Image rays;
        Image dress;
        Image blueprintIcon;
        RectTransform plate;
        Text nameLabel;
        Text hintLabel;
        Button takeButton;
        Text takeLabel;
        Button continueButton;
        Text continueLabel;
        Button doubleButton;
        bool canTryOn;
        bool matsDoubled;
        bool matAdBusy;
        RectTransform fxLayer;
        Image popperLeft;
        Image popperRight;

        Vector2 closedSize, baseSize, lidSize;
        ItemDef reward;
        bool isBlueprint;
        bool revealed;

        class Prize
        {
            public RectTransform rect;
            public Image badge;
            public bool card;
            public Vector2 rest;
            public RectTransform glow;
            public Image glowImage;
            public RectTransform[] sparks;
            public Image[] sparkImages;
            public Text times;
        }

        readonly List<Prize> prizes = new List<Prize>();
        float idle;
        float rainLeft;
        float rainClock;

        class Bit
        {
            public RectTransform rect;
            public Image image;
            public Vector2 velocity;
            public float gravity;
            public float drag;
            public float spin;
            public float age;
            public float life;
            public float sway;
        }

        readonly List<Bit> bits = new List<Bit>();
        readonly Stack<Bit> spare = new Stack<Bit>();

        GameDatabase Db => app.Database;

        protected override void Build()
        {
            Transform frameBg = transform.Find("Backdrop");
            if (frameBg != null)
                frameBg.gameObject.SetActive(false);

            GameObject prefab = Resources.Load<GameObject>("Prefabs/RewardScreen");
            hud = prefab != null
                ? Instantiate(prefab, transform).GetComponent<RewardHud>()
                : RewardHud.Assemble((RectTransform)transform, app.Database);
            if (hud == null)
            {
                Debug.LogError("[一裙又一裙] 领奖预制没有 RewardHud，先跑「重建领奖预制」");
                return;
            }

            var rect = (RectTransform)hud.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();

            stage = hud.stage;
            gift = hud.gift;
            closed = hud.closed;
            boxBase = hud.boxBase;
            lid = hud.lid;
            rays = hud.rays;
            dress = hud.dress;
            blueprintIcon = hud.blueprintIcon;
            plate = hud.plate;
            nameLabel = hud.nameLabel;
            hintLabel = hud.hintLabel;
            takeButton = hud.takeButton;
            takeLabel = hud.takeLabel;
            continueButton = hud.continueButton;
            continueLabel = hud.continueLabel;
            fxLayer = hud.fxLayer;
            popperLeft = hud.popperLeft;
            popperRight = hud.popperRight;
            closedSize = closed.rectTransform.sizeDelta;
            baseSize = boxBase.rectTransform.sizeDelta;
            lidSize = lid.rectTransform.sizeDelta;
            hud.ApplyChrome(app.Database);
            continueButton = hud.continueButton;
            continueLabel = hud.continueLabel;
            hud.Wire(OnTap, OnTake, OnContinue);
        }

        public override void OnShow()
        {
            LevelDef level = app.PendingLevel;
            reward = level != null ? level.reward : null;
            isBlueprint = reward == null && app.PendingNewPrint && level != null && level.blueprint != null;
            if (isBlueprint)
                reward = level.blueprint;
            // 只有直接送进衣柜的裙子才能试穿。图纸、材料、发型都改成回首页。
            canTryOn = !app.GmPlay && !isBlueprint && reward != null && reward.slot == ItemSlot.Dress;
            takeLabel.text = canTryOn ? "收下并试穿" : "回到首页";
            LevelDef next = app.GmPlay
                ? (level != null ? app.LevelAt(level.index + 1) : null)
                : app.NextLevel;
            continueLabel.text = next != null ? "继续第" + next.index + "关" : "回首页";
            revealed = false;
            matsDoubled = false;
            matAdBusy = false;
            ClearBits();
            BuildPrizes();
            ResetPose();
            PrepareDoubleButton();
            if (prizes.Count == 0) return;
            StartCoroutine(ShowRoutine());
        }

        public override void OnHide()
        {
            StopAllCoroutines();
            ClearBits();
            ClearPrizes();
        }

        void ResetPose()
        {
            idle = 0f;
            rainLeft = 0f;
            gift.anchoredPosition = new Vector2(0f, 1100f);
            gift.localScale = Vector3.one;
            gift.localRotation = Quaternion.identity;
            closed.gameObject.SetActive(true);
            boxBase.gameObject.SetActive(false);
            lid.gameObject.SetActive(false);
            lid.color = Color.white;
            dress.gameObject.SetActive(false);
            if (blueprintIcon != null)
                blueprintIcon.gameObject.SetActive(false);
            rays.gameObject.SetActive(false);
            rays.rectTransform.localScale = Vector3.zero;
            plate.localScale = Vector3.zero;
            hintLabel.text = "";
            nameLabel.text = "";
            takeButton.transform.localScale = Vector3.zero;
            takeButton.gameObject.SetActive(false);
            continueButton.transform.localScale = Vector3.zero;
            continueButton.gameObject.SetActive(false);
            if (doubleButton != null)
            {
                doubleButton.transform.localScale = Vector3.zero;
                doubleButton.gameObject.SetActive(false);
            }
            popperLeft.rectTransform.localScale = Vector3.zero;
            popperRight.rectTransform.localScale = Vector3.zero;
            Vector2 mouth = new Vector2(0f, baseSize.y * 0.72f);
            for (int i = 0; i < prizes.Count; i++)
            {
                prizes[i].rect.gameObject.SetActive(false);
                prizes[i].rect.localScale = Vector3.one * 0.15f;
                prizes[i].rect.anchoredPosition = mouth;
            }
        }

        void OnTap()
        {
            if (revealed || prizes.Count == 0) return;
            ApplyReveal();
        }

        IEnumerator ShowRoutine()
        {
            yield return Drop();
            yield return Wiggle();
            OpenLid();
            yield return RisePrizes();
            FinishReveal();
            yield return PopIn(plate, 0.28f);
            if (OfferDouble)
                yield return PopIn((RectTransform)doubleButton.transform, 0.22f);
            yield return PopIn((RectTransform)takeButton.transform, 0.22f);
            yield return PopIn((RectTransform)continueButton.transform, 0.22f);
        }

        IEnumerator Drop()
        {
            const float duration = 0.42f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                gift.anchoredPosition = new Vector2(0f, Mathf.Lerp(1100f, 0f, k * k));
                yield return null;
            }
            gift.anchoredPosition = Vector2.zero;
            Sfx.Play(SfxId.GiftLand);

            const float squash = 0.18f;
            for (float t = 0f; t < squash; t += Time.deltaTime)
            {
                float k = Mathf.Sin(t / squash * Mathf.PI);
                gift.localScale = new Vector3(1f + k * 0.12f, 1f - k * 0.14f, 1f);
                yield return null;
            }
            gift.localScale = Vector3.one;
        }

        IEnumerator Wiggle()
        {
            const float duration = 0.66f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float angle = Mathf.Sin(k * Mathf.PI * 6f) * Mathf.Lerp(3f, 9f, k);
                gift.localRotation = Quaternion.Euler(0f, 0f, angle);
                float swell = 1f + k * 0.06f;
                gift.localScale = new Vector3(swell, 1f + Mathf.Abs(Mathf.Sin(k * Mathf.PI * 6f)) * 0.04f * k + k * 0.04f, 1f);
                yield return null;
            }
            gift.localRotation = Quaternion.identity;
            gift.localScale = Vector3.one;
        }

        /// <summary>闭合的盒子换成盒身加盒盖，盒盖原地起跳飞走，同时放光和礼花。</summary>
        void OpenLid()
        {
            Sfx.Play(SfxId.GiftPop);
            closed.gameObject.SetActive(false);
            boxBase.gameObject.SetActive(true);
            lid.gameObject.SetActive(true);
            lid.rectTransform.anchoredPosition = new Vector2(0f, closedSize.y - lidSize.y * 0.5f);
            lid.rectTransform.localRotation = Quaternion.identity;
            StartCoroutine(FlyLid());
            StartCoroutine(GrowRays());
            FirePoppers();
            BurstSparkles(new Vector2(0f, baseSize.y));
            rainLeft = 3.2f;
        }

        IEnumerator FlyLid()
        {
            RectTransform rect = lid.rectTransform;
            Vector2 pos = rect.anchoredPosition;
            Vector2 velocity = new Vector2(420f, 1650f);
            float angle = 0f;
            const float life = 0.9f;
            for (float t = 0f; t < life; t += Time.deltaTime)
            {
                float dt = Time.deltaTime;
                velocity.y -= 3200f * dt;
                pos += velocity * dt;
                angle -= 320f * dt;
                rect.anchoredPosition = pos;
                rect.localRotation = Quaternion.Euler(0f, 0f, angle);
                lid.color = new Color(1f, 1f, 1f, Mathf.Clamp01((life - t) / 0.3f));
                yield return null;
            }
            lid.gameObject.SetActive(false);
        }

        IEnumerator GrowRays()
        {
            rays.gameObject.SetActive(true);
            const float duration = 0.36f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                rays.rectTransform.localScale = Vector3.one * EaseOutBack(t / duration);
                yield return null;
            }
            rays.rectTransform.localScale = Vector3.one;
        }

        IEnumerator RisePrizes()
        {
            for (int i = 0; i < prizes.Count; i++)
                StartCoroutine(RiseOne(prizes[i], i * 0.08f));
            float wait = 0.55f + Mathf.Max(0, prizes.Count - 1) * 0.08f;
            yield return new WaitForSeconds(wait);
        }

        IEnumerator RiseOne(Prize prize, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            if (prize.rect == null) yield break;
            prize.rect.gameObject.SetActive(true);
            Vector2 from = new Vector2(0f, baseSize.y * 0.72f);
            const float duration = 0.5f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float e = EaseOutBack(t / duration);
                prize.rect.anchoredPosition = Vector2.LerpUnclamped(from, prize.rest, e);
                prize.rect.localScale = Vector3.one * Mathf.LerpUnclamped(0.15f, 1f, e);
                yield return null;
            }
            prize.rect.anchoredPosition = prize.rest;
            prize.rect.localScale = Vector3.one;
        }

        IEnumerator PopIn(RectTransform rect, float duration)
        {
            rect.gameObject.SetActive(true);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                rect.localScale = Vector3.one * EaseOutBack(t / duration);
                yield return null;
            }
            rect.localScale = Vector3.one;
        }

        /// <summary>自检脚本和点击跳过：直接摆到揭开后的样子。</summary>
        public void ApplyReveal()
        {
            if (prizes.Count == 0) return;
            bool stillClosed = closed != null && closed.gameObject.activeSelf;
            StopAllCoroutines();
            gift.anchoredPosition = Vector2.zero;
            gift.localScale = Vector3.one;
            gift.localRotation = Quaternion.identity;
            closed.gameObject.SetActive(false);
            boxBase.gameObject.SetActive(true);
            lid.gameObject.SetActive(false);
            rays.gameObject.SetActive(true);
            rays.rectTransform.localScale = Vector3.one;
            dress.gameObject.SetActive(false);
            for (int i = 0; i < prizes.Count; i++)
            {
                prizes[i].rect.gameObject.SetActive(true);
                prizes[i].rect.anchoredPosition = prizes[i].rest;
                prizes[i].rect.localScale = Vector3.one;
            }
            if (popperLeft.rectTransform.localScale.x < 0.5f)
                FirePoppers();
            popperLeft.rectTransform.localScale = Vector3.one;
            popperRight.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            FinishReveal();
            plate.localScale = Vector3.one;
            if (OfferDouble)
            {
                doubleButton.gameObject.SetActive(true);
                doubleButton.transform.localScale = Vector3.one;
            }
            else if (doubleButton != null)
            {
                doubleButton.gameObject.SetActive(false);
            }
            takeButton.gameObject.SetActive(true);
            takeButton.transform.localScale = Vector3.one;
            continueButton.gameObject.SetActive(true);
            continueButton.transform.localScale = Vector3.one;
            if (rainLeft <= 0f)
                rainLeft = 2f;
            if (stillClosed)
                Sfx.Play(SfxId.GiftPop);
        }

        void FinishReveal()
        {
            if (revealed) return;
            revealed = true;
            string mats = CraftCatalog.Describe(app.PendingMaterials);
            if (isBlueprint && reward != null)
            {
                nameLabel.text = "获得图纸「" + reward.displayName + "」";
                hintLabel.text = string.IsNullOrEmpty(mats) ? "材料够了就能在工坊制作" : "还获得 " + mats;
            }
            else if (reward != null)
            {
                if (!app.GmPlay)
                    app.Wardrobe.Unlock(reward);
                nameLabel.text = "解锁了「" + reward.displayName + "」";
                hintLabel.text = reward.slot == ItemSlot.Wings ? "翅膀已收进衣柜" : "已经放进衣柜";
            }
            else
            {
                nameLabel.text = "获得材料";
                hintLabel.text = mats;
            }
        }

        /// <summary>直送一件衣服。图纸是浅色小纸，裙子小图铺在纸上。材料按份数各一个。</summary>
        void BuildPrizes()
        {
            ClearPrizes();
            if (isBlueprint && reward != null)
                SpawnPaper(reward.slot == ItemSlot.Hair ? HairIcon(reward) : reward.ResolveIcon(),
                    reward.slot == ItemSlot.Hair);
            else if (reward != null)
                SpawnPrize(reward.ResolveIcon(), null, false);

            if (reward == null || isBlueprint)
                SpawnMaterials();
            LayoutPrizes();
        }

        void SpawnMaterials()
        {
            int[] mats = app.PendingMaterials;
            if (mats == null) return;
            for (int m = 0; m < mats.Length && m < CraftCatalog.MatCount; m++)
            {
                Sprite icon = CraftView.MatIcon(Db, m);
                for (int n = 0; n < mats[m]; n++)
                    SpawnPrize(icon, null, false);
            }
        }

        /// <summary>金边证书纸。裙子铺满纸心，发型小图居中，后面一圈会呼吸的金光。</summary>
        void SpawnPaper(Sprite icon, bool hair = false)
        {
            RectTransform sheet = UiKit.Rect(gift, "Blueprint", new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(220f, 286f));
            var prize = new Prize { rect = sheet, card = true };
            if (Db != null && Db.rewardRays != null)
            {
                Image glow = UiKit.Icon(sheet, "Glow", Db.rewardRays, new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(460f, 460f));
                glow.color = GlowGold;
                prize.glow = glow.rectTransform;
                prize.glowImage = glow;
            }

            Sprite art = BlueprintPaper();
            if (art != null)
            {
                Image face = UiKit.Icon(sheet, "Face", art, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.one * 100f);
                face.preserveAspect = false;
                Fill(face.rectTransform, 0f);
            }
            else
            {
                Image rim = UiKit.Slice(sheet, "Rim", UiKit.SoftRect, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero, new Color(0.93f, 0.72f, 0.22f));
                Fill(rim.rectTransform, 0f);
                Image face = UiKit.Slice(sheet, "Face", UiKit.SoftRect, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero, new Color(1f, 0.97f, 0.91f));
                Fill(face.rectTransform, 8f);
            }

            if (icon != null)
            {
                Image mark = UiKit.Icon(sheet, "Dress", icon, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.one * 100f);
                RectTransform dressRect = mark.rectTransform;
                // 裙子图标四周有透明边，框画大一点才铺满纸心。发型小图居中，别撑破金边。
                dressRect.anchorMin = hair ? new Vector2(0.22f, 0.10f) : new Vector2(-0.06f, 0.05f);
                dressRect.anchorMax = hair ? new Vector2(0.78f, 0.78f) : new Vector2(1.06f, 0.90f);
                dressRect.offsetMin = Vector2.zero;
                dressRect.offsetMax = Vector2.zero;
            }

            if (Db != null && Db.rewardSparkle != null)
            {
                prize.sparks = new RectTransform[4];
                prize.sparkImages = new Image[4];
                for (int s = 0; s < 4; s++)
                {
                    Image star = UiKit.Icon(sheet, "Spark", Db.rewardSparkle, new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(52f, 52f));
                    prize.sparks[s] = star.rectTransform;
                    prize.sparkImages[s] = star;
                }
            }
            prizes.Add(prize);
        }

        /// <summary>发型用 Icons/hair_ 小图。物品上的引用有时指到全身头像，这里按文件名取。</summary>
        static Sprite HairIcon(ItemDef item)
        {
            if (item == null) return null;
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(item.id))
            {
                string key = item.id.StartsWith("hair_") ? item.id.Substring(5) : item.id;
                Sprite file = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/DressSort/Art/Icons/hair_" + key + ".png");
                if (file != null) return file;
            }
#endif
            return item.icon;
        }

        static Sprite BlueprintPaper()
        {
            if (blueprintPaper == null)
                blueprintPaper = Resources.Load<Sprite>("Ui/blueprint_paper");
            return blueprintPaper;
        }

        static void Fill(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        void SpawnPrize(Sprite sprite, Sprite badge, bool card)
        {
            Image image = UiKit.Icon(gift, card ? "Blueprint" : "Prize", sprite, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.one * 200f);
            image.preserveAspect = true;
            image.raycastTarget = false;
            if (image.sprite == null)
            {
                image.sprite = UiKit.Circle;
                image.color = Palette.Lemon;
            }
            var prize = new Prize { rect = image.rectTransform, card = card };
            if (badge != null)
            {
                Image mark = UiKit.Icon(image.transform, "Dress", badge, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.one * 100f);
                mark.preserveAspect = true;
                mark.raycastTarget = false;
                prize.badge = mark;
            }
            prizes.Add(prize);
        }

        void LayoutPrizes()
        {
            int n = prizes.Count;
            if (n == 0) return;
            float paperH = n <= 1 ? 340f : n <= 3 ? 286f : n <= 6 ? 220f : 180f;
            float mat = paperH * 0.56f;
            var widths = new float[n];
            float width = 0f;
            for (int i = 0; i < n; i++)
            {
                widths[i] = prizes[i].card ? paperH * PaperAspect : mat;
                width += widths[i];
            }
            float gap = n <= 1 ? 0f : 18f;
            width += gap * (n - 1);
            const float limit = 980f;
            if (n > 1 && width > limit)
            {
                float scale = limit / width;
                paperH *= scale;
                mat *= scale;
                gap *= scale;
                for (int i = 0; i < n; i++)
                    widths[i] *= scale;
                width = limit;
            }
            float x = -width * 0.5f;
            float lift = baseSize.y + (n <= 1 ? DressLift : 140f);
            for (int i = 0; i < n; i++)
            {
                float h = prizes[i].card ? paperH : mat;
                prizes[i].rect.sizeDelta = new Vector2(widths[i], h);
                if (prizes[i].glow != null)
                {
                    float g = h * 1.65f;
                    prizes[i].glow.sizeDelta = new Vector2(g, g);
                }
                if (prizes[i].sparks != null)
                {
                    float spark = h * 0.22f;
                    for (int s = 0; s < prizes[i].sparks.Length; s++)
                        prizes[i].sparks[s].sizeDelta = new Vector2(spark, spark);
                }
                if (prizes[i].badge != null)
                {
                    float mark = h * (200f / 630f);
                    prizes[i].badge.rectTransform.sizeDelta = new Vector2(mark, mark);
                    prizes[i].badge.rectTransform.anchoredPosition = new Vector2(0f, h * (165f / 630f));
                }
                float arc = n <= 1 ? 0f : Mathf.Sin((i + 0.5f) / n * Mathf.PI) * 28f;
                prizes[i].rest = new Vector2(x + widths[i] * 0.5f, lift + arc);
                x += widths[i] + gap;
            }
        }

        void ClearPrizes()
        {
            for (int i = 0; i < prizes.Count; i++)
            {
                if (prizes[i].rect == null) continue;
                if (Application.isPlaying)
                    Destroy(prizes[i].rect.gameObject);
                else
                    DestroyImmediate(prizes[i].rect.gameObject);
            }
            prizes.Clear();
        }

        bool OfferDouble => !matsDoubled && !app.GmPlay && CraftCatalog.Any(app.PendingMaterials);

        void PrepareDoubleButton()
        {
            if (!OfferDouble)
            {
                if (doubleButton != null)
                    doubleButton.gameObject.SetActive(false);
                return;
            }
            if (doubleButton == null)
            {
                const float buttonH = 124f;
                doubleButton = UiKit.Button(hud.transform, "看广告 材料翻倍", new Vector2(0.5f, 0.204f),
                    Vector2.zero, new Vector2(580f, buttonH),
                    AdButton.Face(app.Database), Color.white, 42, OnDoubleMaterials);
                AdButton.Apply(doubleButton, app.Database);
                Text label = doubleButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.fontStyle = FontStyle.Bold;
                    label.alignment = TextAnchor.MiddleCenter;
                }
            }
            else
                AdButton.Apply(doubleButton, app.Database);
            doubleButton.interactable = true;
            Text caption = doubleButton.GetComponentInChildren<Text>();
            if (caption != null)
                caption.text = "看广告 材料翻倍";
            doubleButton.transform.localScale = Vector3.zero;
            doubleButton.gameObject.SetActive(false);
            doubleButton.transform.SetAsLastSibling();
        }

        void OnDoubleMaterials()
        {
            if (!OfferDouble || matAdBusy) return;
            int[] mats = app.PendingMaterials;
            matAdBusy = true;
            doubleButton.interactable = false;
            const string scene = "reward_mats";
            Analytics.AdRequest(scene, WxBridge.RewardAdUnitId);
            WxBridge.ShowRewarded(WxBridge.RewardAdUnitId, () =>
            {
                matAdBusy = false;
                matsDoubled = true;
                if (!app.GmPlay)
                    app.Wardrobe.AddMaterials(mats);
                Analytics.AdShow(scene, WxBridge.RewardAdUnitId);
                Analytics.AdClose(scene, WxBridge.RewardAdUnitId, true);
                if (doubleButton != null)
                    doubleButton.gameObject.SetActive(false);
                if (hintLabel != null)
                    hintLabel.text = "材料已翻倍";
                StartCoroutine(PlayDouble());
            }, () =>
            {
                matAdBusy = false;
                doubleButton.interactable = true;
                Sfx.Play(SfxId.Deny);
                if (hintLabel != null)
                    StartCoroutine(FlashHint(hintLabel.text, "广告没看完"));
            });
        }

        /// <summary>看完广告回来：材料保持原样，每份下面弹出 ×2。</summary>
        IEnumerator PlayDouble()
        {
            Sfx.Play(SfxId.Confetti);
            int shown = 0;
            for (int i = 0; i < prizes.Count; i++)
            {
                Prize prize = prizes[i];
                if (prize.card || prize.rect == null) continue;
                StartCoroutine(PopTimes(prize, shown * 0.07f));
                shown++;
            }
            if (shown == 0)
                yield break;
            yield return new WaitForSeconds(shown * 0.07f + 0.5f);
        }

        IEnumerator PopTimes(Prize prize, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            if (prize.rect == null) yield break;
            Text label = EnsureTimes(prize);
            BurstSparkles(prize.rest);
            Image face = prize.rect.GetComponent<Image>();
            Color ink = face != null ? face.color : Color.white;
            float t = 0f;
            const float duration = 0.48f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float punch = 1f + Mathf.Sin(k * Mathf.PI) * 0.24f;
                prize.rect.localScale = new Vector3(punch, punch, 1f);
                if (label != null)
                {
                    float pop = EaseOutBack(Mathf.Clamp01((k - 0.08f) / 0.7f));
                    label.rectTransform.localScale = new Vector3(pop, pop, 1f);
                    label.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(-18f, 8f, pop));
                }
                if (face != null)
                    face.color = Color.Lerp(Color.white, ink, Mathf.Clamp01((k - 0.12f) / 0.45f));
                yield return null;
            }
            prize.rect.localScale = Vector3.one;
            if (label != null)
            {
                label.rectTransform.localScale = Vector3.one;
                label.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            }
            if (face != null)
                face.color = ink;
        }

        Text EnsureTimes(Prize prize)
        {
            if (prize.times != null) return prize.times;
            float h = Mathf.Max(64f, prize.rect.sizeDelta.y);
            int size = Mathf.Clamp(Mathf.RoundToInt(h * 0.42f), 30, 52);
            Text label = UiKit.Label(prize.rect, "Times", "×2", new Vector2(0.5f, 0f),
                new Vector2(0f, 8f), new Vector2(Mathf.Max(88f, h * 1.1f), size + 18f), size,
                new Color(1f, 0.84f, 0.22f, 1f));
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, 8f);
            rect.localScale = Vector3.zero;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            Outline outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.42f, 0.14f, 0.04f, 1f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);
            prize.times = label;
            return label;
        }

        IEnumerator FlashHint(string restore, string message)
        {
            if (hintLabel == null) yield break;
            hintLabel.text = message;
            yield return new WaitForSeconds(1.2f);
            if (hintLabel != null && !matsDoubled)
                hintLabel.text = restore;
        }

        void OnTake()
        {
            Sfx.Play(SfxId.Claim);
            if (canTryOn && reward != null)
            {
                app.Wardrobe.Equip(reward);
                app.Show(ScreenId.DressUp);
                return;
            }
            app.Show(ScreenId.Home);
        }

        void OnContinue()
        {
            Sfx.Play(SfxId.Claim);
            if (app.GmPlay)
            {
                LevelDef done = app.PendingLevel;
                LevelDef next = done != null ? app.LevelAt(done.index + 1) : null;
                if (next == null)
                {
                    app.Notice = done != null ? "GM 第 " + done.index + " 关过了" : "GM 过关";
                    app.Show(ScreenId.Home);
                    return;
                }
                app.CurrentLevel = next;
                app.Show(ScreenId.Game);
                return;
            }
            app.ContinueAfterClear();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (rays.gameObject.activeSelf)
                rays.rectTransform.Rotate(0f, 0f, -18f * dt);

            if (revealed && prizes.Count > 0)
            {
                idle += dt;
                float bob = Mathf.Sin(idle * 2.2f) * 10f;
                for (int i = 0; i < prizes.Count; i++)
                {
                    Prize prize = prizes[i];
                    if (prize.rect == null) continue;
                    prize.rect.anchoredPosition = prize.rest + new Vector2(0f, bob);
                    if (prize.glow == null && prize.sparks == null) continue;
                    float pulse = 0.5f + 0.5f * Mathf.Sin(idle * 2.6f);
                    if (prize.glow != null)
                        prize.glow.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.08f, pulse);
                    if (prize.glowImage != null)
                    {
                        Color glow = GlowGold;
                        glow.a = Mathf.Lerp(0.45f, 0.95f, pulse);
                        prize.glowImage.color = glow;
                    }
                    if (prize.sparks == null) continue;
                    float rx = prize.rect.sizeDelta.x * 0.64f;
                    float ry = prize.rect.sizeDelta.y * 0.56f;
                    for (int s = 0; s < prize.sparks.Length; s++)
                    {
                        float ang = idle * 1.15f + s * Mathf.PI * 0.5f;
                        prize.sparks[s].anchoredPosition = new Vector2(Mathf.Cos(ang) * rx, Mathf.Sin(ang) * ry);
                        float twinkle = 0.5f + 0.5f * Mathf.Sin(idle * 5.2f + s * 1.4f);
                        prize.sparks[s].localScale = Vector3.one * Mathf.Lerp(0.65f, 1.2f, twinkle);
                        if (prize.sparkImages[s] == null) continue;
                        Color ink = Color.white;
                        ink.a = Mathf.Lerp(0.35f, 1f, twinkle);
                        prize.sparkImages[s].color = ink;
                    }
                }
            }

            if (rainLeft > 0f)
            {
                rainLeft -= dt;
                rainClock -= dt;
                if (rainClock <= 0f)
                {
                    rainClock = 0.05f;
                    SpawnRain();
                }
            }
            StepBits(dt);
        }

        // ---------- 礼花 ----------

        void FirePoppers()
        {
            StartCoroutine(Recoil(popperLeft.rectTransform, 1f));
            StartCoroutine(Recoil(popperRight.rectTransform, -1f));
            Rect area = PageRect();
            float y = (0.27f - 0.5f) * area.height + 80f;
            float x = area.width * 0.5f - 130f - 60f;
            for (int i = 0; i < 34; i++)
            {
                Shoot(new Vector2(-x, y), Random.Range(48f, 82f));
                Shoot(new Vector2(x, y), 180f - Random.Range(48f, 82f));
            }
        }

        IEnumerator Recoil(RectTransform rect, float side)
        {
            const float duration = 0.34f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float s = EaseOutBack(Mathf.Min(1f, k * 1.6f)) * (1f + Mathf.Sin(k * Mathf.PI) * 0.18f);
                rect.localScale = new Vector3(s * side, s, 1f);
                yield return null;
            }
            rect.localScale = new Vector3(side, 1f, 1f);
        }

        void Shoot(Vector2 from, float degrees)
        {
            Bit bit = Spawn(from);
            if (bit == null) return;
            float speed = Random.Range(1300f, 2300f);
            float rad = degrees * Mathf.Deg2Rad;
            bit.velocity = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed;
            bit.gravity = 1900f;
            bit.drag = 1.6f;
            bit.life = Random.Range(2.2f, 3f);
            bit.sway = Random.Range(20f, 60f);
        }

        void SpawnRain()
        {
            Rect area = PageRect();
            Vector2 from = new Vector2(Random.Range(-0.48f, 0.48f) * area.width, area.height * 0.5f + 40f);
            Bit bit = Spawn(from);
            if (bit == null) return;
            bit.velocity = new Vector2(Random.Range(-60f, 60f), -Random.Range(260f, 420f));
            bit.gravity = 60f;
            bit.drag = 0f;
            bit.life = 6f;
            bit.sway = Random.Range(40f, 90f);
        }

        void BurstSparkles(Vector2 center)
        {
            if (Db.rewardSparkle == null) return;
            Vector2 at = (Vector2)fxLayer.InverseTransformPoint(gift.TransformPoint(center));
            for (int i = 0; i < 8; i++)
            {
                Bit bit = Spawn(at, Db.rewardSparkle, Random.Range(46f, 72f));
                if (bit == null) continue;
                float rad = (i / 8f) * Mathf.PI * 2f;
                bit.velocity = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Random.Range(500f, 760f);
                bit.gravity = 0f;
                bit.drag = 3.2f;
                bit.spin = 0f;
                bit.life = 0.8f;
                bit.sway = 0f;
            }
        }

        Bit Spawn(Vector2 at, Sprite sprite = null, float size = 0f)
        {
            List<Sprite> pool = Db.rewardConfetti;
            if (sprite == null)
            {
                if (pool == null || pool.Count == 0) return null;
                sprite = pool[Random.Range(0, pool.Count)];
                if (sprite == null) return null;
                size = Random.Range(40f, 64f);
            }
            Bit bit = spare.Count > 0 ? spare.Pop() : null;
            if (bit == null || bit.rect == null)
            {
                Image image = UiKit.Icon(fxLayer, "Bit", sprite, new Vector2(0.5f, 0.5f), at, Vector2.one * size);
                bit = new Bit { rect = image.rectTransform, image = image };
            }
            bit.rect.gameObject.SetActive(true);
            bit.image.sprite = sprite;
            bit.image.color = Color.white;
            bit.rect.sizeDelta = Vector2.one * size;
            bit.rect.anchoredPosition = at;
            bit.rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            bit.rect.localScale = Vector3.one;
            bit.spin = Random.Range(-420f, 420f);
            bit.age = 0f;
            bits.Add(bit);
            return bit;
        }

        void StepBits(float dt)
        {
            float floor = -PageRect().height * 0.5f - 80f;
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                Bit bit = bits[i];
                bit.age += dt;
                bit.velocity.y -= bit.gravity * dt;
                bit.velocity *= Mathf.Exp(-bit.drag * dt);
                Vector2 pos = bit.rect.anchoredPosition + bit.velocity * dt;
                pos.x += Mathf.Sin(bit.age * 3.1f + i) * bit.sway * dt;
                bit.rect.anchoredPosition = pos;
                bit.rect.Rotate(0f, 0f, bit.spin * dt);
                // 翻面感：x 方向缩放来回摆
                float flip = Mathf.Cos(bit.age * 6f + i);
                bit.rect.localScale = new Vector3(bit.sway > 0f ? Mathf.Max(0.25f, Mathf.Abs(flip)) : 1f, 1f, 1f);
                float fade = Mathf.Clamp01((bit.life - bit.age) / 0.5f);
                bit.image.color = new Color(1f, 1f, 1f, fade);
                if (bit.age >= bit.life || pos.y < floor)
                {
                    bit.rect.gameObject.SetActive(false);
                    bits.RemoveAt(i);
                    spare.Push(bit);
                }
            }
        }

        Rect PageRect()
        {
            var page = hud != null ? hud.transform as RectTransform : root;
            return page != null ? page.rect : new Rect(0f, 0f, 1080f, 1920f);
        }

        void ClearBits()
        {
            foreach (Bit bit in bits)
            {
                if (bit.rect == null) continue;
                bit.rect.gameObject.SetActive(false);
                spare.Push(bit);
            }
            bits.Clear();
        }

        static float EaseOutBack(float k)
        {
            k = Mathf.Clamp01(k);
            const float s = 1.55f;
            k -= 1f;
            return k * k * ((s + 1f) * k + s) + 1f;
        }
    }
}
