using UnityEditor;
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
                if (savePath.EndsWith(".mat"))
                    BLPTool.RevertMaterial(AssetDatabase.LoadAssetAtPath<Material>(savePath));
            return paths;
        }
    }
#endif
}