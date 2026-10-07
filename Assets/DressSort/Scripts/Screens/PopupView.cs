using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 首页侧边入口打开的弹窗：遮罩 + 奶油底板 + 吊牌标题 + 关闭钮。
    /// 外观全在 Resources/Prefabs 里的预制上（「叠叠裙/重建签到排行游戏圈预制」用切图拼出来），
    /// 这里只填数据、换状态切图。
    /// </summary>
    public class PopupView : MonoBehaviour
    {
        public RectTransform board;
        public Button closeButton;
        public Image toastPlate;
        public Text toastLabel;

        protected App app;
        protected Action changed;
        Coroutine toastRoutine;

        public static T Open<T>(string prefabName, RectTransform layer, App app, Action changed) where T : PopupView
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/" + prefabName);
            if (prefab == null)
            {
                Debug.LogError("[叠叠裙] 缺少弹窗预制 Resources/Prefabs/" + prefabName + "，先跑「重建签到排行游戏圈预制」");
                return null;
            }
            GameObject go = Instantiate(prefab, layer);
            go.name = prefabName;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            var view = go.GetComponent<T>();
            view.app = app;
            view.changed = changed;
            if (view.closeButton != null)
                view.closeButton.onClick.AddListener(view.Close);
            if (view.toastPlate != null)
                view.toastPlate.gameObject.SetActive(false);
            view.OnOpen();
            Sfx.Play(SfxId.Popup);
            return view;
        }

        protected virtual void OnOpen()
        {
        }

        public virtual void Close()
        {
            Close(true);
        }

        public void Close(bool sound)
        {
            if (sound)
                Sfx.Play(SfxId.Tap);
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }

        protected void Toast(string message)
        {
            if (toastPlate == null || toastLabel == null) return;
            toastLabel.text = message;
            toastPlate.gameObject.SetActive(true);
            toastPlate.transform.SetAsLastSibling();
            if (!Application.isPlaying) return;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(HideToast());
        }

        IEnumerator HideToast()
        {
            yield return new WaitForSecondsRealtime(1.6f);
            toastPlate.gameObject.SetActive(false);
            toastRoutine = null;
        }

        static readonly Color CapsuleInk = new Color32(0x4A, 0x26, 0x2C, 0xFF);

        /// <summary>胶囊按钮的可点 / 已完成两种样子：浅色平涂胶囊配可可色字，只换底图。</summary>
        protected static void SetCapsule(Button button, Text label, string text, Sprite face, bool ready,
            bool interactable)
        {
            if (button == null) return;
            button.interactable = interactable;
            var image = button.targetGraphic as Image;
            if (image != null && face != null) image.sprite = face;
            if (label == null) return;
            label.text = text;
            label.color = ready ? CapsuleInk : new Color(CapsuleInk.r, CapsuleInk.g, CapsuleInk.b, 0.7f);
            label.fontStyle = FontStyle.Normal;
        }
    }
}
