#if UNITY_EDITOR
using SLZ.Marrow.Warehouse;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UltEvents;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace BLPTool
{
    [InitializeOnLoad]
    public static class BLPTool
    { 
        public static string PluginPath
        {
            get
            {
                string a = "Assets/" + Path.GetRelativePath(Application.dataPath, Directory.GetParent(new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileName()).Parent.FullName) + '/';
                if (a.Contains("PackageCache"))
                    a = "Packages/com.holadivinus.blpreviewtool/";
                return a;
            }
        }
        static BLPTool()
        {
            // Ensure XR Package
            Client.Add("https://github.com/holadivinus/BLXRComp.git");

            EditorApplication.update += OnUpdate;
            AssetBundle.UnloadAllAssetBundles(true);
            SceneView.duringSceneGui += DuringSceneGui;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            
            foreach (var item in UnityEngine.Object.FindObjectsOfType<CrateSpawner>(true))
                if (item != null && item.GetComponent<MeshRenderer>())
                    item.GetComponent<MeshRenderer>().enabled = true;
            
            Menu.SetChecked("Stress Level Zero/Void Tools/(BLPTool) Auto Preview Spawners?", PreviewDefault);
        }
        
        static bool setupMats = false;
        static void OnUpdate() // terrible code tbh
        {
            if (Application.isPlaying) return;

            // Deselect Preview's children if ExplorablePreview off
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

            // Attach CratePreview to CrateSpawners
            foreach (CrateSpawner spawner in UnityEngine.Object.FindObjectsOfType<CrateSpawner>(true))
            {
                CratePreview previewer = spawner.GetComponent<CratePreview>();
                if (previewer == null) spawner.gameObject.AddComponent<CratePreview>();
            }

            // Remove invalid MatLinks, force previewing
            foreach (var matLink in BLPDefinitions.Instance.Links.ToArray())
            {
                if (matLink.AssetMat == null)
                {
                    BLPDefinitions.Instance.Links.Remove(matLink);
                    EditorUtility.SetDirty(BLPDefinitions.Instance);
                    continue;
                }
                if (!matLink.AssetMat.name.EndsWith(PreviewTag))
                {
                    matLink.Preview();
                }
            }

            // reset mats on AssetWarehouse.ready
            if (AssetWarehouse.ready && !setupMats)
            {
                setupMats = true;
                foreach (var link in BLPDefinitions.Instance.Links)
                {
                    RevertMaterial(link.AssetMat);
                    link.Preview();
                }
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


        [MenuItem("Stress Level Zero/Void Tools/(BLPTool) Level Loader", priority = 1)]
        static void LevelLoader(UnityEditor.MenuCommand menuCommand)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            Selection.activeGameObject = new GameObject("LevelLoader", typeof(LevelLoader));
        }

        public static bool PreviewDefault
        {
            get => EditorPrefs.GetBool("BLPTOOL_PreviewDefault", true);
            set => EditorPrefs.SetBool("BLPTOOL_PreviewDefault", value);
        }


        [MenuItem("Stress Level Zero/Void Tools/(BLPTool) Auto Preview Spawners?", priority = 1)]
        static void PreviewDefaultToggle(UnityEditor.MenuCommand menuCommand)
        {
            PreviewDefault = !PreviewDefault;
            Menu.SetChecked("Stress Level Zero/Void Tools/(BLPTool) Auto Preview Spawners?", PreviewDefault);
            foreach (var item in Resources.FindObjectsOfTypeAll<CratePreview>())
                item.enabled = BLPTool.PreviewDefault;
        }
        //[MenuItem("Stress Level Zero/Void Tools/Test", priority = 1)]
        static void Test(MenuCommand menuCommand) 
        {
            var ts = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
                .Where(t => t.IsSubclassOf(typeof(Component)) || t.IsSubclassOf(typeof(ScriptableObject)));
            foreach (var t in ts)
            {
                foreach (var prop in t.GetProperties())
                {
                    if (prop.PropertyType.IsSubclassOf(typeof(UnityEngine.Object)))
                        Debug.Log(prop.DeclaringType.Name + ": " + prop.Name + " : " + prop.PropertyType.Name);
                }
            }

        }
        //[MenuItem("Stress Level Zero/Void Tools/Test2", priority = 1)]
        static void Test2(MenuCommand menuCommand) { }

        static void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
        {
            OnGameObjsSaving((obj) => { }, () => scene.GetRootGameObjects());
        }

        /// <summary>
        /// func that injects matsteal code to any scenario
        /// </summary>
        /// <param name="addObj"></param>
        /// <param name="getRootGameObjects"></param>
        public static void OnGameObjsSaving(Action<GameObject> addObj, Func<GameObject[]> getRootGameObjects)
        {
            // First, remove any existing copiers.
            HideFlags stealerHiding = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
            foreach (var rooter in getRootGameObjects.Invoke())
            {
                if (rooter.name == "SLZ MAT-STEAL SYSTEM") //&& rooter.hideFlags == stealerHiding)
                {
                    // we found it
                    UnityEngine.Object.DestroyImmediate(rooter);
                }
            }

            // Scan scene for offending mats:
            MaterialCopier.SaveDelays.Clear();
            //also if scene ignore prefab mats
            var offenders = getRootGameObjects.Invoke().SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).Where(r => PrefabUtility.GetPrefabInstanceHandle(r) == null).SelectMany(r => r.sharedMaterials).Where(m => m != null && BLPDefinitions.Instance.Links.Any(l => l.AssetMat == m)).Distinct();
            GameObject copierRoot = null;
            GameObject prefab = null;
            foreach (var assetMat in offenders)
            {
                if (copierRoot == null)
                {
                    copierRoot = new GameObject("SLZ MAT-STEAL SYSTEM"); // create new root
                    addObj.Invoke(copierRoot);
                    copierRoot.transform.SetAsFirstSibling();
                }
                if (prefab == null)
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BLPTool.PluginPath + "Material Copier.prefab");

                GameObject newCopier = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                newCopier.transform.SetParent(copierRoot.transform);
                newCopier.GetComponent<MaterialCopier>().TargetMat(BLPDefinitions.Instance.Links.First(l => l.AssetMat == assetMat).SLZMat, assetMat);
                // copier for the offending mat is now set up!
            }
            
            if (MaterialCopier.SaveDelays.Count > 0) 
                SlowSaveFollowup();
        }
        private static async void SlowSaveFollowup()
        {
            // I liked the loading bar, but if 1 error occurs unity just makes it run for infinity.
            // so no progress bar for anyone yippee
            //EditorUtility.DisplayProgressBar("Loading required SLZ Crates for Mats...", "This usually happens on first load or Recompilation.", 0);
            /*int c = 0;
            int m = MaterialCopier.SaveDelays.Count;
            foreach (var t in MaterialCopier.SaveDelays)
            {
                t.GetAwaiter().OnCompleted(() => 
                {
                    c++;
                    EditorUtility.DisplayProgressBar("Loading required SLZ Crates for Mats...", "This usually happens on first load or Recompilation.", c/(float)m);
                    if (c == m)
                        EditorUtility.ClearProgressBar();
                }); 
            }*/
            await Task.WhenAll(MaterialCopier.SaveDelays);
            //EditorUtility.ClearProgressBar();


            // Scene Re-save
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            // Prefab Re-save
            PrefabStage currentPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (currentPrefabStage != null)
            {
                // Mark the prefab contents root dirty
                EditorUtility.SetDirty(currentPrefabStage.prefabContentsRoot);
                // Save the prefab stage 
                PrefabUtility.SaveAsPrefabAsset(currentPrefabStage.prefabContentsRoot, currentPrefabStage.assetPath);
            }
        }


        [MenuItem("CONTEXT/Material/(BLPTool) Steal Mat", true)]
        static bool StealMatContextMenuVerifier(MenuCommand menuCommand)
        {
            return AssetDatabase.GetAssetPath(menuCommand.context) == "";
        }

        //[MenuItem("CONTEXT/UltEventHolder/Inc RetVals")]
        static void aaaaaa(MenuCommand menuCommand)
        {
            UltEventHolder h = (UltEventHolder)menuCommand.context;

            foreach (var plist in h.Event.PersistentCallsList)
            {
                foreach (var arg in plist.PersistentArguments)
                {
                    if (arg.Type == PersistentArgumentType.ReturnValue)
                    {
                        arg.ReturnedValueIndex++;
                    }
                }
            }
        }

        [MenuItem("CONTEXT/Material/(BLPTool) Steal Mat")]
        static void StealMatContextMenu(MenuCommand menuCommand)
        {
            Material found = (Material)menuCommand.context;
            Material newMat = UnityEngine.Object.Instantiate<Material>(BLPTool.DefaultMat);
            AssetDatabase.CreateAsset(newMat, "Assets/" + found.name + ".mat");

            // find the spawner that's using this mat
            CrateSpawner spawner = Resources.FindObjectsOfTypeAll<CrateSpawner>()
                                            .FirstOrDefault(p => p?.spawnableCrateReference?.Crate?.MainAsset?.AssetGUID != null
                                                              && p.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                                                                                                      .Any(m => m == found));
            if (spawner == null)
            {
                Debug.Log($"Error, material \"{found.name}\" couldn't be copied: source spawner not found in scene.");
                return;
            }

            var matLink = new BLPDefinitions.MatLink()
            {
                AssetMat = newMat,
                SpawnerAssetGUID = spawner.spawnableCrateReference.Crate.MainAsset.AssetGUID,
                SLZAssetName = found.ToString(),
                SLZMat = found,
            };
            matLink.Crate = new SpawnableCrateReference(spawner.spawnableCrateReference.Crate.Barcode);

            BLPDefinitions.Instance.Links.Add(matLink);
            EditorGUIUtility.PingObject(newMat);
            EditorUtility.SetDirty(BLPDefinitions.Instance);
        }


        public const string PreviewTag = " (IN PREVIEW MODE)";
        private static Material[] s_matCache = new Material[3];
        public static Material DefaultMat => s_matCache[0] ??= AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(PluginPath, "DefaultMat.mat"));
        public static Material RefHolderMat => s_matCache[1] ??= AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(PluginPath, "RefHolderMat.mat"));
        static Material RefHolderMat2 => s_matCache[2] ??= AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(PluginPath, "RefHolderMat2.mat"));

        /// <summary>
        /// Reverts a Material to its initial, stub state.
        /// </summary>
        /// <param name="mat"></param>
        public static void RevertMaterial(Material mat)
        {
            if (mat == null) 
                return;

            if (mat.name.EndsWith(PreviewTag))
            {
                // save mat data to BLPDefinition
                var link = BLPDefinitions.Instance.Links.FirstOrDefault(l => l.AssetMat == mat);
                if (link != null && link.SLZMat != null && link.AssetMat.shader.name == link.SLZMat.shader.name)
                {
                    //we need to note changes between the AssetMat and SLZMat.
                    BLPDefinitions.MatLink.Changes changes = new();

                    // we get api access to texture data!!! :)
                    foreach (var texPropName in link.SLZMat.GetTexturePropertyNames())
                    {
                        if (link.SLZMat.GetTexture(texPropName) != link.AssetMat.GetTexture(texPropName))
                            changes.TexChanges.Add(new() { TexName = texPropName, ChangedTo = link.AssetMat.GetTexture(texPropName) });
                        if (link.SLZMat.GetTextureOffset(texPropName) != link.AssetMat.GetTextureOffset(texPropName))
                            changes.TexOffsetChanges.Add(new() { TexName = texPropName, ChangedTo = link.AssetMat.GetTextureOffset(texPropName) });
                        if (link.SLZMat.GetTextureScale(texPropName) != link.AssetMat.GetTextureScale(texPropName))
                            changes.TexScaleChanges.Add(new() { TexName = texPropName, ChangedTo = link.AssetMat.GetTextureScale(texPropName) });
                    }

                    {
                        // Color data
                        var SLZedit = new SerializedObject(link.SLZMat);
                        var Assetedit = new SerializedObject(link.AssetMat);

                        var slzIt = SLZedit.FindProperty("m_SavedProperties");
                        var assetIt = Assetedit.FindProperty("m_SavedProperties");

                        var slz_type_Props = slzIt.Copy().FindPropertyRelative("m_Colors")?.GetChildren();
                        if (slz_type_Props != null)
                        {
                            var asset_type_Props = assetIt.Copy().FindPropertyRelative("m_Colors").GetChildren();
                            foreach (var SlzAssetProp in slz_type_Props.Zip(asset_type_Props, (slz, asset) => (slz, asset)).Skip(1))
                            {
                                string propName = SlzAssetProp.slz.displayName;
                                var slzVal = SlzAssetProp.slz.FindPropertyRelative("second").colorValue; // change to targ val type
                                var assetVal = SlzAssetProp.asset.FindPropertyRelative("second").colorValue; // change to targ val type

                                if (slzVal != assetVal) changes.ColorChanges.Add(new() { ColorName = propName, ChangedTo = assetVal });
                            }
                        } 
                    }

                    {
                        // int data
                        var SLZedit = new SerializedObject(link.SLZMat);
                        var Assetedit = new SerializedObject(link.AssetMat);

                        var slzIt = SLZedit.FindProperty("m_SavedProperties");
                        var assetIt = Assetedit.FindProperty("m_SavedProperties");

                        var slz_type_Props = slzIt.Copy().FindPropertyRelative("m_Ints")?.GetChildren();
                        if (slz_type_Props != null)
                        {
                            var asset_type_Props = assetIt.Copy().FindPropertyRelative("m_Ints").GetChildren();
                            foreach (var SlzAssetProp in slz_type_Props.Zip(asset_type_Props, (slz, asset) => (slz, asset)).Skip(1))
                            {
                                string propName = SlzAssetProp.slz.displayName;
                                var slzVal = SlzAssetProp.slz.FindPropertyRelative("second").intValue; // change to targ val type
                                var assetVal = SlzAssetProp.asset.FindPropertyRelative("second").intValue; // change to targ val type

                                if (slzVal != assetVal) changes.IntChanges.Add(new() { IntName = propName, ChangedTo = assetVal });
                            }
                        }
                    }

                    {
                        // float data
                        var SLZedit = new SerializedObject(link.SLZMat);
                        var Assetedit = new SerializedObject(link.AssetMat);

                        var slzIt = SLZedit.FindProperty("m_SavedProperties");
                        var assetIt = Assetedit.FindProperty("m_SavedProperties");

                        var slz_type_Props = slzIt.Copy().FindPropertyRelative("m_Floats")?.GetChildren();
                        if (slz_type_Props != null)
                        {
                            var asset_type_Props = assetIt.Copy().FindPropertyRelative("m_Floats").GetChildren();
                            foreach (var SlzAssetProp in slz_type_Props.Zip(asset_type_Props, (slz, asset) => (slz, asset)).Skip(1))
                            {
                                string propName = SlzAssetProp.slz.displayName;
                                var slzVal = SlzAssetProp.slz.FindPropertyRelative("second").floatValue; // change to targ val type
                                var assetVal = SlzAssetProp.asset.FindPropertyRelative("second").floatValue; // change to targ val type

                                if (slzVal != assetVal) changes.FloatChanges.Add(new() { FloatName = propName, ChangedTo = assetVal });
                            }
                        }
                    }

                    link.MatChanges = changes;
                    EditorUtility.SetDirty(BLPDefinitions.Instance);
                }

                // reset the material
                mat.shader = DefaultMat.shader;
                mat.CopyPropertiesFromMaterial(DefaultMat);

                mat.name = mat.name.Substring(0, mat.name.Length - PreviewTag.Length);
            }
        }
    }
}
#endif
