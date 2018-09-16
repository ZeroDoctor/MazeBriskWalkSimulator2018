using System;
using UnityEngine;
using System.Collections;
using System.IO;
using Devdog.General;
using Devdog.InventoryPro;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Devdog.InventoryPro
{
    [AddComponentMenu(InventoryPro.AddComponentMenuPath + "Managers/Item manager")]
    public partial class ItemManager : ManagerBase<ItemManager>
    {
        [Required]
        [SerializeField]
        [FormerlySerializedAs("itemDatabase")]
        private ItemDatabase _sceneItemDatabase;
        public ItemDatabase sceneItemDatabase
        {
            get { return _sceneItemDatabase; }
            set { _sceneItemDatabase = value; }
        }

        private static InventoryDatabaseLookup<ItemDatabase> _itemDatabaseLookup;
        public static InventoryDatabaseLookup<ItemDatabase> itemDatabaseLookup
        {
            get
            {
                if (_itemDatabaseLookup == null)
                {
                    _itemDatabaseLookup = new InventoryDatabaseLookup<ItemDatabase>(instance != null ? instance.sceneItemDatabase : null, CurrentItemDBPathKey);
                }

                return _itemDatabaseLookup;
            }
        }

        private static string CurrentDBPrefixName
        {
            get
            {
                var path = Application.dataPath;
                if (path.Length > 0)
                {
                    var pathElems = path.Split('/');
                    return pathElems[pathElems.Length - 2];
                }

                return "";
            }
        }

        private static string CurrentItemDBPathKey
        {
            get { return CurrentDBPrefixName + "InventorySystem_CurrentItemDatabasePath"; }
        }

        public static ItemDatabase database
        {
            get
            {
                return itemDatabaseLookup.GetDatabase();
            }
            private set
            {
                itemDatabaseLookup.defaultDatabase = value;
            }
        }
        
        protected override void Awake()
        {
            base.Awake();

#if UNITY_EDITOR
            if (itemDatabaseLookup == null)
                Debug.LogError("Item Database is not assigned!", transform);

#endif
        }

    }
}

// using UnityEditor;