﻿using SLZ.Marrow.Zones;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;

using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;

namespace BLPTool
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    class BLPAssetModificationProcessor : AssetModificationProcessor
    {
        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions rao)
        {
            BLPTool.RevertMaterial(AssetDatabase.LoadAssetAtPath<Material>(assetPath));
            return AssetDeleteResult.DidNotDelete;
        }
        private static AssetMoveResult OnWillMoveAsset(string sourcePath, string destinationPath)
        {
            BLPTool.RevertMaterial(AssetDatabase.LoadAssetAtPath<Material>(sourcePath));
            return AssetMoveResult.DidNotMove;
        }
        private static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (var savePath in paths)
                try
                {
                    if (savePath.EndsWith(".mat"))
                        BLPTool.RevertMaterial(AssetDatabase.LoadAssetAtPath<Material>(savePath));
                    else if (savePath.EndsWith(".prefab"))
                    {
                        GameObject prefabRoot = PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot;
                        BLPTool.OnGameObjsSaving((newObj) => newObj.transform.parent = prefabRoot.transform, () =>
                        {
                            var childs = new GameObject[prefabRoot.transform.childCount];
                            for (global::System.Int32 i = 0; i < prefabRoot.transform.childCount; i++)
                                childs[i] = prefabRoot.transform.GetChild(i).gameObject;
                            return childs;
                        });
                    }
                } catch { }
            return paths;
        }
    }
#endif
}