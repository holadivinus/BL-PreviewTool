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
    public class CamStalk : MonoBehaviour
    {
#if UNITY_EDITOR
        private void Update()
        {
            if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
            {
                transform.SetParent(SceneView.lastActiveSceneView.camera.transform, false);
                transform.localPosition = Vector3.zero;
            }
        }
#endif
    }
}