using SLZ.Marrow.Warehouse;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UltEvents;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BLPTool
{
    [ExecuteAlways]
    public class MaterialCopier : MonoBehaviour
    {
#if UNITY_EDITOR
        private FieldInfo targSetter => typeof(PersistentCall).GetField("_Target", UltEventUtils.AnyAccessBindings);
        public Material targetMaterial
        {
            get => _targMat;
            set
            {
                if (_targMat == value) return;
                Revert(_targMat);
                if (TargMatEvent != null)
                {
                    Material orig = _targMat;
                    if (orig == null) orig = RefHolderMat;
                    Material newMat = value;
                    if (newMat == null) newMat = RefHolderMat;
                    foreach (var targMatEvent in new[] { TargMatEvent.Event, CopyMatEvent.EnableEvent })
                        foreach (var call in targMatEvent.PersistentCallsList)
                            if (call.Target == orig)
                            {
                                targSetter.SetValue(call, newMat);
                                PrefabUtility.RecordPrefabInstancePropertyModifications(TargMatEvent);
                                PrefabUtility.RecordPrefabInstancePropertyModifications(CopyMatEvent);
                            }
                }
                _targMat = value;
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
                Preview(value);
            }
        }
        public UltEventHolder TargMatEvent;
        public LifeCycleEvents CopyMatEvent;
        private Material _curSLZMat;
        private Material FindSLZMat() => Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(mat => mat.ToString() == slzMaterialName);
        public Material _targMat;
        public UltEventHolder MatNameEvent;
        public Renderer2DData Storager
        {
            get
            {
                if (sr == null)
                {
                    sr = ScriptableObject.CreateInstance<Renderer2DData>();
                    AssetDatabase.CreateAsset(sr, MatscanStoragePath.Replace("LevelLoader.cs", "MatScan Storages/" + GUID.Generate().ToString() + ".asset"));
                    PrefabUtility.RecordPrefabInstancePropertyModifications(this);
                }
                return sr;
            }
        }
        public UltEventHolder[] StoragerReffers;
        [SerializeField] Renderer2DData sr;
        public string slzMaterialName
        {
            get => MatNameEvent.Event.PersistentCallsList[2].PersistentArguments[1].String;
            set
            {
                MatNameEvent.Event.PersistentCallsList[2].PersistentArguments[1].String = value;
                PrefabUtility.RecordPrefabInstancePropertyModifications(MatNameEvent);
            }
        }
        private void Awake()
        {
            Debug.Log(DefaultMat);
            Debug.Log(RefHolderMat);
        }

        static Material DefaultMat => s_dm ??= AssetDatabase.LoadAssetAtPath<Material>(MatscanStoragePath.Replace("LevelLoader.cs", "DefaultMat.mat"));
        private static string sp;
        static string MatscanStoragePath => AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(UnityEngine.Object.FindObjectOfType<MaterialCopier>(true))).Replace("MaterialCopier.cs", "LevelLoader.cs");
        private static Material s_dm;
        static Material RefHolderMat => s_rm ??= AssetDatabase.LoadAssetAtPath<Material>(MatscanStoragePath.Replace("LevelLoader.cs", "RefHolderMat.mat"));
        private static Material s_rm;

        public const string PreviewTag = " (IN PREVIEW MODE)";
        private void Preview(Material mat)
        {
            if (mat == null)
                return;
            bool currentlyPreviewing = mat.name.EndsWith(PreviewTag);
            if (currentlyPreviewing)
            {
                Revert(mat);
                currentlyPreviewing = false;
            }


            if (_curSLZMat != null) // the SLZ mat is valid
            {
                // find the local version of the external shader
                Shader projShader = Shader.Find(_curSLZMat.shader.name);
                if (projShader != null)
                    mat.shader = projShader;
                else mat.shader = DefaultMat.shader;

                if (mat.shader.name == _curSLZMat.shader.name)
                    mat.CopyPropertiesFromMaterial(_curSLZMat);

                mat.name += PreviewTag;
                doLater.Enqueue(TargMatEvent.Event.Invoke);
            }
        }
        private Queue<Action> doLater = new();
        public WhichCrate wc;
        public CrateSpawner spawner;
        public static void Revert(Material mat)
        {
            if (mat == null)
                return;
            bool currentlyPreviewing = mat.name.EndsWith(PreviewTag);
            if (currentlyPreviewing)
            {
                // reset the material
                Shader defMatShader = DefaultMat.shader;
                DefaultMat.shader = mat.shader;
                mat.CopyPropertiesFromMaterial(DefaultMat);
                DefaultMat.shader = defMatShader;

                mat.name = mat.name.Substring(0, mat.name.Length - PreviewTag.Length);
            }
        }
        [NonSerialized] public bool real;
        private void Update()
        {
            real = true;
            if (gameObject.scene.name == gameObject.name)
                return;

            if (spawner.spawnableCrateReference != null && spawner.spawnableCrateReference.Crate != null)
                if (wc.CrateRef != null && wc.CrateRef.Crate != null)
                {
                    spawner.spawnableCrateReference.Barcode = new Barcode(wc.CrateRef.Barcode);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);
                }

            if (targetMaterial != null) 
            {
                if ((_curSLZMat != null && _curSLZMat.ToString() != slzMaterialName) || _curSLZMat == null)
                {
                    _curSLZMat = FindSLZMat();
                    if (_curSLZMat != null) Preview(targetMaterial);
                    else Revert(targetMaterial);
                }
                if (!targetMaterial.name.EndsWith(PreviewTag) && _curSLZMat != null)
                    Preview(targetMaterial);
                else if (_curSLZMat == null)
                    Revert(targetMaterial);
            }
            if (doLater.Count > 0)
                doLater.Dequeue()?.Invoke();
            datas[this] = Storager;
            if (datas.Any(d => d.Value == Storager && d.Key != this))
                sr = null;

            foreach (var storEvt in StoragerReffers)
                foreach (var evt in storEvt.Event.PersistentCallsList)
                    if (evt.Target != Storager)
                        targSetter.SetValue(evt, Storager);
        }
        private static Dictionary<MaterialCopier, Renderer2DData> datas = new();
#endif
    }
}