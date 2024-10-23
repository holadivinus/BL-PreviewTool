#if UNITY_EDITOR
using SLZ.Marrow.Warehouse;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
            EditorApplication.update += OnUpdate;
            AssetBundle.UnloadAllAssetBundles(false);
            SceneView.duringSceneGui += DuringSceneGui;
            EditorSceneManager.sceneSaving += OnSceneSaving;


            foreach (var item in UnityEngine.Object.FindObjectsOfType<CrateSpawner>(true))
                if (item != null && item.GetComponent<MeshRenderer>())
                    item.GetComponent<MeshRenderer>().enabled = true;
        }

        static bool setupMats = false;
        static void OnUpdate() // terrible code tbh
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
            foreach (var matLink in BLPDefinitions.Instance.Links.ToArray())
            {
                if (matLink.AssetMat == null)
                {
                    BLPDefinitions.Instance.Links.Remove(matLink);
                    EditorUtility.SetDirty(BLPDefinitions.Instance);
                    return;
                }
                if (!matLink.AssetMat.name.EndsWith(PreviewTag))
                {
                    matLink.Preview();
                }
            }
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
        //[MenuItem("Stress Level Zero/Void Tools/Test", priority = 1)]
        static void Test(MenuCommand menuCommand) { }
        //[MenuItem("Stress Level Zero/Void Tools/Test2", priority = 1)]
        static void Test2(MenuCommand menuCommand) { }
        static void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
        {
            // First, remove any existing copiers.
            HideFlags stealerHiding = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
            foreach (var rooter in scene.GetRootGameObjects())
            {
                if (rooter.name == "SLZ MAT-STEAL SYSTEM") //&& rooter.hideFlags == stealerHiding)
                {
                    // we found it
                    UnityEngine.Object.DestroyImmediate(rooter);
                }
            }

            // Scan scene for offending mats:
            foreach (var rooter in scene.GetRootGameObjects())
            {
                var offenders = rooter.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null && BLPDefinitions.Instance.Links.Any(l => l.AssetMat == m)).Distinct();
                GameObject copierRoot = null;
                GameObject prefab = null;
                foreach (var assetMat in offenders)
                {
                    if (copierRoot == null)
                    {
                        copierRoot = new GameObject("SLZ MAT-STEAL SYSTEM"); // create new root
                        copierRoot.transform.SetAsFirstSibling();
                    }
                    if (prefab == null)
                        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BLPTool.PluginPath + "Material Copier.prefab");

                    GameObject newCopier = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    newCopier.transform.SetParent(copierRoot.transform);
                    newCopier.GetComponent<MaterialCopier>().TargetMat(BLPDefinitions.Instance.Links.First(l => l.AssetMat == assetMat).SLZMat, assetMat);
                    // copier for the offending mat is now set up!
                }
            }
            
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
                if (link != null && link.SLZMat != null && link.AssetMat.shader == link.SLZMat.shader)
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
