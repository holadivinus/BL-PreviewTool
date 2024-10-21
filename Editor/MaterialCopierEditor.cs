using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System.Linq;
using System.Drawing.Printing;
using Cysharp.Threading.Tasks.Triggers;

[CustomEditor(typeof(MaterialCopier))]
public class MaterialCopierEditor : Editor
{
    private bool SelectingMat;
    public override void OnInspectorGUI()
    {
        MaterialCopier mc = (MaterialCopier)target;
        if (mc.gameObject.scene.name == mc.gameObject.name || !mc.real)
            return;
        GUIStyle richLabel = new GUIStyle(EditorStyles.label);
        richLabel.richText = true;
        CratePreview mcPreview = mc.GetComponentInChildren<CratePreview>();

        GUILayout.Label("<b>This Script/UltEvent setup will:</b>", richLabel);
        GUILayout.Label(" <b>A.</b> Spawn a Spawnable (Specified here or in <color=orange>Logic<color=lightblue>/</color>Var<color=lightblue>/</color>Spawnable</color>)", richLabel);
        GUILayout.Label(" <b>B.</b> Steal its <i>Material</i> by name (Specified here)", richLabel);
        GUILayout.Label(" <b>C.</b> Copy the SLZ Material data into one of Your materials (Specified here)", richLabel);
        GUILayout.Label(" <b>D.</b> Call <color=orange>Logic<color=lightblue>/</color>Events<color=lightblue>/</color>OnMaterialCopied</color>, where you can customize the copied mat.", richLabel);
        GUILayout.Label("", richLabel);
        GUILayout.Label("Your target material will preview correctly in the editor, and <color=orange>Logic<color=lightblue>/</color>Events<color=lightblue>/</color>OnMaterialCopied</color> will", richLabel);
        GUILayout.Label("be executed each Reimport.", richLabel);
        GUILayout.Space(30);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Copy Target:", GUILayout.ExpandWidth(false));
        mc.targetMaterial = (Material)EditorGUILayout.ObjectField(mc.targetMaterial, typeof(Material), false);

        GUILayout.EndHorizontal();
        if (!SelectingMat)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("SLZ Target:", GUILayout.ExpandWidth(false));
            if (mcPreview != null && mcPreview.transform.childCount > 0)
                if (GUILayout.Button("Select")) SelectingMat = true;
            mc.slzMaterialName = EditorGUILayout.TextField(mc.slzMaterialName);
            if (mc.targetMaterial != null && mc.targetMaterial.name.EndsWith(MaterialCopier.PreviewTag))
                if (GUILayout.Button("Reimport"))
                {
                    // reimport mat
                    Material m = mc.targetMaterial;
                    mc.targetMaterial =null;
                    mc.targetMaterial = m;
                }
            GUILayout.EndHorizontal();
        } else
        {
            if (mcPreview == null || mcPreview.transform.childCount == 0)
            {
                SelectingMat = false;
                Repaint();
                return;
            }
            SelectingMat = false; // in case of error
            // find possible mats
            List<Material> mats = new();
            foreach (var renderer in mcPreview.GetComponentsInChildren<Renderer>(true))
                if (renderer.gameObject != mcPreview.gameObject) 
                    mats.AddRange(renderer.sharedMaterials);
            GUILayout.BeginVertical();
            foreach (Material mat in mats.Distinct())
                if (mat == null)
                    continue;
            else
                if (GUILayout.Button(mat.name)) 
                { 
                    mc.slzMaterialName = mat.ToString();
                    SelectingMat = false;
                    Repaint();
                    return;
                };
            GUILayout.EndVertical();
            SelectingMat = true;
        }
    }
}
