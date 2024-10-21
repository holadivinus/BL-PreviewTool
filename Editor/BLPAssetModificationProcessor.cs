using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
class BLPAssetModificationProcessor : AssetModificationProcessor
{
    private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions rao)
    {
        MaterialCopier.Revert(AssetDatabase.LoadAssetAtPath<Material>(assetPath));
        return AssetDeleteResult.DidNotDelete;
    }
    private static AssetMoveResult OnWillMoveAsset(string sourcePath, string destinationPath)
    {
        MaterialCopier.Revert(AssetDatabase.LoadAssetAtPath<Material>(sourcePath));
        return AssetMoveResult.DidNotMove;
    }
    private static string[] OnWillSaveAssets(string[] paths)
    {
        foreach (var savePath in paths)
        {
            if (savePath.EndsWith(".mat"))
                MaterialCopier.Revert(AssetDatabase.LoadAssetAtPath<Material>(savePath));
        }
        return paths;
    }
}

