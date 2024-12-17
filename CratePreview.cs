using SLZ.Marrow.Warehouse;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace BLPTool
{
    [ExecuteAlways]
    public class CratePreview : MonoBehaviour
    {
#if UNITY_EDITOR
        public bool ExplorablePreview = false;
        CrateSpawner CrateSpawner;
        string SpawnerGUID => CrateSpawner.spawnableCrateReference?.Crate?.MainAsset?.AssetGUID ?? string.Empty;
        string _lastID = string.Empty;

        GameObject _curPreview;
        MeshRenderer CrateMeshRenderer
        {
            get
            {
                if (this == null)
                    return null;
                if (_crateRenderer == null)
                    _crateRenderer = GetComponent<MeshRenderer>();
                return _crateRenderer;
            }
        }
        MeshRenderer _crateRenderer;
        AsyncOperationHandle<GameObject>? _curLoadTask;

        private void Start()
        {
            CrateSpawner = GetComponent<CrateSpawner>();
            this.enabled = BLPTool.PreviewDefault;
        }
        // Update is called once per frame
        void Update()
        {
            if (CrateSpawner == null)
            {
                DestroyImmediate(this, true);
                return;
            }
            if (_lastID != SpawnerGUID || _curPreview == null) // update / load preview
            {
                if (_curLoadTask.HasValue)
                {
                    _curLoadTask.Value.Completed -= OnLoad;
                    _curLoadTask = null;
                }
                if (SpawnerGUID == string.Empty)
                {
                    if (_curPreview != null)
                        DestroyImmediate(_curPreview, true);
                    if (CrateMeshRenderer != null)
                        CrateMeshRenderer.enabled = true;
                    _lastID = SpawnerGUID;
                }
                else
                {
                    // setup gameobject to hold the Preview
                    _curPreview = new GameObject("Preview");
                    _curPreview.hideFlags = ExplorablePreview ? HideFlags.DontSave : HideFlags.HideAndDontSave;
                    _curPreview.transform.SetParent(this.transform, false);

                    _lastID = SpawnerGUID;

                    // Local prefab!
                    if (CrateSpawner.spawnableCrateReference?.Crate?.MainAsset?.EditorAsset != null)
                    {
                        DisplayPreview((GameObject)PrefabUtility.InstantiatePrefab(CrateSpawner.spawnableCrateReference.Crate.MainAsset.EditorAsset));
                    }
                    else // Remote asset.
                    {
                        AsyncOperationHandle<GameObject> loadTask = Addressables.LoadAssetAsync<GameObject>(SpawnerGUID);
                        loadTask.Completed += OnLoad;
                        _curLoadTask = loadTask;
                    }
                }
            }
            if (_curLoadTask.HasValue)
                _curPreview.name = $"Preview: {(int)(_curLoadTask.Value.PercentComplete * 100)}%";
            if (_curPreview != null)
                _curPreview.hideFlags = ExplorablePreview ? HideFlags.DontSave : HideFlags.HideAndDontSave;
        }

        private void OnDisable()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                //delete other previews 
                if (transform.GetChild(i).name == "Preview")
                    DestroyImmediate(transform.GetChild(i).gameObject, true);
            }
            if (CrateMeshRenderer) CrateMeshRenderer.enabled = true;
        }

        public static bool InDrag; 
        public static List<Collider> DraggedCols = new();
        void OnLoad(AsyncOperationHandle<GameObject> op)
        {
            if (this == null) return;
            if (op.Result != null)
                DisplayPreview(Instantiate(op.Result));
        }
        private void DisplayPreview(GameObject spawn)
        {
            _curLoadTask = null;
            _curPreview.name = "Preview";
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                //delete other previews
                if (transform.GetChild(i) != _curPreview.transform && transform.GetChild(i).gameObject.name == "Preview")
                    DestroyImmediate(transform.GetChild(i).gameObject, true);
            }
            spawn.transform.parent = _curPreview.transform;
            spawn.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            spawn.transform.localPosition = Vector3.zero;
            if (CrateMeshRenderer != null)
                CrateMeshRenderer.enabled = false;

            if (InDrag)
            {
                DraggedCols = spawn.GetComponentsInChildren<Collider>().Where(c => c.enabled).ToList();
                foreach (var col in DraggedCols)
                    col.enabled = false;
            }
        }
        private void OnDestroy()
        {
            if (_curPreview != null)
                DestroyImmediate(_curPreview, true);
        }
#endif
    }
}