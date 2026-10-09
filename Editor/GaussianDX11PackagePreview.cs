using System;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Prempi.GaussianDX11.Editor
{
    // Editor-only helper for the frozen renderer. No Playmode or scene save.
    [InitializeOnLoad]
    internal static class GaussianDX11PackagePreview
    {
        private static NeonQuestGaussian cloud;
        private static MeshRenderer[] renderers;
        private static Material[] originals,temporary;
        private static Material sort;
        private static RenderTexture a,b;
        private static Vector3 lastDirection;
        private static double nextSort;
        private static double nextResume;
        private const string ResumeKey="Prempi.GaussianDX11.EditorPreview.Source";
        static GaussianDX11PackagePreview()
        {
            SceneView.duringSceneGui+=Draw;
            AssemblyReloadEvents.beforeAssemblyReload+=()=>{int id=cloud!=null?cloud.gameObject.GetInstanceID():0;StopCore();SessionState.SetInt(ResumeKey,id);};
            EditorSceneManager.sceneClosing+=(scene,removing)=>{if(cloud!=null&&cloud.gameObject.scene==scene)Stop();};
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode)Stop();};
            EditorApplication.update+=TryResume;
        }
        private static void TryResume()
        {
            if(cloud!=null||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<nextResume)return;
            nextResume=EditorApplication.timeSinceStartup+1;int id=SessionState.GetInt(ResumeKey,0);if(id==0)return;
            var root=EditorUtility.InstanceIDToObject(id) as GameObject;var source=root!=null?root.GetComponent<NeonQuestGaussian>():null;
            if(source!=null)StartPreview(source);
        }
        [MenuItem("Tools/Prempi Gaussian DX11/Preview selected cloud")]
        public static void Preview()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Use this menu in Edit mode");
            var selected=Selection.activeGameObject;
            var source=selected!=null?selected.GetComponentInParent<NeonQuestGaussian>():null;
            if(source==null&&selected!=null)source=selected.GetComponentInChildren<NeonQuestGaussian>(true);
            if(source==null)throw new InvalidOperationException("Select the Gaussian renderer prefab in the Hierarchy");
            StopCore();StartPreview(source);
        }
        private static void StartPreview(NeonQuestGaussian source)
        {
            cloud=source;SessionState.SetInt(ResumeKey,source.gameObject.GetInstanceID());UdonSharpEditorUtility.CopyUdonToProxy(cloud);
            renderers=cloud.cloud.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="NeonQuest/VRChat/Gaussian Sorted DX11").OrderBy(r=>r.sharedMaterial.renderQueue).ToArray();
            originals=renderers.Select(r=>r.sharedMaterial).ToArray();temporary=cloud.drawMaterials.Select(m=>new Material(m){hideFlags=HideFlags.HideAndDontSave}).ToArray();
            sort=new Material(cloud.sortMaterial){hideFlags=HideFlags.HideAndDontSave};a=Target();b=Target();Resume();SceneView.RepaintAll();
        }
        private static RenderTexture Target()
        {
            var target=new RenderTexture(cloud.width,cloud.capacity/cloud.width,0,RenderTextureFormat.RGFloat,RenderTextureReadWrite.Linear){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};target.Create();return target;
        }
        private static void Draw(SceneView view)
        {
            if(cloud==null||view.camera==null||Event.current.type!=EventType.Repaint||EditorApplication.timeSinceStartup<nextSort)return;
            Vector3 forward=view.camera.transform.forward;Matrix4x4 matrix=cloud.cloud.localToWorldMatrix;
            Vector3 direction=new Vector3(Vector3.Dot(matrix.GetColumn(0),forward),Vector3.Dot(matrix.GetColumn(1),forward),Vector3.Dot(matrix.GetColumn(2),forward));
            if(lastDirection!=Vector3.zero&&Vector3.Angle(direction,lastDirection)<.25f)return;
            lastDirection=direction;nextSort=EditorApplication.timeSinceStartup+.1;
            sort.SetTexture("_Positions",cloud.positions);sort.SetInt("_Width",cloud.width);sort.SetInt("_Count",cloud.count);sort.SetVector("_Direction",direction);Graphics.Blit(cloud.positions,a,sort,0);
            var current=a;var next=b;
            for(int stage=2;stage<=cloud.capacity;stage<<=1)for(int step=stage>>1;step>0;step>>=1)
            {sort.SetInt("_Stage",stage);sort.SetInt("_Step",step);Graphics.Blit(current,next,sort,1);var old=current;current=next;next=old;}
            foreach(var material in temporary)material.SetTexture("_Order",current);view.Repaint();
        }
        private static void Restore(){if(renderers==null)return;for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null)renderers[i].sharedMaterial=originals[i];}
        private static void Resume(){if(cloud==null||renderers==null)return;for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null)renderers[i].sharedMaterial=temporary[i];}
        internal static void SaveGuard(){Restore();if(cloud!=null)EditorApplication.delayCall+=Resume;}
        [MenuItem("Tools/Prempi Gaussian DX11/Stop temporary preview")]
        public static void Stop(){SessionState.SetInt(ResumeKey,0);StopCore();}
        private static void StopCore()
        {
            Restore();if(temporary!=null)foreach(var material in temporary)Object.DestroyImmediate(material);if(sort!=null)Object.DestroyImmediate(sort);if(a!=null)Object.DestroyImmediate(a);if(b!=null)Object.DestroyImmediate(b);
            cloud=null;renderers=null;originals=null;temporary=null;sort=null;a=b=null;lastDirection=Vector3.zero;
        }
    }
    internal sealed class GaussianDX11PackageSaveGuard:AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths){GaussianDX11PackagePreview.SaveGuard();return paths;}
    }
}
