// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Prempi.GaussianDX11.Editor
{
    // A completed runtime sort is transient GPU data, never a saved asset order.
    [InitializeOnLoad]
    internal sealed class GaussianDX11OrderSaveGuard : AssetModificationProcessor
    {
        static readonly Dictionary<string,string> Initial=new Dictionary<string,string>();
        static GaussianDX11OrderSaveGuard()
        {
            EditorApplication.delayCall+=Capture;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode)Capture();if(state==PlayModeStateChange.EnteredEditMode)Restore();};
        }
        static bool IsSplat(Material m)=>m!=null&&m.shader!=null&&(m.shader.name=="NeonQuest/VRChat/Gaussian Sorted DX11"||m.shader.name=="Prempi/Gaussian DX11/Sorted SH");
        static void Capture()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:Material"))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(!IsSplat(material))continue;
                var texture=material.GetTexture("_Order") as Texture2D;if(texture==null)continue;
                string initial=AssetDatabase.GetAssetPath(texture);if(!string.IsNullOrEmpty(initial))Initial[path]=initial;
            }
        }
        static void Restore()
        {
            foreach(var entry in Initial)
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(entry.Key);var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(entry.Value);
                if(!IsSplat(material)||texture==null||material.GetTexture("_Order")==texture)continue;
                material.SetTexture("_Order",texture);EditorUtility.SetDirty(material);
            }
        }
        static string[] OnWillSaveAssets(string[] paths)
        {
            if(!EditorApplication.isPlayingOrWillChangePlaymode){Restore();Capture();}return paths;
        }
    }
}
