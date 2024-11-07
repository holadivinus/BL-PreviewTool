using SLZ.Marrow.Warehouse;
using System;
using System.IO;
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
        public CrateSpawner CrateSpawner;
        [SerializeField] Renderer2DData MatArrHolder;
        public UltEventHolder[] Renderer2DDataReffers;
        public LifeCycleEvents StartEvent;
        public UltEventHolder ComparisonEvt;
        public UltEventHolder IntegrationEvt;
        public UltEventHolder MatPropApplyEvt;
        public LifeCycleEvents Triggerer;

        public void TargetMat(Material slzMat, Material assetMat)
        {
            // first figure out what crate makes this mat
            string targCrateGUID = MaterialDB.Instance.Data.First(d => d.MaterialNames.Contains(slzMat.ToString())).CrateGUID;
            SpawnableCrate c = AssetWarehouse.Instance.GetCrates<SpawnableCrate>().First(c => c?.MainAsset?.AssetGUID == targCrateGUID);
            CrateSpawner.spawnableCrateReference = new SpawnableCrateReference(c.Barcode);
            PrefabUtility.RecordPrefabInstancePropertyModifications(CrateSpawner);

            // since we directly instantiate via addressables, configure the "Triggerer" to trigger using the correct GUID
            Triggerer.StartEvent.PersistentCallsList[0].PersistentArguments[0].String = c.MainAsset?.AssetGUID;

            // Next, ensure we've got our own Renderer2DData
            if (MatArrHolder == null)
            {
                MatArrHolder = ScriptableObject.CreateInstance<Renderer2DData>();
                if (!Directory.Exists("Assets/BLPTool/MatScan Storages/")) Directory.CreateDirectory("Assets/BLPTool/MatScan Storages/");
                AssetDatabase.CreateAsset(MatArrHolder, "Assets/BLPTool/MatScan Storages/" + GUID.Generate().ToString() + ".asset");
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }

            // Lots of our events need an up-to-date refference to the Renderer2DData.
            foreach (var evt in Renderer2DDataReffers) 
            {
                foreach (var call in evt.Event.PersistentCallsList)
                    if (call.Target.GetType() == typeof(Renderer2DData))
                        callRetargetter.SetValue(call, MatArrHolder);
                PrefabUtility.RecordPrefabInstancePropertyModifications(evt);
            }

            callRetargetter.SetValue(StartEvent.AwakeEvent.PersistentCallsList[0], assetMat);
            PrefabUtility.RecordPrefabInstancePropertyModifications(StartEvent);


            // we must update our comparison event to look for this slz mat
            ComparisonEvt.Event.PersistentCallsList[2].PersistentArguments[1].String = slzMat.ToString();
            PrefabUtility.RecordPrefabInstancePropertyModifications(ComparisonEvt);

            // Lastly, we must replace refferences to "RefHolderMat" with our assetMat.
            foreach (var call in IntegrationEvt.Event.PersistentCallsList)
                if (call.Target == BLPTool.RefHolderMat)
                    callRetargetter.SetValue(call, assetMat);
            PrefabUtility.RecordPrefabInstancePropertyModifications(IntegrationEvt);

            // oh and now we need to setup an event that applys the saved properties
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

            // Okay, it should steal and setup the texture properly now.
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