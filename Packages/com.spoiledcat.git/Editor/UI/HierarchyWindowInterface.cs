using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_6000_5_OR_NEWER
using HierarchyItemId = UnityEngine.EntityId;
#else
using HierarchyItemId = System.Int32;
#endif

namespace Unity.VersionControl.Git.UI
{
    public static class HierarchyWindowInterface
    {
        private static Dictionary<HierarchyItemId, Texture2D> iconCache;
        private static float rightEdge;

        public static void Initialize()
        {
            iconCache = new Dictionary<HierarchyItemId, Texture2D>();
#if UNITY_6000_5_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= OnHierarchyItemTryToDrawStatusIcon;
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyItemTryToDrawStatusIcon;
#else
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyItemTryToDrawStatusIcon;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyItemTryToDrawStatusIcon;
#endif
        }

        private static void OnHierarchyItemTryToDrawStatusIcon(HierarchyItemId instanceID, Rect selectionRect)
        {
            if (!ApplicationConfiguration.HierarchyIconsEnabled)
                return;

            if (!iconCache.TryGetValue(instanceID, out Texture2D texture))
            {
                string guid = null;
#if UNITY_6000_5_OR_NEWER
                GameObject hierarchyGO = EditorUtility.EntityIdToObject(instanceID) as GameObject;
#else
                GameObject hierarchyGO = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
#endif
                if (!hierarchyGO)
                {
                    // Scene header identifiers refer to scene handles rather than GameObjects.
                    string scenePath = "";
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                    {
                        Scene scene = SceneManager.GetSceneAt(i);
#if UNITY_6000_5_OR_NEWER
                        if (scene.handle.GetRawData() == instanceID.GetRawData())
#else
                        if (scene.GetHashCode() == instanceID)
#endif
                        {
                            scenePath = scene.path;
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(scenePath))
                    {
                        iconCache.Add(instanceID, null);
                        return;
                    }

                    guid = AssetDatabase.AssetPathToGUID(scenePath);
                    rightEdge = selectionRect.x;
                }
                else
                {
                    if (PrefabUtility.GetNearestPrefabInstanceRoot(hierarchyGO) == hierarchyGO)
                    {
                        GameObject prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(hierarchyGO);
                        if (prefab)
                        {
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab, out guid, out long _);
                        }
                    }
                }

                if (guid != null)
                {
                    texture = ProjectWindowInterface.GetStatusIconForAssetGUID(guid);
                }

                iconCache.Add(instanceID, texture);
            }

            if (texture == null)
                return;

            // place the icon to the right of the list:
            Rect r = new Rect(selectionRect);
            r.width = 18;

            if (ApplicationConfiguration.HierarchyIconsAlignment == ApplicationConfiguration.HierarchyIconAlignment.Right)
            {
                r.x = ApplicationConfiguration.HierarchyIconsIndented ? selectionRect.width + 6 : selectionRect.xMax - 40;
                r.x -= ApplicationConfiguration.HierarchyIconsOffsetRight - 22f;
            }
            else
            {
                r.x = rightEdge - r.width - 22f;
                r.x += ApplicationConfiguration.HierarchyIconsOffsetLeft;
            }

            GUI.Label(r, texture);
        }
    }
}
