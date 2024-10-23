using SLZ.Marrow.Warehouse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace BLPTool
{
#if UNITY_EDITOR
    [CustomEditor(typeof(MaterialDB))]
    public class MaterialDBInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!AssetWarehouse.ready)
            {
                GUILayout.Label("Please refresh the Asset Warehouse.");
                return;
            }

            GUILayout.Label("This DB maps Spawnable Crates to Materials.");
            if (GUILayout.Button("Regenerate DB (takes forever)"))
            {
                MaterialDB db = (MaterialDB)target;
                // lets figure out all available crate assets:
                var allSpawnables = AssetWarehouse.Instance.GetPallets().SelectMany(pal => pal.Crates).Where(c => c is SpawnableCrate).Select(c => c?.MainAsset?.AssetGUID).Where(g => g != null);
                // find the crates that have a valid Addressable:
                var spawnableLocations = Addressables.ResourceLocators.Where(l => l is ResourceLocationMap)
                                                             .SelectMany(l => ((ResourceLocationMap)l).Locations)
                                                             .Where(l => allSpawnables.Contains(l.Key))
                                                             .Select(l => l.Value.First());
                GenerateDB(db, spawnableLocations);
            }
        }
        private async void GenerateDB(MaterialDB db, IEnumerable<IResourceLocation> crates)
        {
            db.Data = new();
            if (EditorUtility.DisplayCancelableProgressBar("Generating Crate Material Db...", " ", 0))
            {
                EditorUtility.ClearProgressBar();
                return;
            }
            float end = crates.Count();
            int i = 0;
            List<string> existings = new List<string>();
            List<string> existingShaders = new List<string>();
            foreach (var item in crates)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Generating Crate Material DB...", $"{i}/{(int)end}: {item.InternalId}", i / end))
                {
                    EditorUtility.ClearProgressBar();
                    return;
                }
                var load = Addressables.LoadAssetAsync<GameObject>(item);
                while(!load.IsDone)
                {
                    await Task.Delay(100);
                    if (EditorUtility.DisplayCancelableProgressBar("Generating Crate Material DB...", $"{i}/{(int)end}: {item.InternalId}", load.PercentComplete))
                    {
                        EditorUtility.ClearProgressBar();
                        return;
                    }
                }
                var mats = load.Result.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Where(m => !existings.Contains(m.ToString())).ToList();
                var newData = new MaterialDB.CrateMats()
                {
                    CrateGUID = item.PrimaryKey,
                    MaterialNames = mats.Select(m => m.ToString()).ToList()
                };
                if (newData.MaterialNames.Count > 0)
                {
                    newData.Shaders = mats.Select(m => m.shader.name).Where(sn => !existingShaders.Contains(sn)).Distinct().ToList();
                    if (newData.Shaders.Count > 0) existingShaders.AddRange(newData.Shaders);
                    db.Data.Add(newData);
                }
                existings.AddRange(newData.MaterialNames);
                i++;
            }

            db.AllMaterials = db.Data.SelectMany(d => d.MaterialNames).ToList();
            db.AllShaders = db.Data.SelectMany(d => d.Shaders).ToList();

            EditorUtility.ClearProgressBar();
        }
    }
#endif
}
