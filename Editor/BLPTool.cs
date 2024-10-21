using SLZ.Marrow.Warehouse;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BLPTool
{
    [InitializeOnLoad]
    public static class BLPTool
    {
        static BLPTool()
        {
            EditorApplication.update += OnUpdate;
            AssetBundle.UnloadAllAssetBundles(false);
            SceneView.duringSceneGui += DuringSceneGui;

            foreach (var item in UnityEngine.Object.FindObjectsOfType<CrateSpawner>(true))
                if (item != null && item.GetComponent<MeshRenderer>())
                    item.GetComponent<MeshRenderer>().enabled = true;
        }

        static void OnUpdate()
        {
            if (Application.isPlaying) return;
            UnityEngine.Object[] selecteds = Selection.objects.ToArray();
            bool changed = false;
            for (int i = 0; i < selecteds.Length; i++)
            {
                if (selecteds[i] is GameObject selectedGameObject)
                {
                    CratePreview selectedPreview = selectedGameObject.GetComponentInParent<CratePreview>();
                    if (!(selectedPreview?.ExplorablePreview ?? true))
                        selecteds[i] = selectedPreview.gameObject;
                    changed = true;
                }
            }
            if (changed) Selection.objects = selecteds;
            foreach (CrateSpawner spawner in UnityEngine.Object.FindObjectsOfType<CrateSpawner>(true))
            {
                CratePreview previewer = spawner.GetComponent<CratePreview>();
                if (previewer == null) spawner.gameObject.AddComponent<CratePreview>();
            }
        }

        private static void DuringSceneGui(SceneView sceneView)
        {
            Event e = Event.current;
            if (e.type == EventType.DragUpdated) CratePreview.InDrag = true;
            if (e.type == EventType.DragExited) // ondrop
            {
                if (CratePreview.DraggedCols.Count > 0)
                {
                    foreach (var item in CratePreview.DraggedCols)
                        if (item != null)
                            item.enabled = true;
                    CratePreview.DraggedCols.Clear();
                    CratePreview.InDrag = false;
                }
            }
        }

        [MenuItem("GameObject/MarrowSDK/(BLPTool) Material Copier", priority = 1)]
        static void CreateMaterialCopier(UnityEditor.MenuCommand menuCommand)
        {
            GameObject prefabSource = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:Prefab Material Copier")[0]));
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefabSource);

            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Selection.activeObject = go;
        }
        [MenuItem("Stress Level Zero/Void Tools/(BLPTool) Level Loader", priority = 1)]
        static void LevelLoader(UnityEditor.MenuCommand menuCommand)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            Selection.activeGameObject = new GameObject("LevelLoader", typeof(LevelLoader));
        }

    }
}