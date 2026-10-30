using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NiumaResource.Editor
{
    /// <summary>
    /// 为字符串地址字段提供可搜索的 Addressables 条目选择器
    /// </summary>
    [CustomPropertyDrawer(typeof(AddressableAddressAttribute))]
    public sealed class AddressableAddressDrawer : PropertyDrawer
    {
        #region Constants(常量)

        private const float VerticalSpacing = 2f;
        private const float MessageHeight = 38f;
        private const float LocateButtonWidth = 44f;

        #endregion

        #region Runtime State(运行时状态)

        private readonly AdvancedDropdownState _dropdownState = new();
        private AddressEntryDropdown _activeDropdown;

        #endregion

        #region Property Drawer(属性绘制)

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(
                    position,
                    "AddressableAddressAttribute 只能用于 string 字段",
                    MessageType.Error);

                EditorGUI.EndProperty();
                return;
            }

            Rect selectorRect = new(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);

            DrawAddressSelector(selectorRect, property, label);

            string validationMessage = GetValidationMessage(property);

            if (!string.IsNullOrEmpty(validationMessage))
            {
                Rect messageRect = new(
                    position.x,
                    selectorRect.yMax + VerticalSpacing,
                    position.width,
                    MessageHeight);

                EditorGUI.HelpBox(
                    messageRect,
                    validationMessage,
                    MessageType.Warning);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                return MessageHeight;
            }

            float height = EditorGUIUtility.singleLineHeight;

            if (!string.IsNullOrEmpty(GetValidationMessage(property)))
            {
                height += VerticalSpacing + MessageHeight;
            }

            return height;
        }

        #endregion

        #region Address Selection(地址选择)

        private void DrawAddressSelector(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            Rect contentRect = EditorGUI.PrefixLabel(position, label);

            Rect dropdownRect = new(
                contentRect.x,
                contentRect.y,
                Mathf.Max(0f, contentRect.width - LocateButtonWidth - VerticalSpacing),
                contentRect.height);

            Rect locateRect = new(
                dropdownRect.xMax + VerticalSpacing,
                contentRect.y,
                LocateButtonWidth,
                contentRect.height);

            AddressableAssetEntry currentEntry = null;

            if (!property.hasMultipleDifferentValues)
            {
                TryFindEntryByAddress(property.stringValue, out currentEntry);
            }

            string displayedAddress = property.hasMultipleDifferentValues
                ? "— 多个值 —"
                : string.IsNullOrWhiteSpace(property.stringValue)
                    ? "未选择"
                    : property.stringValue;

            GUIContent dropdownContent = new(
                displayedAddress,
                currentEntry?.AssetPath ?? property.stringValue);

            if (EditorGUI.DropdownButton(
                    dropdownRect,
                    dropdownContent,
                    FocusType.Keyboard))
            {
                ShowAddressDropdown(dropdownRect, property);
            }

            using (new EditorGUI.DisabledScope(currentEntry == null))
            {
                if (GUI.Button(locateRect, "定位"))
                {
                    PingEntryAsset(currentEntry);
                }
            }
        }

        private void ShowAddressDropdown(
            Rect position,
            SerializedProperty property)
        {
            AddressableAddressAttribute addressAttribute =
                (AddressableAddressAttribute)attribute;

            Object[] targetObjects =
                property.serializedObject.targetObjects;

            string propertyPath = property.propertyPath;

            _activeDropdown = new AddressEntryDropdown(
                _dropdownState,
                addressAttribute.AssetType,
                address => ApplyAddress(
                    targetObjects,
                    propertyPath,
                    address));

            _activeDropdown.Show(position);
        }

        private static void ApplyAddress(
            Object[] targetObjects,
            string propertyPath,
            string address)
        {
            if (targetObjects == null ||
                targetObjects.Length == 0 ||
                string.IsNullOrEmpty(propertyPath))
            {
                return;
            }

            SerializedObject serializedObject =
                new(targetObjects);

            serializedObject.Update();

            SerializedProperty targetProperty =
                serializedObject.FindProperty(propertyPath);

            if (targetProperty == null ||
                targetProperty.propertyType != SerializedPropertyType.String)
            {
                return;
            }

            targetProperty.stringValue = address ?? string.Empty;
            serializedObject.ApplyModifiedProperties();

            GUI.changed = true;
        }

        private static void PingEntryAsset(
            AddressableAssetEntry entry)
        {
            if (entry == null ||
                string.IsNullOrEmpty(entry.AssetPath))
            {
                return;
            }

            Object asset =
                AssetDatabase.LoadMainAssetAtPath(entry.AssetPath);

            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
            }
        }

        #endregion

        #region Addressables Lookup(Addressables 查询)

        /// <summary>
        /// 检查当前地址是否对应 Addressables 中的独立资源条目
        /// </summary>
        private static bool TryFindEntryByAddress(
            string address,
            out AddressableAssetEntry result)
        {
            result = null;

            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;

            if (settings == null)
            {
                return false;
            }

            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (AddressableAssetEntry entry in group.entries)
                {
                    if (entry != null &&
                        string.Equals(
                            entry.address,
                            address,
                            StringComparison.Ordinal))
                    {
                        result = entry;
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region Validation(校验)

        private string GetValidationMessage(
            SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.String ||
                property.hasMultipleDifferentValues ||
                string.IsNullOrWhiteSpace(property.stringValue))
            {
                return string.Empty;
            }

            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;

            if (settings == null)
            {
                return "项目尚未创建 Addressables Settings";
            }

            if (!TryFindEntryByAddress(
                    property.stringValue,
                    out AddressableAssetEntry entry))
            {
                return "当前地址未对应任何 Addressables 独立资源，请重新选择";
            }

            AddressableAddressAttribute addressAttribute =
                (AddressableAddressAttribute)attribute;

            Type actualType =
                AssetDatabase.GetMainAssetTypeAtPath(entry.AssetPath);

            if (actualType == null ||
                !addressAttribute.AssetType.IsAssignableFrom(actualType))
            {
                return $"当前地址对应的资源不是 {addressAttribute.AssetType.Name} 类型";
            }

            return string.Empty;
        }

        #endregion

        #region Dropdown(下拉菜单)

        private sealed class AddressEntryDropdown : AdvancedDropdown
        {
            private readonly Type _assetType;
            private readonly Action<string> _selectionCallback;

            public AddressEntryDropdown(
                AdvancedDropdownState state,
                Type assetType,
                Action<string> selectionCallback)
                : base(state)
            {
                _assetType = assetType ?? typeof(Object);
                _selectionCallback = selectionCallback;

                minimumSize = new Vector2(480f, 320f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                AdvancedDropdownItem root =
                    new("Addressables 资源");

                root.AddChild(new AddressDropdownItem(
                    "清空地址",
                    string.Empty,
                    string.Empty,
                    true));

                AddressableAssetSettings settings =
                    AddressableAssetSettingsDefaultObject.Settings;

                if (settings == null)
                {
                    root.AddChild(new AddressDropdownItem(
                        "尚未创建 Addressables Settings",
                        string.Empty,
                        string.Empty,
                        false));

                    return root;
                }

                List<AddressableAssetGroup> groups = new();

                foreach (AddressableAssetGroup group in settings.groups)
                {
                    if (group != null)
                    {
                        groups.Add(group);
                    }
                }

                groups.Sort((left, right) =>
                    string.Compare(
                        left.Name,
                        right.Name,
                        StringComparison.OrdinalIgnoreCase));

                int matchedCount = 0;

                foreach (AddressableAssetGroup group in groups)
                {
                    List<AddressableAssetEntry> entries =
                        CollectMatchingEntries(group);

                    if (entries.Count == 0)
                    {
                        continue;
                    }

                    AdvancedDropdownItem groupItem =
                        new(group.Name);

                    foreach (AddressableAssetEntry entry in entries)
                    {
                        string fileName =
                            Path.GetFileName(entry.AssetPath);

                        string itemName =
                            $"{entry.address}    [{fileName}]";

                        groupItem.AddChild(new AddressDropdownItem(
                            itemName,
                            entry.address,
                            entry.AssetPath,
                            true));

                        matchedCount++;
                    }

                    root.AddChild(groupItem);
                }

                if (matchedCount == 0)
                {
                    root.AddChild(new AddressDropdownItem(
                        $"没有找到 {_assetType.Name} 类型的 Addressables 资源",
                        string.Empty,
                        string.Empty,
                        false));
                }

                return root;
            }

            protected override void ItemSelected(
                AdvancedDropdownItem item)
            {
                if (item is not AddressDropdownItem addressItem ||
                    !addressItem.CanSelect)
                {
                    return;
                }

                _selectionCallback?.Invoke(addressItem.Address);
            }

            private List<AddressableAssetEntry> CollectMatchingEntries(
                AddressableAssetGroup group)
            {
                List<AddressableAssetEntry> entries = new();

                foreach (AddressableAssetEntry entry in group.entries)
                {
                    if (IsMatchingEntry(entry))
                    {
                        entries.Add(entry);
                    }
                }

                entries.Sort((left, right) =>
                    string.Compare(
                        left.address,
                        right.address,
                        StringComparison.OrdinalIgnoreCase));

                return entries;
            }

            /// <summary>
            /// 检查 Addressables 条目是否具有地址且资源类型符合字段要求
            /// </summary>
            private bool IsMatchingEntry(
                AddressableAssetEntry entry)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.address) ||
                    string.IsNullOrWhiteSpace(entry.AssetPath))
                {
                    return false;
                }

                Type actualType =
                    AssetDatabase.GetMainAssetTypeAtPath(entry.AssetPath);

                return actualType != null &&
                       _assetType.IsAssignableFrom(actualType);
            }
        }

        private sealed class AddressDropdownItem : AdvancedDropdownItem
        {
            public string Address { get; }
            public bool CanSelect { get; }

            public AddressDropdownItem(
                string name,
                string address,
                string assetPath,
                bool canSelect)
                : base(name)
            {
                Address = address;
                CanSelect = canSelect;

                if (!string.IsNullOrEmpty(assetPath))
                {
                    icon = AssetDatabase.GetCachedIcon(assetPath) as Texture2D;
                }
            }
        }

        #endregion
    }
}