using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEditor;
using SLZ.Marrow.Zones;
using UltEvents;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.AddressableAssets.ResourceLocators;
using static UnityEditor.Progress;





#if UNITY_EDITOR
using SLZ.Marrow.Warehouse;
using SLZ.MarrowEditor;
#endif
namespace BLPTool
{
    [ExecuteAlways]
    public class LevelLoader : MonoBehaviour
    {
#if UNITY_EDITOR
        void Start()
        {
            if (!EditorApplication.isPlaying) return;
            this.gameObject.AddComponent<Camera>();
            

            Texture.streamingTextureForceLoadAll = true;
            Time.timeScale = 0;
            AssetBundle.UnloadAllAssetBundles(true);
            DontDestroyOnLoad(gameObject);
            var prefunc = Addressables.InternalIdTransformFunc;
            Addressables.InternalIdTransformFunc = (location) =>
            {
                string o = location.InternalId;
                if (o.StartsWith("PALLET_BARCODE"))
                {
                    int firstSlash = o.IndexOf("\\");
                    if (firstSlash != -1)
                        o = o.Substring(firstSlash);
                }
                if (location.PrimaryKey.EndsWith(".bundle"))
                {
                    o = standPath + o;
                    if (!File.Exists(o))
                    {
                        string pre = o;
                        int last_ = o.LastIndexOf('_');
                        int lastSlash = o.LastIndexOf("/");
                        if (last_ != -1 && lastSlash != -1 && lastSlash < last_)
                        {
                            o = o.Substring(0, last_) + ".bundle";
                        }

                        if (!File.Exists(o))
                            o = pre;
                    }
                }

                if (!File.Exists(o))
                {
                    // check if the file would exist in each catalog
                    o = o.Split("StandaloneWindows64\\").Last();
                    foreach (var catalogPath in targCatalogs)
                    {
                        string cat = Directory.GetParent(catalogPath).FullName;
                        string p = Path.Combine(cat + "/", o);
                        if (File.Exists(p))
                        {
                            o = p;
                            break;
                        }
                    }
                }

                return o;
            };

            LevelLoad();
        }
        [SerializeField][HideInInspector] List<string> targCatalogs;
        [SerializeField][HideInInspector] PalletReference Pallet;
        [SerializeField] CrateReferenceT<LevelCrate> level;/*
        [SerializeField] bool LoadAllScenesThatStartWith;
        [SerializeField] string StartWith;*/


        [SerializeField][HideInInspector] string Bonelab_Folder = "";
        [SerializeField] string[] will_be_loaded = null;
        string a;
        string b;
        string standPath => b ??= Path.Combine(Bonelab_Folder, "StreamingAssets", "aa", "StandaloneWindows64");
        async void LevelLoad()
        {
            foreach (var catalogPath in targCatalogs)
            {
                print("loading catalog: " + catalogPath);
                await Addressables.LoadContentCatalogAsync(catalogPath).Task;
                print("Loaded!");
            }
            /*if (LoadAllScenesThatStartWith)
            {
                foreach (var catalog in Addressables.ResourceLocators)
                    if (catalog is ResourceLocationMap map)
                        foreach (var loc in map.Locations.Values)
                            if (loc.First().ResourceType == typeof(UnityEngine.ResourceManagement.ResourceProviders.SceneInstance))
                            {
                                string name = loc.First().InternalId.Split('/').Last();
                                if (name.StartsWith(StartWith))
                                {
                                    AsyncOperationHandle<SceneInstance> loadee;
                                    (string, AsyncOperationHandle<SceneInstance>) tuple = (
                                        name,
                                        loadee = Addressables.LoadSceneAsync(loc.First(), UnityEngine.SceneManagement.LoadSceneMode.Additive)
                                    );
                                    loadingSubScenes.Add(tuple);
                                    loadee.Completed += (a) => loadingSubScenes.Remove(tuple);
                                }
                            }
                return;
            }
            else*/
            {
                foreach (var guid in will_be_loaded)
                {
                    //var loc = ((ResourceLocationMap)Addressables.ResourceLocators.First(loc => loc is ResourceLocationMap)).Locations.Values.First(v => v.First().ProviderId == guid);
                    AsyncOperationHandle<SceneInstance> loadee;
                    (string, AsyncOperationHandle<SceneInstance>) tuple = (
                        guid,//loc.First().InternalId.Split('/').Last(),
                        loadee = Addressables.LoadSceneAsync(guid, UnityEngine.SceneManagement.LoadSceneMode.Additive)
                    );
                    loadingSubScenes.Add(tuple);
                    loadee.Completed += (a) => loadingSubScenes.Remove(tuple);
                }
            }
        }
        /*
        private List<string> LoadedScenes = new List<string>();
        private void OnLoad_Completed(AsyncOperationHandle<SceneInstance> obj)
        {
            foreach (var item in FindObjectsOfType<SceneChunk>())
            {
                foreach (var item1 in (MarrowScene[])item.GetType().GetField("sceneLayers", UltEventUtils.AnyAccessBindings).GetValue(item))
                {
                    Debug.Log(item.gameObject.name + " has " + item1.AssetGUID);
                    if (!LoadedScenes.Contains(item1.AssetGUID))
                    {
                        LoadedScenes.Add(item1.AssetGUID);
                        AsyncOperationHandle<SceneInstance> loadee; 
                        (string, AsyncOperationHandle<SceneInstance>) tuple = (
                            item.gameObject.name,
                            loadee = Addressables.LoadSceneAsync(item1.AssetGUID, UnityEngine.SceneManagement.LoadSceneMode.Additive)
                        );
                        loadingSubScenes.Add(tuple);
                        loadee.Completed += (a) => loadingSubScenes.Remove(tuple);
                    }
                }
            }
        }*/

        List<(string, AsyncOperationHandle<SceneInstance>)> loadingSubScenes = new();
        private void OnGUI()
        {
            if (!EditorApplication.isPlaying) return;
            if (loadingSubScenes.Count > 0)
            {
                foreach (var loader in loadingSubScenes)
                {
                    GUILayout.Label($"Loading {loader.Item1} at {(int)(loader.Item2.PercentComplete * 100)}%");
                }
            }
            else GUILayout.Label("\ndone");
        }
        private void Update()
        {
            if (!EditorApplication.isPlaying)
            {
                Bonelab_Folder = SDKProjectPreferences.MarrowGameInstallPaths[0];
                if (level != null && level.Crate != null)
                {
                    int keyCount = level.Crate.PersistentScenes.Count + level.Crate.ChunkScenes.Count + 1;
                    will_be_loaded ??= new string[keyCount];
                    if (will_be_loaded.Length != keyCount)
                        will_be_loaded = new string[keyCount];
                    will_be_loaded[0] = level.Crate.MainAsset.AssetGUID;
                    for (int i = 0; i < level.Crate.PersistentScenes.Count; i++)
                        will_be_loaded[i + 1] = level.Crate.PersistentScenes[i].AssetGUID;
                    for (int i = 0; i < level.Crate.ChunkScenes.Count; i++)
                        will_be_loaded[level.Crate.PersistentScenes.Count + 1 + i] = level.Crate.ChunkScenes[i].AssetGUID;
                }
                Pallet.Barcode = new Barcode(level?.Crate?.Pallet?.Barcode);
                targCatalogs = (List<string>)typeof(AssetWarehouse).GetField("loadedCatalogs", UltEvents.UltEventUtils.AnyAccessBindings).GetValue(AssetWarehouse.Instance);
            }
        }
#endif
    }
}