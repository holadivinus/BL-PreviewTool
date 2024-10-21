using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEditor;
#if UNITY_EDITOR
using SLZ.Marrow.Warehouse;
using SLZ.MarrowEditor;
#endif

[ExecuteAlways]
public class LevelLoader : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying) return;
#endif

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
#if UNITY_EDITOR
    [SerializeField][HideInInspector] PalletReference Pallet;
    [SerializeField] CrateReferenceT<LevelCrate> level;
#endif


    [SerializeField][HideInInspector] string Bonelab_Folder = "";
    [SerializeField][HideInInspector] string stupid_key = "";
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

#if UNITY_EDITOR
        print("loading crate: " + level.Barcode);
#endif
        loadingScene = Addressables.LoadSceneAsync(stupid_key);
    }
    AsyncOperationHandle<SceneInstance>? loadingScene = null;
    private void OnGUI()
    {
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying) return;
#endif
        if (loadingScene.HasValue && !loadingScene.Value.IsDone)
        {
            GUILayout.Label("Loading at " + (int)(loadingScene.Value.PercentComplete * 100) + "%");
        }
        else GUILayout.Label("\ndone");
    }
    private void Update()
    {
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying)
        {
            Bonelab_Folder = SDKProjectPreferences.MarrowGameInstallPaths[0];
            stupid_key = level?.Crate?.MainAsset?.AssetGUID;
            Pallet.Barcode = new Barcode(level?.Crate?.Pallet?.Barcode);
            targCatalogs = (List<string>)typeof(AssetWarehouse).GetField("loadedCatalogs", UltEvents.UltEventUtils.AnyAccessBindings).GetValue(AssetWarehouse.Instance);
        }
#endif
    }
}
