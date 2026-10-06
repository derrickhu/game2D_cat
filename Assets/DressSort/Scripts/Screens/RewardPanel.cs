using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 过关领奖。外观在 Resources/Prefabs/RewardScreen，这里播开盒动画并填文案。
    /// </summary>
    public class RewardPanel : Panel
    {
        const float DressLift = 200f;

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
        RectTransform fxLayer;
        Image popperLeft;
        Image popperRight;

        Vector2 closedSize, baseSize, lidSize;
        ItemDef reward;
        bool isBlueprint;
        bool revealed;
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
                Debug.LogError("[叠叠裙] 领奖预制没有 RewardHud，先跑「重建领奖预制」");
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
            fxLayer = hud.fxLayer;
            popperLeft = hud.popperLeft;
            popperRight = hud.popperRight;
            closedSize = closed.rectTransform.sizeDelta;
            baseSize = boxBase.rectTransform.sizeDelta;
            lidSize = lid.rectTransform.sizeDelta;
            hud.Wire(OnTap, OnTake);
        }

        public override void OnShow()
        {
            LevelDef level = app.PendingLevel;
            reward = level != null ? level.reward : null;
            isBlueprint = reward == null && level != null && level.blueprint != null;
            if (isBlueprint)
                reward = level.blueprint;
            takeLabel.text = isBlueprint ? "去工坊看看" : "收下并试穿";
            revealed = false;
            ClearBits();
            ResetPose();
            if (reward == null) return;
            StartCoroutine(ShowRoutine());
        }

        public override void OnHide()
        {
            StopAllCoroutines();
            ClearBits();
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
            rays.gameObject.SetActive(false);
            rays.rectTransform.localScale = Vector3.zero;
            plate.localScale = Vector3.zero;
            hintLabel.text = "";
            nameLabel.text = "";
            takeButton.transform.localScale = Vector3.zero;
            takeButton.gameObject.SetActive(false);
            popperLeft.rectTransform.localScale = Vector3.zero;
            popperRight.rectTransform.localScale = Vector3.zero;
        }

        void OnTap()
        {
            if (revealed || reward == null) return;
            ApplyReveal();
        }

        IEnumerator ShowRoutine()
        {
            yield return Drop();
            yield return Wiggle();
            OpenLid();
            yield return RiseDress();
            FinishReveal();
            yield return PopIn(plate, 0.28f);
            yield return PopIn((RectTransform)takeButton.transform, 0.28f);
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

        IEnumerator RiseDress()
        {
            DressUp();
            dress.gameObject.SetActive(true);
            RectTransform rect = dress.rectTransform;
            Vector2 from = new Vector2(0f, baseSize.y * 0.72f);
            Vector2 to = DressRest();
            const float duration = 0.55f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float e = EaseOutBack(t / duration);
                rect.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
                rect.localScale = Vector3.one * Mathf.LerpUnclamped(0.15f, 1f, e);
                yield return null;
            }
            rect.anchoredPosition = to;
            rect.localScale = Vector3.one;
        }

        Vector2 DressRest() => new Vector2(0f, baseSize.y + DressLift);

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
            if (reward == null) return;
            StopAllCoroutines();
            gift.anchoredPosition = Vector2.zero;
            gift.localScale = Vector3.one;
            gift.localRotation = Quaternion.identity;
            closed.gameObject.SetActive(false);
            boxBase.gameObject.SetActive(true);
            lid.gameObject.SetActive(false);
            rays.gameObject.SetActive(true);
            rays.rectTransform.localScale = Vector3.one;
            DressUp();
            dress.gameObject.SetActive(true);
            dress.rectTransform.anchoredPosition = DressRest();
            dress.rectTransform.localScale = Vector3.one;
            if (popperLeft.rectTransform.localScale.x < 0.5f)
                FirePoppers();
            popperLeft.rectTransform.localScale = Vector3.one;
            popperRight.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            FinishReveal();
            plate.localScale = Vector3.one;
            takeButton.gameObject.SetActive(true);
            takeButton.transform.localScale = Vector3.one;
            if (rainLeft <= 0f)
                rainLeft = 2f;
        }

        void FinishReveal()
        {
            if (revealed) return;
            revealed = true;
            if (isBlueprint)
            {
                nameLabel.text = "获得图纸「" + reward.displayName + "」";
                string mats = CraftCatalog.Describe(app.PendingMaterials);
                hintLabel.text = string.IsNullOrEmpty(mats) ? "材料够了就能在工坊制作" : "还获得 " + mats;
            }
            else
            {
                app.Wardrobe.Unlock(reward);
                nameLabel.text = "解锁了「" + reward.displayName + "」";
                hintLabel.text = reward.slot == ItemSlot.Wings ? "翅膀已收进衣柜" : "已经放进衣柜";
            }
        }

        /// <summary>直送的是衣服本身；图纸是一张图纸卡，衣服小图印在卡上。</summary>
        void DressUp()
        {
            Sprite card = Db != null ? Db.craftCard : null;
            bool useCard = isBlueprint && card != null;
            dress.sprite = useCard ? card : reward.ResolveIcon();
            dress.color = Color.white;
            blueprintIcon.gameObject.SetActive(useCard);
            if (useCard)
                blueprintIcon.sprite = reward.ResolveIcon();
        }

        void OnTake()
        {
            if (isBlueprint)
            {
                app.Show(ScreenId.Workshop);
                return;
            }
            if (reward != null)
                app.Wardrobe.Equip(reward);
            app.Show(ScreenId.DressUp);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (rays.gameObject.activeSelf)
                rays.rectTransform.Rotate(0f, 0f, -18f * dt);

            if (revealed && dress.gameObject.activeSelf)
            {
                idle += dt;
                dress.rectTransform.anchoredPosition = DressRest() + new Vector2(0f, Mathf.Sin(idle * 2.2f) * 10f);
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
