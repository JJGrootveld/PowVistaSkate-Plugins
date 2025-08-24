using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

namespace InventoryCounter
{
    public class InventoryCounterManager : MonoBehaviour
    {
        public static InventoryCounterManager? Instance;

        private Canvas? _overlayCanvas;
        private RectTransform? _overlayRect;
        private GameObject? _badgeRoot;
        private RectTransform? _badgeRect;
        private TextMeshProUGUI? _badgeText;

        private RectTransform? _anchorTarget; // backpack icon or fallback panel
        private bool _inventoryOpen = false;
        private int _lastCount = -1;
        // Debug toggles (disabled for production). Set to true for troubleshooting.
        private bool enableDebugLogs = false;

        // Layout controls: percentages of screen height for size and offset
        [Header("Layout (relative to screen height)")]
        [Range(0.01f, 0.20f)] public float badgeHeightPercent = 0.035f; // 3.5% of screen height
        [Range(1.0f, 3.0f)] public float badgeWidthToHeight = 1.6f;      // width = height * ratio
        [Range(0.0f, 0.10f)] public float offsetXPercent = 0.018f;       // x offset from anchor
        [Range(0.0f, 0.10f)] public float offsetYPercent = 0.014f;       // y offset from anchor

        // Reflection caches
        private readonly Dictionary<Type, List<MemberInfo>> _collectionFieldCache = new();
        private readonly Dictionary<Type, List<MemberInfo>> _quantityFieldCache = new();
        private Type? _inGameUiType;
        private object? _inGameUiInstance;
        private MemberInfo? _inGameUiItemsMember;
        private readonly Dictionary<string, Type?> _typeLookupCache = new();

        public void Initialize()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CreateOverlayAndBadge();

            // Start light polling loops
            InvokeRepeating(nameof(DetectInventoryAndAnchor), 0.25f, 0.25f);
            InvokeRepeating(nameof(RefreshItemCount), 0.25f, 0.25f);
        }

        private void CreateOverlayAndBadge()
        {
            var canvasGo = new GameObject("InventoryCounterCanvas");
            _overlayCanvas = canvasGo.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.overrideSorting = true;
            _overlayCanvas.sortingOrder = 32760;
            canvasGo.AddComponent<CanvasScaler>();

            _overlayRect = canvasGo.GetComponent<RectTransform>();

            _badgeRoot = new GameObject("InventoryCounterBadge");
            _badgeRoot.transform.SetParent(canvasGo.transform, false);

            var bg = _badgeRoot.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.85f);
            bg.raycastTarget = false;
            bg.type = Image.Type.Sliced;

            _badgeRect = _badgeRoot.GetComponent<RectTransform>();
            _badgeRect.pivot = new Vector2(0.5f, 0.5f);
            _badgeRect.sizeDelta = new Vector2(55, 28); // initial; overridden per-frame based on screen height

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(_badgeRoot.transform, false);
            _badgeText = textGo.AddComponent<TextMeshProUGUI>();
            _badgeText.alignment = TextAlignmentOptions.Center;
            _badgeText.fontSize = 18;
            _badgeText.color = Color.white;
            _badgeText.raycastTarget = false;
            if (TMPro.TMP_Settings.defaultFontAsset != null && _badgeText.font == null)
            {
                _badgeText.font = TMPro.TMP_Settings.defaultFontAsset;
            }
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            _badgeRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            CancelInvoke();
            if (_overlayCanvas != null)
            {
                Destroy(_overlayCanvas.gameObject);
            }
        }

        private void DetectInventoryAndAnchor()
        {
            bool isOpen = DetectInventoryOpen(out RectTransform? inventoryRoot);

            if (!isOpen)
            {
                if (_inventoryOpen)
                {
                    _inventoryOpen = false;
                    SetBadgeVisible(false);
                    _anchorTarget = null;
                }
                return;
            }

            // Inventory is open
            if (!_inventoryOpen)
            {
                _inventoryOpen = true;
                SetBadgeVisible(true);
                _lastCount = -1; // force refresh
                if (enableDebugLogs)
                {
                    Debug.Log("[InventoryCounter] Inventory detected OPEN");
                }
            }

            // Find anchor target if missing or invalid
            if (_anchorTarget == null || !_anchorTarget.gameObject.activeInHierarchy)
            {
                _anchorTarget = FindBackpackIconRect() ?? inventoryRoot;
            }

            // Update badge position
            if (_anchorTarget != null && _overlayRect != null && _badgeRect != null)
            {
                // Convert anchor rect center to screen, then to overlay local
                Vector3 worldPos = _anchorTarget.TransformPoint(new Vector3(_anchorTarget.rect.width * 0.5f, _anchorTarget.rect.height * 0.5f, 0));
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldPos);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlayRect, screenPoint, null, out var localPoint);
                // Resolution-independent size and offset based on screen height
                float screenH = _overlayRect.rect.height;
                float badgeH = Mathf.Max(16f, screenH * badgeHeightPercent);
                float badgeW = badgeH * badgeWidthToHeight;
                _badgeRect.sizeDelta = new Vector2(badgeW, badgeH);
                if (_badgeText != null)
                {
                    _badgeText.fontSize = Mathf.Clamp(badgeH * 0.6f, 12f, 72f);
                }
                var pos = localPoint + new Vector2(screenH * offsetXPercent, screenH * offsetYPercent);
                // Clamp inside overlay rect
                var half = _overlayRect.rect.size * 0.5f;
                pos.x = Mathf.Clamp(pos.x, -half.x + _badgeRect.rect.width, half.x - _badgeRect.rect.width);
                pos.y = Mathf.Clamp(pos.y, -half.y + _badgeRect.rect.height, half.y - _badgeRect.rect.height);
                _badgeRect.anchoredPosition = pos;
            }
        }

        private void SetBadgeVisible(bool visible)
        {
            if (_badgeRoot != null && _badgeRoot.activeSelf != visible)
            {
                _badgeRoot.SetActive(visible);
            }
        }

        private bool DetectInventoryOpen(out RectTransform? inventoryRoot)
        {
            inventoryRoot = null;

            // Heuristic 1: any active InventoryUIItem indicates inventory list
            try
            {
                var items = Object.FindObjectsOfType<MonoBehaviour>();
                foreach (var mb in items)
                {
                    if (mb == null) continue;
                    var t = mb.GetType();
                    if (t.Name.IndexOf("InventoryUIItem", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (mb.gameObject.activeInHierarchy)
                        {
                            inventoryRoot = mb.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>()
                                            ?? mb.GetComponentInParent<RectTransform>();
                            return true;
                        }
                    }
                }
            }
            catch { }

            // Heuristic 2: any active component with name containing "Inventory" under a Canvas
            var behaviours = Object.FindObjectsOfType<MonoBehaviour>();
            foreach (var b in behaviours)
            {
                if (b == null) continue;
                if (!b.gameObject.activeInHierarchy) continue;
                var name = b.GetType().Name;
                if (name.IndexOf("Inventory", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    inventoryRoot = b.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>()
                                    ?? b.GetComponentInParent<RectTransform>();
                    if (inventoryRoot != null) return true;
                }
            }

            return false;
        }

        private RectTransform? FindBackpackIconRect()
        {
            // Search for likely backpack icon names or sprites
            var images = Object.FindObjectsOfType<Image>();
            foreach (var img in images)
            {
                if (img == null) continue;
                var go = img.gameObject;
                if (!go.activeInHierarchy) continue;
                // Skip our own overlay elements
                if (_overlayCanvas != null && go.transform.IsChildOf(_overlayCanvas.transform)) continue;
                if (go.name != null && go.name.IndexOf("InventoryCounter", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                string objName = go.name ?? string.Empty;
                string spriteName = img.sprite != null ? img.sprite.name : string.Empty;

                if (NameLooksLikeBackpack(objName) || NameLooksLikeBackpack(spriteName))
                {
                    return img.rectTransform;
                }
            }
            // Fallback: any Button with similar name
            var buttons = Object.FindObjectsOfType<Button>();
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                var go = btn.gameObject;
                if (_overlayCanvas != null && go.transform.IsChildOf(_overlayCanvas.transform)) continue;
                string name = go.name ?? string.Empty;
                if (NameLooksLikeBackpack(name)) return btn.GetComponent<RectTransform>();
            }
            return null;
        }

        private static bool NameLooksLikeBackpack(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            name = name.ToLowerInvariant();
            return name.Contains("backpack") || name.Contains("bag");
        }

        private void RefreshItemCount()
        {
            if (!_inventoryOpen || _badgeText == null)
            {
                return;
            }

            int countModel = TryGetItemCountFromModel();
            int totalSlots;
            int countUi = TryGetItemCountFromUi(out totalSlots);

            int count = Mathf.Max(countModel < 0 ? 0 : countModel, countUi < 0 ? 0 : countUi);

            if (count != _lastCount)
            {
                _lastCount = count;
                _badgeText.text = totalSlots > 0 ? $"{count}/{totalSlots}" : count.ToString();
                if (enableDebugLogs)
                {
                    Debug.Log($"[InventoryCounter] Count updated: {count}/{totalSlots} (model={countModel}, ui={countUi})");
                }
            }
        }

        private int TryGetItemCountFromModel()
        {
            try
            {
                // Search for a likely player or inventory controller
                var behaviours = Object.FindObjectsOfType<MonoBehaviour>();
                foreach (var b in behaviours)
                {
                    if (b == null) continue;
                    var t = b.GetType();
                    string tn = t.Name;
                    if (tn.IndexOf("Player", StringComparison.OrdinalIgnoreCase) < 0 &&
                        tn.IndexOf("Inventory", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    // Probe collection-like members
                    foreach (var member in GetCollectionMembers(t))
                    {
                        object? value = null;
                        try
                        {
                            value = member is FieldInfo fi ? fi.GetValue(b) : (member as PropertyInfo)?.GetValue(b);
                        }
                        catch { continue; }
                        if (value == null) continue;

                        // Count based on known patterns
                        int items = CountItemsInUnknownCollection(value);
                        if (items >= 0) return items;
                    }

                    // Special-case: InGameUI.items mirrors the item definition list; some games also expose a live inventory list under InGameUI
                    if (tn.Equals("InGameUI", StringComparison.OrdinalIgnoreCase))
                    {
                        // Try common member names on InGameUI-like classes
                        object? inv = GetMemberValueByNames(b, new[] { "inventory", "playerInventory", "bag", "itemsInInventory", "currentInventory", "slots" });
                        if (inv != null)
                        {
                            int items = CountItemsInUnknownCollection(inv);
                            if (items >= 0) return items;
                        }
                    }
                }
            }
            catch { }

            return -1;
        }

        private List<MemberInfo> GetCollectionMembers(Type type)
        {
            if (_collectionFieldCache.TryGetValue(type, out var list)) return list;
            list = new List<MemberInfo>();
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var f in type.GetFields(F))
            {
                if (LooksLikeCollectionName(f.Name)) list.Add(f);
            }
            foreach (var p in type.GetProperties(F))
            {
                if (!p.CanRead) continue;
                if (LooksLikeCollectionName(p.Name)) list.Add(p);
            }
            _collectionFieldCache[type] = list;
            return list;
        }

        private static bool LooksLikeCollectionName(string name)
        {
            name = name.ToLowerInvariant();
            return name.Contains("inventory") || name.Contains("items") || name.Contains("bag") || name.Contains("slots") || name.Contains("slotlist");
        }

        private int CountItemsInUnknownCollection(object value)
        {
            try
            {
                if (value is System.Collections.IEnumerable enumerable)
                {
                    int count = 0;
                    foreach (var element in enumerable)
                    {
                        if (element == null) continue;
                        int qty = GetQuantityFromElement(element);
                        // If we cannot resolve quantity but element appears to be a slot object with valid id, assume 1
                        if (qty <= 0)
                        {
                            var slotId = TryGetIntFieldOrProperty(element, new[] { "slotValue", "slotIndex", "index", "itemIndex", "id" });
                            if (slotId.HasValue && slotId.Value >= 0) qty = 1;
                        }
                        if (qty > 0) count++;
                    }
                    return count;
                }
            }
            catch { }
            return -1;
        }

        private int GetQuantityFromElement(object element)
        {
            var t = element.GetType();
            foreach (var m in GetQuantityMembers(t))
            {
                try
                {
                    object? v = m is FieldInfo fi ? fi.GetValue(element) : (m as PropertyInfo)?.GetValue(element);
                    if (v is int i) return i;
                    if (v is float f) return (int)Mathf.RoundToInt(f);
                }
                catch { }
            }
            // If element has an icon or sprite, assume present
            try
            {
                var go = element as GameObject;
                if (go != null)
                {
                    var img = go.GetComponentInChildren<Image>();
                    if (img != null && img.sprite != null && img.color.a > 0.01f) return 1;
                }
            }
            catch { }
            return 0;
        }

        private static object? GetMemberValueByNames(object obj, string[] names)
        {
            var t = obj.GetType();
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var n in names)
            {
                var f = t.GetField(n, F);
                if (f != null)
                {
                    try { return f.GetValue(obj); } catch { }
                }
                var p = t.GetProperty(n, F);
                if (p != null && p.CanRead)
                {
                    try { return p.GetValue(obj); } catch { }
                }
            }
            return null;
        }

        private List<MemberInfo> GetQuantityMembers(Type t)
        {
            if (_quantityFieldCache.TryGetValue(t, out var list)) return list;
            list = new List<MemberInfo>();
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            string[] names = { "amount", "count", "quantity", "qty", "stack", "stackCount" };
            foreach (var n in names)
            {
                var f = t.GetField(n, F);
                if (f != null) list.Add(f);
                var p = t.GetProperty(n, F);
                if (p != null && p.CanRead) list.Add(p);
            }
            _quantityFieldCache[t] = list;
            return list;
        }

        private int TryGetItemCountFromUi(out int totalCandidates)
        {
            try
            {
                // Look for UI entries named InventoryUIItem (or similar) and infer occupancy
                var behaviours = Object.FindObjectsOfType<MonoBehaviour>();
                int occupied = 0;
                totalCandidates = 0;
                foreach (var b in behaviours)
                {
                    if (b == null) continue;
                    if (!b.gameObject.activeInHierarchy) continue;
                    string tn = b.GetType().Name;
                    if (tn.IndexOf("InventoryUIItem", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        totalCandidates++;
                        if (IsUiItemOccupied(b))
                        {
                            occupied++;
                        }
                    }
                }
                if (enableDebugLogs)
                {
                    Debug.Log($"[InventoryCounter] UI probe: candidates={totalCandidates}, occupied={occupied}");
                }
                return occupied;
            }
            catch { totalCandidates = -1; }
            return -1;
        }

        private int InferUiItemQuantity(MonoBehaviour uiItem)
        {
            // First try common fields
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            string[] qtyNames = { "amount", "count", "quantity", "qty", "stack", "stackCount" };
            foreach (var n in qtyNames)
            {
                var f = uiItem.GetType().GetField(n, F);
                if (f != null)
                {
                    try { var v = f.GetValue(uiItem); if (v is int i) return i; } catch { }
                }
                var p = uiItem.GetType().GetProperty(n, F);
                if (p != null && p.CanRead)
                {
                    try { var v = p.GetValue(uiItem); if (v is int i) return i; } catch { }
                }
            }

            // Check for boolean presence
            var hasItemField = uiItem.GetType().GetField("hasItem", F);
            if (hasItemField != null)
            {
                try { var v = hasItemField.GetValue(uiItem); if (v is bool b && b) return 1; } catch { }
            }

            // If slot-like id is valid (>=0), treat as present
            string[] idNames = { "slotValue", "itemID", "id", "index" };
            foreach (var n in idNames)
            {
                var f = uiItem.GetType().GetField(n, F);
                if (f != null)
                {
                    try { var v = f.GetValue(uiItem); if (v is int i && i >= 0) return 1; } catch { }
                }
                var p = uiItem.GetType().GetProperty(n, F);
                if (p != null && p.CanRead)
                {
                    try { var v = p.GetValue(uiItem); if (v is int i && i >= 0) return 1; } catch { }
                }
            }

            // Fallback: icon presence
            var img = uiItem.GetComponentInChildren<Image>();
            if (img != null && img.sprite != null && img.color.a > 0.01f) return 1;
            return 0;
        }

        private bool IsUiItemOccupied(MonoBehaviour uiItem)
        {
            // 0) If we can resolve slotValue and InGameUI.items, use authoritative item type/name
            int? slotIdx = TryGetIntFieldOrProperty(uiItem, new[] { "slotValue", "slotIndex", "index", "itemIndex" });
            if (slotIdx.HasValue && slotIdx.Value >= 0)
            {
                var state = TryResolveInGameUiItem(slotIdx.Value);
                if (state.HasValue)
                {
                    return state.Value;
                }
            }

            // 1) Explicit booleans
            bool? hasItem = TryGetBoolFieldOrProperty(uiItem, new[] { "hasItem", "occupied", "isOccupied", "isFilled" });
            if (hasItem.HasValue && hasItem.Value) return true;
            bool? isEmpty = TryGetBoolFieldOrProperty(uiItem, new[] { "isEmpty", "empty" });
            if (isEmpty.HasValue) return !isEmpty.Value;

            // 2) Quantities
            int qty = InferUiItemQuantity(uiItem);
            if (qty > 0) return true;

            // 3) Slot sentinel values: many UIs use -1 for empty
            int? slot = TryGetIntFieldOrProperty(uiItem, new[] { "slotValue", "slotIndex", "index", "itemIndex" });
            if (slot.HasValue && slot.Value < 0) return false;

            // 4) Dedicated icon field on the component (Image)
            var iconImage = GetImageFieldOrProperty(uiItem, new[] { "icon", "itemIcon", "image", "iconImage" });
            if (iconImage != null && iconImage.sprite != null && iconImage.color.a > 0.01f) return true;

            // 5) Child icon graphic
            var img = uiItem.GetComponentInChildren<Image>();
            if (img != null && img.sprite != null && img.color.a > 0.01f) return true;

            // 6) No positive signal → treat as empty (prevents counting all slots)
            return false;
        }

        // Returns true if slot index corresponds to a non-empty item according to InGameUI.items; null if unavailable
        private bool? TryResolveInGameUiItem(int slotIndex)
        {
            try
            {
                if (_inGameUiType == null)
                {
                    _inGameUiType = FindTypeByName("InGameUI");
                }
                if (_inGameUiType == null) return null;

                if (_inGameUiInstance == null)
                {
                    const BindingFlags SF = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                    var instField = _inGameUiType.GetField("instance", SF) ?? _inGameUiType.GetField("realInstance", SF);
                    object? candidate = null;
                    if (instField != null)
                    {
                        candidate = instField.GetValue(null);
                    }
                    else
                    {
                        var instProp = _inGameUiType.GetProperty("instance", SF) ?? _inGameUiType.GetProperty("realInstance", SF);
                        if (instProp != null && instProp.CanRead) candidate = instProp.GetValue(null);
                    }
                    _inGameUiInstance = candidate;
                }
                if (_inGameUiInstance == null) return null;

                if (_inGameUiItemsMember == null)
                {
                    const BindingFlags IF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    _inGameUiItemsMember = _inGameUiType.GetField("items", IF) as MemberInfo ?? _inGameUiType.GetProperty("items", IF);
                }
                if (_inGameUiItemsMember == null) return null;

                object? itemsObj = _inGameUiItemsMember is FieldInfo fi ? fi.GetValue(_inGameUiInstance) : ( _inGameUiItemsMember as PropertyInfo)?.GetValue(_inGameUiInstance);
                if (itemsObj == null) return null;

                var list = itemsObj as System.Collections.IList;
                if (list == null) return null;
                if (slotIndex < 0 || slotIndex >= list.Count) return false;

                object? item = list[slotIndex];
                if (item == null) return false;

                // Probe type/name on the item
                var itemTypeVal = GetMemberValueByNames(item, new[] { "type", "itemType", "elementType" });
                if (itemTypeVal != null)
                {
                    string s = itemTypeVal.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(s) && !s.Equals("None", StringComparison.OrdinalIgnoreCase)) return true;
                }
                var itemNameVal = GetMemberValueByNames(item, new[] { "humanFriendlyName", "displayName", "name" });
                if (itemNameVal is string nameStr)
                {
                    if (!string.IsNullOrWhiteSpace(nameStr) && !string.Equals(nameStr, "None", StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }
            catch
            {
                return null;
            }
        }

        private Type? FindTypeByName(string typeName)
        {
            if (_typeLookupCache.TryGetValue(typeName, out var cached)) return cached;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                try
                {
                    var t = asm.GetType(typeName, throwOnError: false, ignoreCase: false);
                    if (t != null)
                    {
                        _typeLookupCache[typeName] = t;
                        return t;
                    }
                }
                catch { }
            }
            _typeLookupCache[typeName] = null;
            return null;
        }

        // Debug helpers removed for production

        private static int? TryGetIntFieldOrProperty(object obj, string[] candidateNames)
        {
            var t = obj.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var name in candidateNames)
            {
                var field = t.GetField(name, flags);
                if (field != null && field.FieldType == typeof(int))
                {
                    try { return (int)field.GetValue(obj); } catch { }
                }
                var prop = t.GetProperty(name, flags);
                if (prop != null && prop.PropertyType == typeof(int) && prop.CanRead)
                {
                    try { return (int)prop.GetValue(obj); } catch { }
                }
            }
            return null;
        }

        private static bool? TryGetBoolFieldOrProperty(object obj, string[] names)
        {
            var t = obj.GetType();
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var n in names)
            {
                var f = t.GetField(n, F);
                if (f != null && f.FieldType == typeof(bool))
                {
                    try { return (bool)f.GetValue(obj); } catch { }
                }
                var p = t.GetProperty(n, F);
                if (p != null && p.PropertyType == typeof(bool) && p.CanRead)
                {
                    try { return (bool)p.GetValue(obj); } catch { }
                }
            }
            return null;
        }

        private static Image? GetImageFieldOrProperty(object obj, string[] names)
        {
            var t = obj.GetType();
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var n in names)
            {
                var f = t.GetField(n, F);
                if (f != null)
                {
                    try { var v = f.GetValue(obj); if (v is Image img) return img; } catch { }
                }
                var p = t.GetProperty(n, F);
                if (p != null && p.CanRead)
                {
                    try { var v = p.GetValue(obj); if (v is Image img) return img; } catch { }
                }
            }
            return null;
        }
    }
}

