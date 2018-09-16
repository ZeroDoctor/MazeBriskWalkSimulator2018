using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Devdog.General;
using Devdog.General.Editors;
using Devdog.General.Editors.ReflectionDrawers;
using UnityEditor;
using UnityEngine;
using EditorUtility = UnityEditor.EditorUtility;
using Object = UnityEngine.Object;
using EditorStyles = Devdog.General.Editors.EditorStyles;

namespace Devdog.InventoryPro.Editors
{
    public class ItemEditor : InventoryEditorCrudBase<InventoryItemBase>
    {
        protected class TypeFilter
        {
            public System.Type type;
            public bool enabled;

            public TypeFilter(System.Type type, bool enabled)
            {
                this.type = type;
                this.enabled = enabled;
            }
        }

        protected override List<InventoryItemBase> crudList
        {
            get { return new List<InventoryItemBase>(ItemManager.database.items); }
            set { ItemManager.database.items = value.ToArray(); }
        }

        public Editor itemEditorInspector { get; set; }

        private static InventoryItemBase _previousItem;
        private static InventoryItemBase _isDraggingPrefab;
        private string _previouslySelectedGUIItemName;

        protected TypeFilter[] allItemTypes;

        public ItemEditor(string singleName, string pluralName, EditorWindow window)
            : base(singleName, pluralName, window)
        {
            if (selectedItem != null)
            {
                itemEditorInspector = Editor.CreateEditor(selectedItem);
            }

            window.autoRepaintOnSceneChange = false;
            allItemTypes = GetAllItemTypes();
        }

        protected TypeFilter[] GetAllItemTypes()
        {
            return ReflectionUtility.GetAllTypesThatImplement(typeof (InventoryItemBase), true)
                    .Select(o => new TypeFilter(o, false)).ToArray();
        }

        protected override bool MatchesSearch(InventoryItemBase item, string searchQuery)
        {
            searchQuery = searchQuery ?? "";

            string search = searchQuery.ToLower();
            return (item.name.ToLower().Contains(search) ||
                item.description.ToLower().Contains(search) ||
                item.ID.ToString().Contains(search) ||
                item.GetType().Name.ToLower().Contains(search));
        }

        protected override void CreateNewItem()
        {
            var picker = CreateNewItemEditor.Get((System.Type type, GameObject obj, EditorWindow thisWindow) =>
            {
                InventoryScriptableObjectUtility.SetPrefabSaveFolderIfNotSet();
                string prefabPath = InventoryScriptableObjectUtility.GetSaveFolderForFolderName("Items") + "/item_" + System.DateTime.Now.ToFileTimeUtc() + "_PFB.prefab";

                //var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var instanceObj = UnityEngine.Object.Instantiate<GameObject>(obj); // For unity 5.3+ - Source needs to be instance object.
                var prefab = PrefabUtility.CreatePrefab(prefabPath, instanceObj);
                UnityEngine.Object.DestroyImmediate(instanceObj);

                if (InventorySettingsManager.instance != null && InventorySettingsManager.instance.settings != null)
                {
                    prefab.layer = InventorySettingsManager.instance.settings.itemWorldLayer;
                }
                else
                {
                    Debug.LogWarning("Couldn't set item layer because there's no InventorySettingsManager in the scene");
                }

                AssetDatabase.SetLabels(prefab, new string[] { "InventoryProPrefab" });

                var comp = (InventoryItemBase)prefab.AddComponent(type);
                comp.ID = (crudList.Count > 0) ? crudList.Max(o => o.ID) + 1 : 0;
                EditorUtility.SetDirty(comp); // To save it.

                prefab.GetOrAddComponent<ItemTrigger>();
                prefab.GetOrAddComponent<ItemTriggerInputHandler>();
                if (prefab.GetComponent<SpriteRenderer>() == null)
                {
                    // This is not a 2D object
                    if (prefab.GetComponent<Collider>() == null)
                        prefab.AddComponent<BoxCollider>();

                    var sphereCollider = prefab.GetOrAddComponent<SphereCollider>();
                    sphereCollider.isTrigger = true;
                    sphereCollider.radius = 1f;

                    prefab.GetOrAddComponent<Rigidbody>();
                }

                // Avoid deleting the actual prefab / model, only the cube / internal models without an asset path.
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(obj)))
                {
                    Object.DestroyImmediate(obj);
                }

                AddItem(comp, true);
                thisWindow.Close();
            });
            picker.Show();
        }

        public override void DuplicateItem(int index)
        {
            var source = crudList[index];

            var item = UnityEngine.Object.Instantiate<InventoryItemBase>(source);
            item.ID = (crudList.Count > 0) ? crudList.Max(o => o.ID) + 1 : 0;
            item.name += "(duplicate)";

            string prefabPath = InventoryScriptableObjectUtility.GetSaveFolderForFolderName("Items") + "/item_" + System.DateTime.Now.ToFileTimeUtc() + "_PFB.prefab";

            var prefab = PrefabUtility.CreatePrefab(prefabPath, item.gameObject);
            prefab.layer = InventorySettingsManager.instance.settings.itemWorldLayer;

            AssetDatabase.SetLabels(prefab, new string[] { "InventoryProPrefab" });

            AddItem(prefab.gameObject.GetComponent<InventoryItemBase>());

            EditorUtility.SetDirty(prefab); // To save it.

            UnityEngine.Object.DestroyImmediate(item.gameObject, false); // Destroy the instance created

            window.Repaint();
        }

        public override void AddItem(InventoryItemBase item, bool editOnceAdded = true)
        {
            base.AddItem(item, editOnceAdded);
            UpdateAssetName(item);

            EditorUtility.SetDirty(ItemManager.database); // To save it.
        }

        public override void RemoveItem(int i)
        {
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(ItemManager.database.items[i]));
            base.RemoveItem(i);

            EditorUtility.SetDirty(ItemManager.database); // To save it.
        }

        public override void EditItem(InventoryItemBase item)
        {
            base.EditItem(item);

            Undo.ClearUndo(_previousItem);

            Undo.RecordObject(item, "INV_PRO_item");

            if (item != null)
                itemEditorInspector = Editor.CreateEditor(item);


            _previousItem = item;
        }

        protected override void DrawSidebar()
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();

            int i = 0;
            foreach (var type in allItemTypes)
            {
                if(i % 3 == 0)
                    GUILayout.BeginHorizontal();

                type.enabled = GUILayout.Toggle(type.enabled, type.type.Name.Replace("InventoryItem", ""), "OL Toggle");
                
                if (i % 3 == 2 || i == allItemTypes.Length - 1)
                    GUILayout.EndHorizontal();

                i++;
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            base.DrawSidebar();

        }

        protected override void DrawSidebarRow(InventoryItemBase item, int i)
        {
            int checkedCount = 0;
            foreach (var type in allItemTypes)
            {
                if (type.enabled)
                    checkedCount++;
            }

            if (checkedCount > 0)
            {
                if (allItemTypes.FirstOrDefault(o => o.type == item.GetType() && o.enabled) == null)
                {
                    return;
                }
            }

            BeginSidebarRow(item, i);

            DrawSidebarRowElement("#" + item.ID.ToString(), 40);
            DrawSidebarRowElement(item.name, 130);
            DrawSidebarRowElement(item.GetType().Name.Replace("InventoryItem", ""), 125);
            DrawSidebarValidation(item, i);

            sidebarRowElementOffset.x -= 20; // To compensate for visibility toggle
            bool t = DrawSidebarRowElementToggle(true, "", "VisibilityToggle", 20);
            if (t == false) // User clicked view icon
                AssetDatabase.OpenAsset(selectedItem);

            EndSidebarRow(item, i);
        }

        protected override void ClickedSidebarRowElement(InventoryItemBase item)
        {
            base.ClickedSidebarRowElement(item);
        }

        protected override void DrawDetail(InventoryItemBase item, int index)
        {
            EditorGUIUtility.labelWidth = EditorStyles.labelWidth;

            if (InventoryScriptableObjectUtility.isPrefabsSaveFolderSet == false)
            {
                EditorGUILayout.HelpBox("Prefab save folder is not set.", MessageType.Error);
                if (GUILayout.Button("Set prefab save folder"))
                {
                    InventoryScriptableObjectUtility.SetPrefabSaveFolder();
                }

                EditorGUIUtility.labelWidth = 0;
                return;
            }

            GUILayout.Label("Use the inspector if you want to add custom components.", EditorStyles.titleStyle);
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            if (GUILayout.Button("Convert type"))
            {
                var typePicker = ScriptPickerEditor.Get(typeof(InventoryItemBase));
                typePicker.Show();
                typePicker.OnPickObject += type =>
                {
                    ConvertThisToNewType(item, type);
                };

                return;
            }

            EditorGUI.BeginChangeCheck();
            itemEditorInspector.OnInspectorGUI();

            if (_previouslySelectedGUIItemName == "ItemEditor_itemName" && GUI.GetNameOfFocusedControl() != _previouslySelectedGUIItemName)
            {
                UpdateAssetName(item);
            }

            if (EditorGUI.EndChangeCheck() && selectedItem != null)
            {
                UnityEditor.EditorUtility.SetDirty(selectedItem);
            }

            _previouslySelectedGUIItemName = GUI.GetNameOfFocusedControl();

            ValidateItemFromCache(item);
            EditorGUIUtility.labelWidth = 0;
        }

        public static string GetAssetName(InventoryItemBase item)
        {
            return "Item_" + (string.IsNullOrEmpty(item.name) ? string.Empty : item.name.ToLower().Replace(" ", "_")) + "_#" + item.ID + "_" + ItemManager.database.name + "_PFB";
        }

        public static void UpdateAssetName(InventoryItemBase item)
        {
            var newName = GetAssetName(item);
            if (AssetDatabase.GetAssetPath(item).EndsWith(newName + ".prefab") == false)
            {
                AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(item), newName);
            }
        }

        public void ConvertThisToNewType(InventoryItemBase currentItem, Type type)
        {
            var comp = (InventoryItemBase)currentItem.gameObject.AddComponent(type);
            ReflectionUtility.CopySerializableValues(currentItem, comp);

            // Set in database
            for (int i = 0; i < ItemManager.database.items.Length; i++)
            {
                if (ItemManager.database.items[i].ID == currentItem.ID)
                {
                    ItemManager.database.items[i] = comp;
                }
            }

            selectedItem = comp;
            itemEditorInspector = Editor.CreateEditor(selectedItem);
            EditorUtility.SetDirty(selectedItem);
            GUI.changed = true;

            Object.DestroyImmediate(currentItem, true); // Get rid of the old object
            window.Repaint();
        }

        protected override bool IDsOutOfSync()
        {
            uint next = 0;
            foreach (var item in crudList)
            {
                if (item == null || item.ID != next)
                    return true;
                
                next++;
            }

            return false;
        }

        protected override void SyncIDs()
        {
            Debug.Log("Item ID's out of sync, force updating...");

            crudList = crudList.Where(o => o != null).ToList();
            uint lastID = 0;
            foreach (var item in crudList)
            {
                item.ID = lastID++;
                EditorUtility.SetDirty(item);
            }

            GUI.changed = true;
            EditorUtility.SetDirty(ItemManager.database);
        }
    }
}
