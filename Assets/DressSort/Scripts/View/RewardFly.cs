using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 领取奖励的共用飞入：图标从领取处弹出，再飞进屏幕上方（体力飞向体力条）。
    /// 领到东西只播这一下，不再弹文字。
    /// </summary>
    public static class RewardFly
    {
        public struct Bit
        {
            public Sprite sprite;
            public int count;
            public Vector3 world;
            public bool energy;
            public bool aimed;
            public Vector3 aim;
            public float nudge;
        }

        /// <param name="sound">为 false 时只播飞行声。过关页已经播过领取音时用。</param>
        public static void Play(Component near, IList<Bit> bits, GameDatabase db, bool sound = true)
        {
            if (near == null || bits == null || bits.Count == 0) return;
            Canvas canvas = near.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var layer = canvas.transform as RectTransform;
            if (layer == null) return;
            Runner runner = canvas.GetComponent<Runner>();
            if (runner == null) runner = canvas.gameObject.AddComponent<Runner>();
            runner.Begin(layer, canvas, bits, db, sound);
        }

        sealed class Runner : MonoBehaviour
        {
            public void Begin(RectTransform layer, Canvas canvas, IList<Bit> bits, GameDatabase db, bool sound)
            {
                StartCoroutine(Go(layer, canvas, bits, db, sound));
            }

            IEnumerator Go(RectTransform layer, Canvas canvas, IList<Bit> bits, GameDatabase db, bool sound)
            {
                if (sound) Sfx.Play(SfxId.Claim);
                Rect area = layer.rect;
                Vector2 pocket = new Vector2(area.center.x, area.yMax - 140f);
                Vector2 energyAt = pocket;
                RectTransform energyIcon = null;
                GameObject found = GameObject.Find("EnergyIcon");
                if (found != null)
                {
                    energyIcon = found.transform as RectTransform;
                    if (energyIcon != null)
                        energyAt = ToLocal(layer, canvas, energyIcon.position);
                }

                var flight = new Flight { energyIcon = energyIcon, runner = this };
                for (int i = 0; i < bits.Count; i++)
                {
                    Bit bit = bits[i];
                    Vector2 from = ToLocal(layer, canvas, bit.world) + new Vector2(bit.nudge, 0f);
                    Vector2 to = bit.aimed
                        ? ToLocal(layer, canvas, bit.aim)
                        : bit.energy ? energyAt : pocket + new Vector2(bit.nudge * 0.25f, 0f);
                    SpawnBurst(layer, from, db);
                    Image icon = SpawnIcon(layer, from, bit);
                    StartCoroutine(FlyOne(icon, from, to, i * 0.07f, bit.energy, flight));
                }
                yield break;
            }

            sealed class Flight
            {
                public bool whoosh;
                public bool punched;
                public RectTransform energyIcon;
                public Runner runner;
            }

            static Image SpawnIcon(RectTransform layer, Vector2 at, Bit bit)
            {
                Image icon = UiKit.Icon(layer, "RewardFly", bit.sprite, new Vector2(0.5f, 0.5f), at, new Vector2(88f, 88f));
                icon.raycastTarget = false;
                icon.transform.SetAsLastSibling();
                if (icon.sprite == null)
                {
                    icon.sprite = UiKit.Circle;
                    icon.color = new Color32(0xF2, 0xC1, 0x4A, 0xFF);
                }
                icon.rectTransform.localScale = Vector3.one * 0.2f;
                var group = icon.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                if (bit.count > 0)
                {
                    Text count = UiKit.Label(icon.transform, "Count", "×" + bit.count, new Vector2(0.5f, 0f),
                        new Vector2(0f, -6f), new Vector2(100f, 36f), 28, new Color32(0x3A, 0x1C, 0x35, 0xFF));
                    count.raycastTarget = false;
                    CraftView.AddRim(count, Color.white, 2f);
                }
                return icon;
            }

            static void SpawnBurst(RectTransform layer, Vector2 at, GameDatabase db)
            {
                Sprite sprite = db != null ? db.rewardSparkle : null;
                if (sprite == null && db != null) sprite = db.boardBurst;
                if (sprite == null) return;
                Image burst = UiKit.Icon(layer, "RewardBurst", sprite, new Vector2(0.5f, 0.5f), at, new Vector2(140f, 140f));
                burst.raycastTarget = false;
                burst.transform.SetAsLastSibling();
                var group = burst.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                burst.rectTransform.localScale = Vector3.one * 0.4f;
                burst.StartCoroutine(FadeBurst(burst, group));
            }

            static IEnumerator FadeBurst(Image burst, CanvasGroup group)
            {
                float t = 0f;
                while (t < 0.32f && burst != null)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / 0.32f);
                    burst.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.35f, Ease.OutQuad(k));
                    group.alpha = 1f - k;
                    yield return null;
                }
                if (burst != null) UiKit.Discard(burst);
            }

            static IEnumerator FlyOne(Image icon, Vector2 from, Vector2 to, float delay, bool energy, Flight flight)
            {
                if (delay > 0f) yield return new WaitForSeconds(delay);
                if (icon == null) yield break;
                RectTransform rect = icon.rectTransform;
                const float pop = 0.16f;
                for (float t = 0f; t < pop && icon != null; t += Time.deltaTime)
                {
                    rect.localScale = Vector3.one * Mathf.LerpUnclamped(0.2f, 1.08f, Ease.OutBack(t / pop, 2.4f));
                    yield return null;
                }
                if (icon == null) yield break;
                if (flight != null && !flight.whoosh)
                {
                    flight.whoosh = true;
                    Sfx.Play(SfxId.Whoosh);
                }
                Vector2 lift = from + new Vector2((to.x - from.x) * 0.2f, 180f);
                var group = icon.GetComponent<CanvasGroup>();
                const float fly = 0.5f;
                for (float t = 0f; t < fly && icon != null; t += Time.deltaTime)
                {
                    float k = Mathf.Clamp01(t / fly);
                    float e = k * k;
                    Vector2 a = Vector2.LerpUnclamped(from, lift, e);
                    Vector2 b = Vector2.LerpUnclamped(lift, to, e);
                    rect.anchoredPosition = Vector2.LerpUnclamped(a, b, e);
                    rect.localScale = Vector3.one * Mathf.Lerp(1.08f, 0.4f, k);
                    if (group != null && k > 0.7f)
                        group.alpha = 1f - (k - 0.7f) / 0.3f;
                    yield return null;
                }
                if (energy && flight != null && !flight.punched && flight.energyIcon != null && flight.runner != null)
                {
                    flight.punched = true;
                    flight.runner.StartCoroutine(Punch(flight.energyIcon));
                }
                if (icon != null) UiKit.Discard(icon);
            }

            static IEnumerator Punch(RectTransform rect)
            {
                Vector3 rest = rect.localScale;
                float t = 0f;
                while (t < 0.26f && rect != null)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Sin(Mathf.Clamp01(t / 0.26f) * Mathf.PI);
                    rect.localScale = rest * (1f + k * 0.16f);
                    yield return null;
                }
                if (rect != null) rect.localScale = rest;
            }

            static Vector2 ToLocal(RectTransform layer, Canvas canvas, Vector3 world)
            {
                Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, cam, out Vector2 local);
                return local;
            }
        }
    }
}
