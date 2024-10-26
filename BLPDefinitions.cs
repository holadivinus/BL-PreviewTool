using SLZ.Marrow.Warehouse;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace BLPTool
{
#if UNITY_EDITOR
    public class BLPDefinitions : ScriptableObject
    {
        private static BLPDefinitions s_instance;
        public static BLPDefinitions Instance 
        { 
            get
            {
                if (s_instance == null)
                {
                    // first, search the assets folder in case it was moved
                    string found = AssetDatabase.FindAssets($"t:{nameof(BLPDefinitions)}").FirstOrDefault();
                    if (found != null)
                        return s_instance = AssetDatabase.LoadAssetAtPath<BLPDefinitions>(AssetDatabase.GUIDToAssetPath(found));
                    else
                    {
                        s_instance = CreateInstance<BLPDefinitions>();
                        AssetDatabase.CreateAsset(s_instance, "Assets/BLPDefinitions.asset");
                        return s_instance;
                    }
                } else return s_instance;
            }
        }

        [Serializable]
        public class MatLink
        {
            public Material AssetMat;
            [SerializeField] public SpawnableCrateReference Crate;
            public string SLZAssetName;
            public string SpawnerAssetGUID;
            [NonSerialized] public Material SLZMat;

            [Serializable] public class Changes
            {
                [Serializable]
                public class TexChange
                {
                    public string TexName;
                    public Texture ChangedTo;
                }
                [SerializeField] public List<TexChange> TexChanges = new();

                [Serializable]
                public class TexOffsetChange
                {
                    public string TexName;
                    public Vector2 ChangedTo;
                }
                [SerializeField] public List<TexOffsetChange> TexOffsetChanges = new();

                [Serializable]
                public class TexScaleChange
                {
                    public string TexName;
                    public Vector2 ChangedTo;
                }
                [SerializeField] public List<TexScaleChange> TexScaleChanges = new();

                [Serializable]
                public class FloatChange
                {
                    public string FloatName;
                    public float ChangedTo;
                }
                [SerializeField] public List<FloatChange> FloatChanges = new();

                [Serializable]
                public class IntChange
                {
                    public string IntName;
                    public int ChangedTo;
                }
                [SerializeField] public List<IntChange> IntChanges = new();

                [Serializable]
                public class ColorChange
                {
                    public string ColorName;
                    public Color ChangedTo;
                }
                [SerializeField] public List<ColorChange> ColorChanges = new();

            }
            [SerializeField] public Changes MatChanges;

            public async void Preview()
            {
                if (SLZMat == null)
                {
                    SLZMat = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m.ToString() == SLZAssetName && AssetDatabase.GetAssetPath(m) == "");
                    if (SLZMat == null)
                    {
                        if (!AssetMat.name.EndsWith(BLPTool.PreviewTag))
                            AssetMat.name += BLPTool.PreviewTag; // do this, so that we Preview() isn't called 6000 times


                        await Addressables.LoadAssetAsync<GameObject>(SpawnerAssetGUID).Task;
                        

                        SLZMat = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m.ToString() == SLZAssetName && AssetDatabase.GetAssetPath(m) == "");

                        // we need the original material to be rendered for full tex loading (hypthetically, lol)
                        if (SLZMat != null)
                        {
                            var newGobj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            newGobj.transform.transform.localScale = Vector3.one * .00000001f;
                            newGobj.GetComponent<Renderer>().sharedMaterial = SLZMat;
                            newGobj.hideFlags = HideFlags.HideAndDontSave;
                        }

                        AssetMat.name = AssetMat.name.Substring(0, AssetMat.name.Length - BLPTool.PreviewTag.Length);
                    }
                }
                if (SLZMat == null) return; // damn

                if (AssetMat.name.EndsWith(BLPTool.PreviewTag))
                    BLPTool.RevertMaterial(AssetMat);

                // use the SLZ Shader
                AssetMat.shader = SLZMat.shader;
                AssetMat.CopyPropertiesFromMaterial(SLZMat);

                AssetMat.name += BLPTool.PreviewTag;
               

                if (MatChanges != null) // apply "changes"
                {
                    foreach (var tc in MatChanges.TexChanges)
                        AssetMat.SetTexture(tc.TexName, tc.ChangedTo);
                    foreach (var tc in MatChanges.TexOffsetChanges)
                        AssetMat.SetTextureOffset(tc.TexName, tc.ChangedTo);
                    foreach (var tc in MatChanges.TexScaleChanges)
                        AssetMat.SetTextureScale(tc.TexName, tc.ChangedTo);
                    foreach (var cc in MatChanges.ColorChanges)
                        AssetMat.SetColor(cc.ColorName, cc.ChangedTo);
                    foreach (var ic in MatChanges.IntChanges)
                        AssetMat.SetInteger(ic.IntName, ic.ChangedTo);
                    foreach (var fc in MatChanges.FloatChanges)
                        AssetMat.SetFloat(fc.FloatName, fc.ChangedTo);
                }
            }
        }

        public List<MatLink> Links = new List<MatLink>(); 
    }
#endif
}
