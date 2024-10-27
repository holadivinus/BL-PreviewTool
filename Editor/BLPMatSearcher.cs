using FuzzySharp;
using SLZ.Marrow.Warehouse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

namespace BLPTool
{
#if UNITY_EDITOR
    public class BLPMatSearcher : EditorWindow
    {
        [MenuItem("Stress Level Zero/Void Tools/(BLPTool) External Material Copier", priority = 0)]
        public static void ShowExample()
        {
            BLPMatSearcher wnd = GetWindow<BLPMatSearcher>();
            wnd.titleContent = new GUIContent("BLPTool Mat Searcher");
        }

        public VisualElement WhiteListTab;
        public Button WhiteListTabBT;
        public Button OpenFilterTabBT;
        public VisualElement PalletFilterList;
        public VisualElement Results;
        public static bool ready;
        public bool WhiteListTabOpen;
        public enum SearchMode { Shader, Material }
        public SearchMode searchMode = SearchMode.Material;
        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
              
            // Import UXML  
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BLPTool.PluginPath + "Editor/BLPMatSearcher.uxml");
            root.Add(visualTree.Instantiate());

            PalletFilterList = rootVisualElement.Q<VisualElement>("PalletFilterList");
            WhiteListTabBT = rootVisualElement.Q<Button>("WhiteListTabBT");
            WhiteListTab = rootVisualElement.Q<VisualElement>("WhiteListTab");
            OpenFilterTabBT = rootVisualElement.Q<Button>("OpenFilterTabBT");
            void ToggleFilterTab()
            {
                WhiteListTabOpen = !WhiteListTabOpen;
                WhiteListTab.style.display = WhiteListTabOpen ? DisplayStyle.Flex : DisplayStyle.None;
                OpenFilterTabBT.style.display = WhiteListTabOpen ? DisplayStyle.Flex : DisplayStyle.None;
            }
            WhiteListTabBT.clicked += ToggleFilterTab;
            OpenFilterTabBT.clicked += ToggleFilterTab;

            var matBT = rootVisualElement.Q<Button>("MatBT");
            var shaderBT = rootVisualElement.Q<Button>("ShaderBT");
            void ToggleSearchMode()
            {
                searchMode = searchMode == SearchMode.Material ? SearchMode.Shader : SearchMode.Material;
                matBT.style.display = searchMode == SearchMode.Material ? DisplayStyle.Flex : DisplayStyle.None;
                shaderBT.style.display = searchMode == SearchMode.Shader ? DisplayStyle.Flex : DisplayStyle.None;
                searched = new();
            }
            matBT.clicked += ToggleSearchMode;
            shaderBT.clicked += ToggleSearchMode;

            Results = rootVisualElement.Q<VisualElement>("SearchResultBTs");

            rootVisualElement.Q<Button>("LoadMoreBT").clicked += () => ShowMoreResults(16);

            rootVisualElement.Q<Button>("SearchBT").clicked += Search;
            rootVisualElement.Q<TextField>("SearchText").RegisterValueChangedCallback((evt) => Search());
            ready = true;
            OnFocus();
        }

        static Dictionary<string, bool> SelectedFilters = new();
        private void OnFocus()
        {
            if (!ready) return;
            // Refresh Pallet List
            PalletFilterList.Clear();
            if (!AssetWarehouse.ready)
            { 
                EditorApplication.delayCall += OnFocus;
                return;
            }

            // List Filters
            foreach (var pallet in AssetWarehouse.Instance.GetPallets())
            {
                var toggleUI = new Toggle(pallet.Barcode.ID);
                toggleUI.style.flexDirection = FlexDirection.RowReverse;

                EditorApplication.delayCall += () =>
                {
                    var numer = toggleUI.Children().GetEnumerator();
                    numer.MoveNext();
                    numer.Current.style.flexGrow = 10;
                    numer.MoveNext();
                    numer.Current.style.flexGrow = 0;
                };

                string palletName = pallet.Barcode.ID;
                if (SelectedFilters.TryGetValue(palletName, out bool val)) 
                {
                    toggleUI.value = val;
                } else
                {
                    toggleUI.value = true;
                    SelectedFilters[palletName] = true;
                }

                toggleUI.RegisterValueChangedCallback((evt) =>
                {
                    SelectedFilters[palletName] = evt.newValue;
                });

                PalletFilterList.Add(toggleUI);
            }
            
            
        }

        private List<string> searched = new();
        private void Search()
        {
            var dbg = rootVisualElement.Q<Label>("debuglabel");

            if (!AssetWarehouse.ready) return;
            if (searchMode == SearchMode.Material)
            {
                Results.Clear();
                string userInput = rootVisualElement.Q<TextField>("SearchText").text.ToLower();
                var sorted = MaterialDB.Instance.AllMaterials.Select(m => (m, m.Replace(" (UnityEngine.Material)", "").ToLower())).ToList();
                sorted.Sort((a,b) => Comparer<int>.Default.Compare(CompareStrings(b.Item2, userInput), CompareStrings(a.Item2, userInput)));

                searched = sorted.Select(s => s.Item1).ToList();
                ShowMoreResults(16);
            } else
            {
                // shaders?
                Results.Clear();
                string userInput = rootVisualElement.Q<TextField>("SearchText").text.ToLower();
                var sorted = MaterialDB.Instance.AllShaders.Select(m => (m, m.Replace(" (UnityEngine.Shader)", "").ToLower())).ToList();
                sorted.Sort((a, b) => Comparer<int>.Default.Compare(CompareStrings(b.Item2, userInput), CompareStrings(a.Item2, userInput)));

                searched = sorted.Select(s => s.Item1).ToList();
                ShowMoreResults(16);
            }
        }
        private static string GetActiveFolderPath()
        {
            return "Assets/";
            Type projectWindowUtilType = typeof(ProjectWindowUtil);
            MethodInfo getActiveFolderPath = projectWindowUtilType.GetMethod("GetActiveFolderPath", BindingFlags.Static | BindingFlags.NonPublic);
            object obj = getActiveFolderPath.Invoke(null, new object[0]);
            string pathToCurrentFolder = obj.ToString();
            return pathToCurrentFolder + '/';
        }
        private void ShowMoreResults(int num)
        {
            if (searched.Count() == 0) return;
            if (searchMode == SearchMode.Material)
            {
                List<string> show = searched.Take(Math.Min(num, searched.Count())).ToList();
                searched = searched.Skip(Math.Min(num, searched.Count())).ToList();
                //linq my beloved
                foreach (var matName in show)
                {
                    // find the spawnable that owns this mat
                    string crateGuid = MaterialDB.Instance.Data.First(d => d.MaterialNames.Contains(matName)).CrateGUID;
                    Material found = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m.ToString() == matName);
                    MaterialEditor e = null;

                    IMGUIContainer newVis = null;
                    newVis = new IMGUIContainer(() =>
                    {
                        if (e != null)
                            e.DrawPreview(newVis.contentRect);
                    });

                    void ProcessMat(Material mat)
                    {
                        foreach (var texProp in mat.GetTexturePropertyNames())
                        {
                            var tex = mat.GetTexture(texProp);
                            if (tex != null && tex is Texture2D tex2D)
                            {
                                tex2D.minimumMipmapLevel = 0;
                                tex2D.requestedMipmapLevel = 0;
                            }
                        }
                        e = (MaterialEditor)MaterialEditor.CreateEditor(mat);
                        found = mat;
                    }
                    if (found == null)
                    {
                        Addressables.LoadAssetAsync<GameObject>(crateGuid).Completed += (a) 
                            => ProcessMat(Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m.ToString() == matName));
                    } else ProcessMat(found);


                    
                    newVis.style.width = 100;
                    newVis.style.height = 100;
                    
                    Results.Add(newVis);

                    var bt = new Button();
                    newVis.Add(bt);
                    bt.text = matName.Replace(" (UnityEngine.Material)", "");
                    bt.style.position = Position.Absolute;
                    bt.style.bottom = 0;

                    bt.clicked += () =>
                    {
                        Material newMat = Instantiate<Material>(BLPTool.DefaultMat);
                        AssetDatabase.CreateAsset(newMat, GetActiveFolderPath() + found.name + ".mat");
                        var matLink = new BLPDefinitions.MatLink()
                        {
                            AssetMat = newMat,
                            SpawnerAssetGUID = MaterialDB.Instance.Data.First(d => d.MaterialNames.Contains(found.ToString())).CrateGUID,
                            SLZAssetName = found.ToString(),
                            SLZMat = found,
                        };
                        matLink.Crate = new SpawnableCrateReference(AssetWarehouse.Instance.GetCrates().First(c => c?.MainAsset?.AssetGUID == matLink.SpawnerAssetGUID).Barcode);

                        BLPDefinitions.Instance.Links.Add(matLink);
                        EditorGUIUtility.PingObject(newMat);
                        EditorUtility.SetDirty(BLPDefinitions.Instance);
                    };
                }
            }
            else
            {
                List<string> show = searched.Take(Math.Min(num, searched.Count())).ToList();
                searched = searched.Skip(Math.Min(num, searched.Count())).ToList();
                //linq my beloved
                foreach (var shaderName in show)
                {
                    // find the spawnable that owns this shader
                    string crateGuid = MaterialDB.Instance.Data.First(d => d.Shaders.Contains(shaderName)).CrateGUID;
                    Shader found = Resources.FindObjectsOfTypeAll<Shader>().FirstOrDefault(s => s.name == shaderName);


                    var newVis = new Button();
                    newVis.text = shaderName.Replace(" (UnityEngine.Shader)", "");

                    
                    if (found == null)
                    {
                        string sn = shaderName;
                        Addressables.LoadAssetAsync<GameObject>(crateGuid).Completed += (a)
                            => found = Resources.FindObjectsOfTypeAll<Shader>().FirstOrDefault(s => s.name == sn);
                    }

                    Results.Add(newVis);


                    newVis.clicked += async () =>
                    {
                        Material newMat = Instantiate<Material>(BLPTool.DefaultMat);
                        AssetDatabase.CreateAsset(newMat, GetActiveFolderPath() + found.name.Split('/').Last() + ".mat");

                        // we must find the mat that owns this shader
                        var dbData = MaterialDB.Instance.Data.First(d => d.Shaders.Contains(found.name));
                        var mat = (await Addressables.LoadAssetAsync<GameObject>(dbData.CrateGUID).Task)
                        .GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).First(m => m.shader.name == found.name);

                        var matLink = new BLPDefinitions.MatLink()
                        {
                            AssetMat = newMat,
                            SpawnerAssetGUID = MaterialDB.Instance.Data.First(d => d.Shaders.Contains(found.name)).CrateGUID,
                            SLZAssetName = mat.ToString(),
                            SLZMat = mat,
                        };
                        matLink.Crate = new SpawnableCrateReference(AssetWarehouse.Instance.GetCrates().First(c => c?.MainAsset?.AssetGUID == matLink.SpawnerAssetGUID).Barcode);

                        BLPDefinitions.Instance.Links.Add(matLink);
                        EditorGUIUtility.PingObject(newMat);
                        EditorUtility.SetDirty(BLPDefinitions.Instance);

                        newMat.shader = found;
                        newMat.name += BLPTool.PreviewTag;
                        BLPTool.RevertMaterial(newMat); // record the defaults of this shader
                    };
                }
            }
        }

        public static int CompareStrings(string s, string t)
        {
            //rahh we hate leveinshtirnenenn
            return Fuzz.WeightedRatio(s, t);
        }
    }
#endif
}