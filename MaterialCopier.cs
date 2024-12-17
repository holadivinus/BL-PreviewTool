using JetBrains.Annotations;
using SLZ.Marrow.Utilities;
using SLZ.Marrow.Warehouse;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UltEvents;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering.Universal;

namespace BLPTool
{
    [ExecuteAlways]
    public class MaterialCopier : MonoBehaviour
    {
#if UNITY_EDITOR
        private static Assembly _xrAssmb;
        public static Assembly XRAssembly => _xrAssmb ??= AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(ass => ass.FullName.StartsWith("Unity.XR.Interaction.Toolkit"));
        public static Type GetExtType(string name, Assembly ass = null)
        {
            ass ??= XRAssembly;
            if (ass == null)
                return null;
            foreach (Type t in ass.GetTypes())
                try
                {
                    if (t.Name == name) return t;
                }
                catch (TypeLoadException) { }
            return null;
        }
        public static Type XRType = GetExtType("XRInteractorAffordanceStateProvider");

        public CrateSpawner CrateSpawner;
        public UltEventHolder MatPropApplyEvt; 

        private static string GetPath(GameObject go)
        {
            string path = go.name;
            while (go.transform.parent != null)
            {
                go = go.transform.parent.gameObject;
                path = $"{go.name}/{path}";
            }
            return path;
        }

        public void TargetMat(Material slzMat, Material assetMat)
        {
            // first figure out what crate makes this mat
            string targCrateGUID = MaterialDB.Instance.Data.First(d => d.MaterialNames.Contains(slzMat.ToString())).CrateGUID;
            SpawnableCrate c = AssetWarehouse.Instance.GetCrates<SpawnableCrate>().First(c => c?.MainAsset?.AssetGUID == targCrateGUID);
            CrateSpawner.spawnableCrateReference = new SpawnableCrateReference(c.Barcode);
            PrefabUtility.RecordPrefabInstancePropertyModifications(CrateSpawner);


            // next get the ""spawned"" prefab
            // at this point we're saving the scene that had a stubbed in mat, meaning it's garuanteed a matlink has the spawned prefab.
            var llink = BLPDefinitions.Instance.Links.FirstOrDefault(l => l.AssetMat == assetMat);
            GameObject spawnedGobj = llink.SourceLoaded; 
            if (object.ReferenceEquals(spawnedGobj, null)) { return; }

            // find the renderer using the slzMat 
            Renderer r = spawnedGobj.GetComponentsInChildren<Renderer>(true).FirstOrDefault(mr => mr.sharedMaterials.Any(sr => sr.ToString() == slzMat.ToString()));                                                        
            slzMat = r.sharedMaterials.FirstOrDefault(mat => mat.ToString() == slzMat.ToString());
            if (object.ReferenceEquals(r, null)) { return; }

            // make all those ults get the right gobj
            CrateSpawner.onSpawnEvent.PersistentCallsList[1].PersistentArguments[0].String = "/{0}" + GetPath(r.gameObject).Substring(GetPath(spawnedGobj).Length);

            // get the correct shared mat idx from the renderer
            CrateSpawner.onSpawnEvent.PersistentCallsList[29].PersistentArguments[1].Int = Array.IndexOf(r.sharedMaterials, slzMat);

            // replace instances of "RefHolderMat" with asset mat
            foreach (PersistentCall pcall in CrateSpawner.onSpawnEvent.PersistentCallsList)
            {
                if (pcall.Target == BLPTool.RefHolderMat) 
                    callRetargetter.SetValue(pcall, assetMat);
            }

            // decorate paste mat
            MatPropApplyEvt.Event.PersistentCallsList.Clear();
            var link = BLPDefinitions.Instance.Links.First(l => l.AssetMat == assetMat);
            if (link.MatChanges != null)
            {
                MethodInfo meth = null;

                // Textures
                meth = typeof(Material).GetMethod(nameof(Material.SetTexture), new Type[] { typeof(string), typeof(Texture) });
                foreach (var tc in link.MatChanges.TexChanges)
                {
                    var call = new PersistentCall(meth, assetMat);
                    call.PersistentArguments[0].String = tc.TexName;
                    call.PersistentArguments[1].Object = tc.ChangedTo;
                    MatPropApplyEvt.Event.PersistentCallsList.Add(call);
                }

                // Textures Offsets
                meth = typeof(Material).GetMethod(nameof(Material.SetTextureOffset), new Type[] { typeof(string), typeof(Vector2) });
                foreach (var tc in link.MatChanges.TexOffsetChanges)
                {
                    var call = new PersistentCall(meth, assetMat);
                    call.PersistentArguments[0].String = tc.TexName;
                    call.PersistentArguments[1].Vector2 = tc.ChangedTo;
                    MatPropApplyEvt.Event.PersistentCallsList.Add(call);
                }

                // Textures Scalings
                meth = typeof(Material).GetMethod(nameof(Material.SetTextureScale), new Type[] { typeof(string), typeof(Vector2) });
                foreach (var tc in link.MatChanges.TexScaleChanges)
                {
                    var call = new PersistentCall(meth, assetMat);
                    call.PersistentArguments[0].String = tc.TexName;
                    call.PersistentArguments[1].Vector2 = tc.ChangedTo;
                    MatPropApplyEvt.Event.PersistentCallsList.Add(call);
                }

                // Colors
                meth = typeof(Material).GetMethod(nameof(Material.SetColor), new Type[] { typeof(string), typeof(Color) });
                foreach (var tc in link.MatChanges.ColorChanges)
                {
                    var call = new PersistentCall(meth, assetMat);
                    call.PersistentArguments[0].String = tc.ColorName;
                    call.PersistentArguments[1].Color = tc.ChangedTo;
                    MatPropApplyEvt.Event.PersistentCallsList.Add(call);
                }

                // Floats
                meth = typeof(Material).GetMethod(nameof(Material.SetFloat), new Type[] { typeof(string), typeof(float) });
                foreach (var tc in link.MatChanges.FloatChanges)
                {
                    var call = new PersistentCall(meth, assetMat);
                    call.PersistentArguments[0].String = tc.FloatName;
                    call.PersistentArguments[1].Float = tc.ChangedTo;
                    MatPropApplyEvt.Event.PersistentCallsList.Add(call);
                }

                // Ints
                meth = typeof(Material).GetMethod(nameof(Material.SetInt), new Type[] { typeof(string), typeof(int) });
                foreach (var tc in link.MatChanges.IntChanges)
                {
                    var call = new PersistentCall(meth, assetMat);
                    call.PersistentArguments[0].String = tc.IntName;
                    call.PersistentArguments[1].Float = tc.ChangedTo;
                    MatPropApplyEvt.Event.PersistentCallsList.Add(call);
                }

                PrefabUtility.RecordPrefabInstancePropertyModifications(MatPropApplyEvt);
            }
        }

        private void Update()
        {
            var rend = CrateSpawner.GetComponent<Renderer>();
            if (rend)
                rend.enabled = false;

            var prev = CrateSpawner.GetComponent<CratePreview>();
            if (prev)
                prev.enabled = false;
        }

        private FieldInfo callRetargetter = typeof(PersistentCall).GetField("_Target", UltEventUtils.AnyAccessBindings);
#endif
    }
}