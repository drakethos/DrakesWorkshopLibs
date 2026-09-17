using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using Text = UnityEngine.UI.Text;

namespace DrakeModsLibs.UI;

/// <summary>
/// Libs-orchestrated inventory menu host. Feature mods <see cref="Register"/> tabs;
/// missing mods = no tab. Opens the owning mod’s existing wood UI (RenameIt / Lock
/// panels stay feature-owned). When more than one tab is available, shows a
/// Craft|Upgrade-style top-right tab strip to switch. Config overrides via
/// <see cref="DrakeModsLibs.Integration.DrakeIntegrationConfig"/>.
/// </summary>
public static class DrakeTabHost
{
    const float TabWidth = 72f;
    const float TabHeight = 28f;
    const float TabGap = 4f;
    /// <summary>Strip sits above centered wood menus (≈320 tall).</summary>
    const float StripOffsetY = 178f;
    const float StripOffsetX = 8f;

    static readonly List<DrakeTabRegistration> Registrations = new List<DrakeTabRegistration>();
    static GameObject? _stripRoot;
    static RectTransform? _tabStrip;
    static readonly List<Button> _tabButtons = new List<Button>();
    static ItemDrop.ItemData? _currentItem;
    static string? _activeTabId;
    static Action? _onClosed;
    static bool _opening;

    public static bool IsOpen =>
        _currentItem != null && (!string.IsNullOrEmpty(_activeTabId) || (_stripRoot && _stripRoot.activeSelf));

    public static string? ActiveTabId => _activeTabId;
    public static ItemDrop.ItemData? CurrentItem => _currentItem;

    public static void Register(
        string id,
        string title,
        int priority,
        Func<ItemDrop.ItemData, bool> isAvailable,
        Action<DrakeTabPageContext> show,
        Func<ItemDrop.ItemData, bool>? claimDefault = null,
        Action? hide = null,
        Func<string>? getHintPhrase = null,
        Func<string>? getTitle = null)
    {
        if (string.IsNullOrEmpty(id))
            return;

        for (var i = 0; i < Registrations.Count; i++)
        {
            if (Registrations[i].Id == id)
            {
                Registrations[i] = new DrakeTabRegistration(
                    id, title, priority, isAvailable, show, claimDefault, hide, getHintPhrase, getTitle);
                ApplyConfigOverride(Registrations[i]);
                return;
            }
        }

        var reg = new DrakeTabRegistration(
            id, title, priority, isAvailable, show, claimDefault, hide, getHintPhrase, getTitle);
        ApplyConfigOverride(reg);
        Registrations.Add(reg);
    }

    /// <summary>Registered tabs that <see cref="DrakeTabRegistration.IsAvailable"/> accepts for this item (usable now).</summary>
    public static List<DrakeTabRegistration> GetUsableTabs(ItemDrop.ItemData? item)
    {
        if (item == null)
            return new List<DrakeTabRegistration>();
        return GetAvailable(item).ToList();
    }

    public static int GetUsableCount(ItemDrop.ItemData? item) => GetUsableTabs(item).Count;

    /// <summary>Open host when any usable tab exists. Same as <see cref="OpenForItem"/>.</summary>
    public static bool TryOpenInventoryContext(ItemDrop.ItemData? item, string? preferredTabId = null, Action? onClosed = null) =>
        OpenForItem(item, preferredTabId, onClosed);

    public static void SetPriority(string id, int priority)
    {
        var reg = Find(id);
        if (reg == null)
            return;
        reg.Priority = priority;
        ApplyConfigOverride(reg);
    }

    public static void RaisePriority(string id, int delta) =>
        SetPriority(id, (Find(id)?.Priority ?? 0) + delta);

    public static bool TryGetPriority(string id, out int priority)
    {
        var reg = Find(id);
        if (reg == null)
        {
            priority = 0;
            return false;
        }

        priority = reg.Priority;
        return true;
    }

    /// <summary>
    /// Opens the best available tab for <paramref name="item"/>. Returns false when no
    /// registered mod claims the item (caller should fall through to vanilla).
    /// </summary>
    public static bool OpenForItem(ItemDrop.ItemData? item, string? preferredTabId = null, Action? onClosed = null)
    {
        if (item == null || GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return false;

        var available = GetAvailable(item).ToList();
        if (available.Count == 0)
            return false;

        _onClosed = onClosed;
        _currentItem = item;

        if (available.Count > 1)
        {
            EnsureTabStrip();
            if (!_stripRoot || _tabStrip == null)
                return false;
            RebuildTabs(available);
            _stripRoot.SetActive(true);
            _stripRoot.transform.SetAsLastSibling();
        }
        else
        {
            HideStrip();
        }

        var defaultId = ResolveDefaultTabId(item, available, preferredTabId);
        SwitchTo(defaultId, available, hidePrevious: false);
        return true;
    }

    /// <summary>
    /// Feature menu closed itself (Cancel / OK). Clears host state without re-hiding the feature.
    /// </summary>
    public static void NotifyFeatureClosed(string? tabId = null)
    {
        if (_opening)
            return;
        if (!string.IsNullOrEmpty(tabId) && _activeTabId != null && _activeTabId != tabId)
            return;

        _currentItem = null;
        _activeTabId = null;
        HideStrip();
        var closed = _onClosed;
        _onClosed = null;
        closed?.Invoke();
    }

    public static void Close()
    {
        if (_opening)
            return;

        var active = Find(_activeTabId ?? "");
        _activeTabId = null;
        _currentItem = null;
        HideStrip();

        try
        {
            active?.Hide?.Invoke();
        }
        catch (Exception)
        {
            /* consumer logs */
        }

        var closed = _onClosed;
        _onClosed = null;
        closed?.Invoke();
    }

    static void SwitchTo(string tabId, List<DrakeTabRegistration> available, bool hidePrevious = true)
    {
        var reg = available.FirstOrDefault(r => r.Id == tabId) ?? available[0];
        var previousId = _activeTabId;

        if (hidePrevious && !string.IsNullOrEmpty(previousId) && previousId != reg.Id)
        {
            try
            {
                Find(previousId!)?.Hide?.Invoke();
            }
            catch (Exception)
            {
                /* consumer logs */
            }
        }

        _activeTabId = reg.Id;
        HighlightTabs(available);

        _opening = true;
        try
        {
            // ContentRoot is the strip root when multi-tab; null-safe placeholder for single-tab.
            var root = _tabStrip != null ? (Transform)_tabStrip : GUIManager.CustomGUIFront.transform;
            reg.Show(new DrakeTabPageContext(_currentItem!, reg.Id, root));
        }
        catch (Exception)
        {
            /* consumer logs */
        }
        finally
        {
            _opening = false;
        }

        if (_stripRoot && _stripRoot.activeSelf)
            _stripRoot.transform.SetAsLastSibling();
    }

    static string ResolveDefaultTabId(
        ItemDrop.ItemData item,
        List<DrakeTabRegistration> available,
        string? preferredTabId)
    {
        var forced = DrakeModsLibs.Integration.DrakeIntegrationConfig.ForceDefaultTabId;
        if (!string.IsNullOrWhiteSpace(forced)
            && available.Any(r => r.Id == forced))
            return forced!;

        if (!string.IsNullOrEmpty(preferredTabId)
            && available.Any(r => r.Id == preferredTabId))
            return preferredTabId!;

        if (!DrakeModsLibs.Integration.DrakeIntegrationConfig.DisableClaimDefault)
        {
            var claim = available
                .Where(r => SafeClaim(r, item))
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (claim != null)
                return claim.Id;
        }

        return available
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .First()
            .Id;
    }

    static bool SafeClaim(DrakeTabRegistration reg, ItemDrop.ItemData item)
    {
        try
        {
            return reg.ClaimDefault(item);
        }
        catch
        {
            return false;
        }
    }

    static IEnumerable<DrakeTabRegistration> GetAvailable(ItemDrop.ItemData item)
    {
        foreach (var reg in Registrations)
        {
            bool ok;
            try
            {
                ok = reg.IsAvailable(item);
            }
            catch
            {
                ok = false;
            }

            if (ok)
                yield return reg;
        }
    }

    static DrakeTabRegistration? Find(string id) =>
        string.IsNullOrEmpty(id) ? null : Registrations.FirstOrDefault(r => r.Id == id);

    static void ApplyConfigOverride(DrakeTabRegistration reg)
    {
        if (DrakeModsLibs.Integration.DrakeIntegrationConfig.TryGetPriorityOverride(reg.Id, out var p))
            reg.Priority = p;
    }

    static void EnsureTabStrip()
    {
        if (_stripRoot || GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        _stripRoot = new GameObject("drake_tab_strip_root", typeof(RectTransform));
        _stripRoot.transform.SetParent(GUIManager.CustomGUIFront.transform, false);
        var rootRt = _stripRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = new Vector2(StripOffsetX, StripOffsetY);
        rootRt.sizeDelta = new Vector2(280f, TabHeight + 8f);

        var stripGo = new GameObject("tabs", typeof(RectTransform));
        stripGo.transform.SetParent(_stripRoot.transform, false);
        _tabStrip = stripGo.GetComponent<RectTransform>();
        _tabStrip.anchorMin = new Vector2(1f, 0.5f);
        _tabStrip.anchorMax = new Vector2(1f, 0.5f);
        _tabStrip.pivot = new Vector2(1f, 0.5f);
        _tabStrip.anchoredPosition = Vector2.zero;
        _tabStrip.sizeDelta = new Vector2(280f, TabHeight + 4f);

        var esc = _stripRoot.AddComponent<EscapeCloser>();
        esc.Bind(Close);

        _stripRoot.SetActive(false);
    }

    static void HideStrip()
    {
        if (_stripRoot)
            _stripRoot.SetActive(false);
    }

    static void RebuildTabs(List<DrakeTabRegistration> available)
    {
        if (_tabStrip == null || GUIManager.Instance == null)
            return;

        foreach (var btn in _tabButtons)
        {
            if (btn)
                UnityEngine.Object.Destroy(btn.gameObject);
        }

        _tabButtons.Clear();

        var ordered = available
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            var reg = ordered[i];
            var fromRight = i;
            var x = -(fromRight * (TabWidth + TabGap) + TabWidth * 0.5f);
            var btn = DrakeButtonSfx.SoftenButton(GUIManager.Instance.CreateButton(
                text: reg.Title,
                parent: _tabStrip,
                anchorMin: new Vector2(1f, 0.5f),
                anchorMax: new Vector2(1f, 0.5f),
                position: new Vector2(x, 0f),
                width: TabWidth,
                height: TabHeight));
            var capturedId = reg.Id;
            btn.onClick.AddListener(() =>
            {
                if (_currentItem == null)
                    return;
                var list = GetAvailable(_currentItem).ToList();
                if (list.Count == 0)
                    return;
                if (capturedId == _activeTabId)
                    return;
                SwitchTo(capturedId, list, hidePrevious: true);
            });
            _tabButtons.Add(btn);
        }
    }

    static void HighlightTabs(List<DrakeTabRegistration> available)
    {
        if (_tabButtons.Count == 0 || GUIManager.Instance == null)
            return;

        var ordered = available
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToList();

        for (var i = 0; i < _tabButtons.Count && i < ordered.Count; i++)
        {
            var btn = _tabButtons[i];
            if (!btn)
                continue;
            var label = btn.GetComponentInChildren<Text>(true);
            if (!label)
                continue;
            var active = ordered[i].Id == _activeTabId;
            label.color = active
                ? GUIManager.Instance.ValheimOrange
                : Color.white;
        }
    }

    sealed class EscapeCloser : MonoBehaviour
    {
        Action? _close;

        public void Bind(Action close) => _close = close;

        void Update()
        {
            if (!gameObject.activeInHierarchy || _close == null)
                return;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                _close.Invoke();
        }
    }
}
