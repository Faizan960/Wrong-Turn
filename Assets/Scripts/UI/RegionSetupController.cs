using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Leaderboards;
using WrongDirection.Managers;
using WrongDirection.Presentation;

namespace WrongDirection.UI
{
    /// <summary>
    /// Region picker (country → city) and display-name editor, opened from the
    /// Rankings screen. Coarse regions only, chosen from a paged list — no GPS,
    /// no location permission (§6). Region changes and name changes are applied
    /// through LeaderboardManager (server validates the 30-day cooldown and name
    /// sanitization); this only collects input and surfaces the result.
    /// </summary>
    public class RegionSetupController : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private RectTransform content;
        [SerializeField] private Button cancelButton;
        [SerializeField] private float transitionSeconds = 0.16f;

        [Header("Region")]
        [SerializeField] private Button countrySelectButton;
        [SerializeField] private TMP_Text countryValue;
        [SerializeField] private Button citySelectButton;
        [SerializeField] private TMP_Text cityValue;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Picker (paged list)")]
        [SerializeField] private GameObject pickerPanel;
        [SerializeField] private TMP_Text pickerTitle;
        [SerializeField] private Button[] pickerButtons;
        [SerializeField] private TMP_Text[] pickerLabels;
        [SerializeField] private Button pickerCloseButton;
        [SerializeField] private Button pickerPrevButton;
        [SerializeField] private Button pickerNextButton;
        [SerializeField] private TMP_Text pickerPageText;

        [Header("Name editor")]
        [SerializeField] private GameObject nameEditPanel;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button nameConfirmButton;
        [SerializeField] private Button nameCancelButton;
        [SerializeField] private TMP_Text nameStatus;

        private static readonly Color Ok = new Color(0f, 1f, 0.53f, 1f);
        private static readonly Color Err = new Color(1f, 0.23f, 0.27f, 1f);
        private static readonly Color Info = new Color(0.627f, 0.627f, 0.627f, 1f);

        private Vector2 _contentHome;
        private Tween _fade, _slide;
        private Action _onDone;
        private Action _onNameDone;

        private string _pendingCC, _pendingCDisp, _pendingCityId, _pendingCityDisp;

        // Paged picker state
        private readonly List<(string label, Action onPick)> _pickerItems = new List<(string, Action)>();
        private int _pickerPage;

        private void Awake()
        {
            if (cancelButton != null) cancelButton.onClick.AddListener(CloseImmediate);
            if (countrySelectButton != null) countrySelectButton.onClick.AddListener(OpenCountryPicker);
            if (citySelectButton != null) citySelectButton.onClick.AddListener(OpenCityPicker);
            if (confirmButton != null) confirmButton.onClick.AddListener(ConfirmRegion);
            if (pickerCloseButton != null) pickerCloseButton.onClick.AddListener(() => Show(pickerPanel, false));
            if (pickerPrevButton != null) pickerPrevButton.onClick.AddListener(() => Page(-1));
            if (pickerNextButton != null) pickerNextButton.onClick.AddListener(() => Page(1));
            if (nameConfirmButton != null) nameConfirmButton.onClick.AddListener(ConfirmName);
            if (nameCancelButton != null) nameCancelButton.onClick.AddListener(() => Show(nameEditPanel, false));
            if (content != null) _contentHome = content.anchoredPosition;
            SetVisible(false, instant: true);
        }

        // ------------------------------------------------------------------
        public void Open(Action onDone)
        {
            _onDone = onDone;
            var id = LeaderboardManager.Exists ? LeaderboardManager.Instance.Identity : null;
            if (id != null && id.region != null)
            {
                _pendingCC = id.region.countryCode;
                _pendingCDisp = id.region.countryDisplay;
                _pendingCityId = id.region.cityId;
                _pendingCityDisp = id.region.cityDisplay;
            }
            else { _pendingCC = _pendingCDisp = _pendingCityId = _pendingCityDisp = null; }

            Show(pickerPanel, false);
            Show(nameEditPanel, false);
            SetStatus("", Info);
            RefreshLabels();
            SetVisible(true);
        }

        public void CloseImmediate()
        {
            Show(pickerPanel, false);
            Show(nameEditPanel, false);
            SetVisible(false);
        }

        // ------------------------------------------------------------------
        // Country / city pickers
        // ------------------------------------------------------------------
        private void OpenCountryPicker()
        {
            _pickerItems.Clear();
            foreach (var c in RegionCatalog.Countries)
            {
                var cc = c; // capture
                _pickerItems.Add((cc.display, () =>
                {
                    _pendingCC = cc.code; _pendingCDisp = cc.display;
                    _pendingCityId = null; _pendingCityDisp = null; // reset city on country change
                    RefreshLabels();
                    Show(pickerPanel, false);
                }));
            }
            if (pickerTitle != null) pickerTitle.text = "SELECT COUNTRY";
            _pickerPage = 0;
            Show(pickerPanel, true);
            RenderPicker();
        }

        private void OpenCityPicker()
        {
            if (string.IsNullOrEmpty(_pendingCC)) { SetStatus("PICK A COUNTRY FIRST", Err); return; }
            var country = RegionCatalog.FindCountry(_pendingCC);
            if (country == null) return;
            _pickerItems.Clear();
            _pickerItems.Add(("— No city —", () =>
            {
                _pendingCityId = null; _pendingCityDisp = null;
                RefreshLabels(); Show(pickerPanel, false);
            }));
            foreach (var city in country.cities)
            {
                var ct = city;
                _pickerItems.Add((ct.display, () =>
                {
                    _pendingCityId = ct.id; _pendingCityDisp = ct.display;
                    RefreshLabels(); Show(pickerPanel, false);
                }));
            }
            if (pickerTitle != null) pickerTitle.text = "SELECT CITY";
            _pickerPage = 0;
            Show(pickerPanel, true);
            RenderPicker();
        }

        private void Page(int dir)
        {
            int perPage = pickerButtons != null ? pickerButtons.Length : 1;
            int pages = Mathf.Max(1, Mathf.CeilToInt((float)_pickerItems.Count / perPage));
            _pickerPage = Mathf.Clamp(_pickerPage + dir, 0, pages - 1);
            RenderPicker();
        }

        private void RenderPicker()
        {
            int perPage = pickerButtons != null ? pickerButtons.Length : 0;
            int pages = Mathf.Max(1, Mathf.CeilToInt((float)_pickerItems.Count / Mathf.Max(1, perPage)));
            int start = _pickerPage * perPage;
            for (int i = 0; i < perPage; i++)
            {
                int idx = start + i;
                bool used = idx < _pickerItems.Count;
                if (pickerButtons[i] != null) pickerButtons[i].gameObject.SetActive(used);
                if (!used) continue;
                var item = _pickerItems[idx];
                if (pickerLabels != null && i < pickerLabels.Length && pickerLabels[i] != null)
                    pickerLabels[i].text = item.label;
                pickerButtons[i].onClick.RemoveAllListeners();
                pickerButtons[i].onClick.AddListener(() => item.onPick());
            }
            if (pickerPageText != null) pickerPageText.text = (_pickerPage + 1) + " / " + pages;
            if (pickerPrevButton != null) pickerPrevButton.interactable = _pickerPage > 0;
            if (pickerNextButton != null) pickerNextButton.interactable = _pickerPage < pages - 1;
        }

        private void RefreshLabels()
        {
            if (countryValue != null) countryValue.text = string.IsNullOrEmpty(_pendingCDisp) ? "TAP TO SELECT" : _pendingCDisp;
            if (cityValue != null) cityValue.text = string.IsNullOrEmpty(_pendingCityDisp) ? "OPTIONAL" : _pendingCityDisp;
            // CONFIRM stays dead until a country is picked — an enabled-looking
            // button that only ever answers "PICK A COUNTRY FIRST" is a dead end.
            if (confirmButton != null) confirmButton.interactable = !string.IsNullOrEmpty(_pendingCC);
        }

        private void ConfirmRegion()
        {
            if (string.IsNullOrEmpty(_pendingCC)) { SetStatus("PICK A COUNTRY FIRST", Err); return; }
            if (!LeaderboardManager.Exists) { SetStatus("UNAVAILABLE", Err); return; }
            SetStatus("SAVING…", Info);
            // One request per tap: the button re-opens only on a failure the
            // player can act on (the success path closes the panel).
            if (confirmButton != null) confirmButton.interactable = false;
            var region = new RegionInfo
            {
                countryCode = _pendingCC, countryDisplay = _pendingCDisp,
                cityId = _pendingCityId, cityDisplay = _pendingCityDisp,
            };
            LeaderboardManager.Instance.UpdateRegion(region, res =>
            {
                if (res.Ok) { var cb = _onDone; CloseImmediate(); cb?.Invoke(); return; }

                if (confirmButton != null) confirmButton.interactable = true;
                if (res.status == LeaderboardStatus.RegionLocked)
                    SetStatus("REGION CAN CHANGE AGAIN\nAFTER 30 DAYS", Err);
                else if (res.status == LeaderboardStatus.Offline)
                    SetStatus("OFFLINE — TRY AGAIN", Err);
                else SetStatus("COULDN'T SAVE REGION", Err);
            });
        }

        // ------------------------------------------------------------------
        // Name editor
        // ------------------------------------------------------------------
        public void OpenNameEditor(Action onDone)
        {
            _onNameDone = onDone;
            var id = LeaderboardManager.Exists ? LeaderboardManager.Instance.Identity : null;
            if (nameInput != null) nameInput.text = id != null ? id.displayName : "";
            if (nameStatus != null) nameStatus.text = "";
            SetVisible(true);
            Show(nameEditPanel, true);
        }

        private void ConfirmName()
        {
            if (!LeaderboardManager.Exists) return;
            string name = nameInput != null ? nameInput.text : "";
            if (nameStatus != null) { nameStatus.text = "SAVING…"; nameStatus.color = Info; }
            if (nameConfirmButton != null) nameConfirmButton.interactable = false;
            LeaderboardManager.Instance.UpdateDisplayName(name, res =>
            {
                if (nameConfirmButton != null) nameConfirmButton.interactable = true;
                if (res.Ok)
                {
                    var cb = _onNameDone;
                    Show(nameEditPanel, false);
                    SetVisible(false);
                    cb?.Invoke();
                }
                else if (res.status == LeaderboardStatus.InvalidName)
                    SetNameStatus("3–16 CHARACTERS, NO SYMBOLS", Err);
                else if (res.status == LeaderboardStatus.Offline)
                    SetNameStatus("OFFLINE — TRY AGAIN", Err);
                else SetNameStatus("NAME NOT ALLOWED", Err);
            });
        }

        // ------------------------------------------------------------------
        private void SetStatus(string s, Color c) { if (statusText != null) { statusText.text = s; statusText.color = c; } }
        private void SetNameStatus(string s, Color c) { if (nameStatus != null) { nameStatus.text = s; nameStatus.color = c; } }
        private static void Show(GameObject go, bool on) { if (go != null) go.SetActive(on); }

        private void SetVisible(bool visible, bool instant = false)
        {
            if (panel == null) return;
            _fade?.Kill(); _slide?.Kill();
            panel.interactable = visible;
            panel.blocksRaycasts = visible;
            if (instant || AccessibilityPrefs.ReduceMotion)
            {
                panel.alpha = visible ? 1f : 0f;
                if (content != null) content.anchoredPosition = _contentHome;
                return;
            }
            _fade = panel.DOFade(visible ? 1f : 0f, transitionSeconds).SetEase(Ease.OutQuad).SetUpdate(true);
            if (content != null)
            {
                if (visible) content.anchoredPosition = _contentHome + Vector2.down * 30f;
                _slide = content.DOAnchorPos(visible ? _contentHome : _contentHome + Vector2.down * 30f,
                    transitionSeconds).SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }
    }
}
