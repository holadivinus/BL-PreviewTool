using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BLPTool
{
#if UNITY_EDITOR
    /// <summary>
    /// Maps Spawnable Crates to contained Materials
    /// </summary>
    public class MaterialDB : ScriptableObject
    {
        private static MaterialDB s_instance;
        public static MaterialDB Instance
        {
            get
            {
                if (s_instance == null)
                    return s_instance = AssetDatabase.LoadAssetAtPath<MaterialDB>(BLPTool.PluginPath + "SLZMaterialDB.asset");
                else return s_instance;
            }
        }
        [Serializable] public class CrateMats
        {
            public string CrateGUID;
            public List<string> MaterialNames;
            public List<string> Shaders;
        }
        [SerializeField] public List<CrateMats> Data = new();

        [SerializeField] public List<string> AllMaterials = new();
        [SerializeField] public List<string> AllShaders = new();
    }
#endif
} 
