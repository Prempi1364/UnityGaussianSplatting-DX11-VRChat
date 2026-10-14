using System;
using System.Collections.Generic;
using System.Linq;
#if VRC_SDK_VRCSDK3
using UdonSharpEditor;
using VRC.SDKBase;
#endif
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Prempi.GaussianDX11.Editor
{
    // Preview GPU resources and materials are temporary; original assets stay intact.
    [InitializeOnLoad]
    internal static class GaussianDX11PackagePreview
    {
        private sealed class State
        {
            internal NeonQuestGaussian source;
            internal MeshRenderer[] renderers;
            internal Material[] originals, materials;
            internal readonly Dictionary<Camera,CameraOrder> orders=new Dictionary<Camera,CameraOrder>();
            internal void Restore(){for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null)renderers[i].sharedMaterial=originals[i];}
            internal void Apply(){for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null)renderers[i].sharedMaterial=materials[i];}
            internal void Dispose()
            {
                Restore();foreach(var m in materials)Object.DestroyImmediate(m);
                foreach(var order in orders.Values)order.Dispose();orders.Clear();
            }
        }
        private sealed class CameraOrder
        {
            internal Material sort;
            internal RenderTexture a,b,published,current,next;
            internal Vector3 direction,pending;
            internal Vector3 position,pendingPosition;
            internal bool hasOrder,sorting;
            internal int stage,step;
            internal double lastAdvance;
            internal void Dispose()
            {
                Object.DestroyImmediate(sort);
                foreach(var rt in new[]{a,b,published}){rt.Release();Object.DestroyImmediate(rt);}
            }
        }
        private static readonly Dictionary<int,State> states=new Dictionary<int,State>();
        private static double nextDiscover;
        private static bool paused;
        static GaussianDX11PackagePreview()
        {
            Camera.onPreCull+=Render;EditorApplication.update+=Update;
            AssemblyReloadEvents.beforeAssemblyReload+=Clear;EditorApplication.quitting+=Clear;
            EditorSceneManager.sceneClosing+=(s,r)=>Clear();EditorApplication.playModeStateChanged+=s=>Clear();
        }
        private static bool CanPreview()
        {
            // Temporary editor materials must be restored before Unity clones the
            // scene for Play. Do not recreate them while ClientSim initializes.
            if(paused||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||BuildPipeline.isBuildingPlayer||Application.isBatchMode)return false;
#if VRC_SDK_VRCSDK3
            if(VRC.SDKBase.Editor.VRC_SdkBuilder.ActiveBuildType!=VRC.SDKBase.Editor.VRC_SdkBuilder.BuildType.None)return false;
            if(VRCSdkControlPanel.window!=null&&VRCSdkControlPanel.window.PanelState!=VRC.SDKBase.Editor.SdkPanelState.Idle)return false;
#endif
            return true;
        }
        private static void Update()
        {
            if(!CanPreview()){if(states.Count>0)Clear();return;}
            foreach(int key in states.Where(p=>p.Value.source==null||!p.Value.source.gameObject.activeInHierarchy).Select(p=>p.Key).ToArray())
            {states[key].Dispose();states.Remove(key);}
            foreach(var state in states.Values)
                foreach(var camera in state.orders.Keys.Where(c=>c==null).ToArray())
                {state.orders[camera].Dispose();state.orders.Remove(camera);}
            if(EditorApplication.timeSinceStartup<nextDiscover)return;nextDiscover=EditorApplication.timeSinceStartup+1;
            bool discovered=false;
            foreach(var source in Resources.FindObjectsOfTypeAll<NeonQuestGaussian>())
            {
                if(!source.gameObject.scene.IsValid()||!source.gameObject.scene.isLoaded||!source.gameObject.activeInHierarchy||EditorUtility.IsPersistent(source)||states.ContainsKey(source.GetInstanceID()))continue;
#if VRC_SDK_VRCSDK3
                if(!EditorApplication.isPlaying)UdonSharpEditorUtility.CopyUdonToProxy(source);
#endif
                if(source.cloud==null||source.positions==null||source.sortMaterial==null||source.drawMaterials==null||source.width<1||source.capacity<2)continue;
                var renderers=source.cloud.GetComponentsInChildren<MeshRenderer>(true).Where(r=>source.drawMaterials.Contains(r.sharedMaterial)).ToArray();if(renderers.Length==0)continue;
                var originals=renderers.Select(r=>r.sharedMaterial).ToArray();
                var state=new State{source=source,renderers=renderers,originals=originals,
                    materials=originals.Select(m=>new Material(m){hideFlags=HideFlags.HideAndDontSave}).ToArray()};
                state.Apply();states.Add(source.GetInstanceID(),state);
                discovered=true;
            }
            if(discovered)SceneView.RepaintAll();
        }
        private static RenderTexture Target(NeonQuestGaussian source)
        {
            var rt=new RenderTexture(source.width,source.capacity/source.width,0,RenderTextureFormat.RGFloat,RenderTextureReadWrite.Linear)
            {filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};rt.Create();return rt;
        }
        private static void Render(Camera camera)
        {
            if(!CanPreview()||camera==null||camera.cameraType==CameraType.Preview)return;
            var previousTarget=RenderTexture.active;
            try
            {
            var planes=GeometryUtility.CalculateFrustumPlanes(camera);
            foreach(var state in states.Values)
            {
                var source=state.source;
                if(source==null||!source.gameObject.activeInHierarchy||(source.worldRoot!=null&&!source.worldRoot.activeInHierarchy))continue;
                if(!state.renderers.Any(r=>r!=null&&r.enabled&&GeometryUtility.TestPlanesAABB(planes,r.bounds)))continue;
                // Scene, Game and auxiliary cameras must never replace each other's
                // order. The material binding is switched immediately before drawing.
                CameraOrder order;
                if(!state.orders.TryGetValue(camera,out order))
                {
                    order=new CameraOrder{sort=new Material(source.sortMaterial){hideFlags=HideFlags.HideAndDontSave},a=Target(source),b=Target(source),published=Target(source)};
                    state.orders.Add(camera,order);
                }
                var matrix=source.cloud.localToWorldMatrix;var forward=camera.transform.forward;
                var direction=new Vector3(Vector3.Dot(matrix.GetColumn(0),forward),Vector3.Dot(matrix.GetColumn(1),forward),Vector3.Dot(matrix.GetColumn(2),forward));
                if(!order.sorting&&(!order.hasOrder||Vector3.Angle(direction,order.direction)>=source.angleThreshold))
                {
                    order.pending=direction;order.sort.SetTexture("_Positions",source.positions);order.sort.SetInt("_Width",source.width);order.sort.SetInt("_Count",source.count);order.sort.SetVector("_Direction",direction);
                    order.pendingPosition=camera.transform.position;
                    order.sort.SetInt("_SortMode",0);
                    Graphics.Blit(source.positions,order.a,order.sort,0);order.current=order.a;order.next=order.b;order.stage=2;order.step=1;order.sorting=true;
                }
                double now=EditorApplication.timeSinceStartup;
                if(order.sorting&&now-order.lastAdvance>=1.0/60)
                {
                    order.lastAdvance=now;
                    // Finish the first order before this camera can draw an unrelated
                    // imported or another camera's order. Later changes keep the budget.
                    int budget=order.hasOrder?Mathf.Clamp(source.passesPerFrame,1,32):512;
                    for(int i=0;i<budget&&order.sorting;i++)
                    {
                        int compareSteps=order.step>=4?3:order.step>=2?2:1;
                        order.sort.SetInt("_Stage",order.stage);order.sort.SetInt("_Step",order.step);Graphics.Blit(order.current,order.next,order.sort,compareSteps==3?4:compareSteps==2?3:1);
                        var old=order.current;order.current=order.next;order.next=old;order.step>>=compareSteps;
                        if(order.step==0){order.stage<<=1;order.step=order.stage>>1;}
                        if(order.stage>source.capacity)
                        {
                            Graphics.Blit(order.current,order.published,order.sort,2);
                            order.direction=order.pending;order.position=order.pendingPosition;order.hasOrder=true;order.sorting=false;
                        }
                    }
                }
                foreach(var m in state.materials)m.SetTexture("_Order",order.hasOrder?order.published:state.originals[0].GetTexture("_Order"));
                if(camera.cameraType==CameraType.SceneView&&order.sorting)SceneView.RepaintAll();
            }
            }
            finally{RenderTexture.active=previousTarget;}
        }
        [MenuItem("Tools/Prempi Gaussian DX11/Preview selected cloud")]
        public static void Preview(){paused=false;nextDiscover=0;Update();SceneView.RepaintAll();}
        [MenuItem("Tools/Prempi Gaussian DX11/Stop temporary preview")]
        public static void Stop(){paused=true;Clear();}
        private static void Clear(){foreach(var state in states.Values)state.Dispose();states.Clear();nextDiscover=0;}
        internal static void SaveGuard()
        {
            foreach(var state in states.Values)state.Restore();
            EditorApplication.delayCall+=()=>{if(CanPreview())foreach(var state in states.Values)state.Apply();};
        }
    }
    internal sealed class GaussianDX11PackageSaveGuard:AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths){GaussianDX11PackagePreview.SaveGuard();return paths;}
    }
}
