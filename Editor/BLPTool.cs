using SLZ.Marrow.Warehouse;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System;
using UnityEngine.ResourceManagement.ResourceLocations;
using Unity.VisualScripting;

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
        if (Selection.activeGameObject != null)
        {
            CratePreview selectedPreview = Selection.activeGameObject.GetComponentInParent<CratePreview>();
            if (!(selectedPreview?.ExplorablePreview ?? true))
                Selection.activeGameObject = selectedPreview.gameObject;
        }
        foreach (CrateSpawner spawner in UnityEngine.Object.FindObjectsOfType<CrateSpawner>(true))
        {
            CratePreview previewer = spawner.GetComponent<CratePreview>();
            if (previewer == null) spawner.AddComponent<CratePreview>();
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
}
