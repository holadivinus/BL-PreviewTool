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
                sorted.Sort((a,b) => Comparer<int>.Default.Compare(LevenshteinDistance(a.Item2, userInput), LevenshteinDistance(b.Item2, userInput)));

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
                    };
                }
            }
        }

        public static int LevenshteinDistance(string s, string t)
        {
            // Special cases
            if (s == t) return 0;
            if (s.Length == 0) return t.Length;
            if (t.Length == 0) return s.Length;
            // Initialize the distance matrix
            int[,] distance = new int[s.Length + 1, t.Length + 1];
            for (int i = 0; i <= s.Length; i++) distance[i, 0] = i;
            for (int j = 0; j <= t.Length; j++) distance[0, j] = j;
            // Calculate the distance
            for (int i = 1; i <= s.Length; i++)
            {
                for (int j = 1; j <= t.Length; j++)
                {
                    int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                    distance[i, j] = Math.Min(Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1), distance[i - 1, j - 1] + cost);
                }
            }
            // Return the distance
            return distance[s.Length, t.Length];
        }
    }
#endif
}