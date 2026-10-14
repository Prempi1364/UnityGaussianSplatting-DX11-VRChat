using UnityEngine;
#if VRC_SDK_VRCSDK3
using UdonSharp;
using VRC.SDKBase;
using VRC.SDK3.Rendering;
#endif

// Local GPU sorting is visual state; each visitor needs their own viewing order.
#if VRC_SDK_VRCSDK3
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class NeonQuestGaussian : UdonSharpBehaviour
#else
public class NeonQuestGaussian : MonoBehaviour
#endif
{
    public Transform cloud;
    public GameObject worldRoot;
    public Material sortMaterial;
    public Material[] drawMaterials;
    public Texture positions;
    public RenderTexture workA;
    public RenderTexture workB;
    public RenderTexture published;
    public int count;
    public int capacity;
    public int width=1024;
    public int passesPerFrame=32;
    public float angleThreshold=2f;
    private RenderTexture current;
    private RenderTexture next;
    private int stage;
    private int step;
    private bool sorting;
    private bool hasOrder;
    private bool initialized;
    private Vector3 orderDirection, pendingDirection;
    private bool loggedReady;
    private int completedSorts;
    private int sortPassesExecuted;
#if !VRC_SDK_VRCSDK3
    private Material[] sourceMaterials;
    private MeshRenderer[] unityRenderers;
#endif

    private void Start()
    {
        if(cloud==null||sortMaterial==null||positions==null||workA==null||workB==null||published==null||drawMaterials==null||count<1||width<1||capacity<2)return;
#if !VRC_SDK_VRCSDK3
        // Ordinary Unity instances must not write to shared prefab resources.
        sourceMaterials=drawMaterials;
        drawMaterials=new Material[sourceMaterials.Length];
        unityRenderers=cloud.GetComponentsInChildren<MeshRenderer>(true);
        for(int i=0;i<drawMaterials.Length;i++)
        {
            drawMaterials[i]=new Material(sourceMaterials[i]);
            foreach(var renderer in unityRenderers)if(renderer.sharedMaterial==sourceMaterials[i])renderer.sharedMaterial=drawMaterials[i];
        }
        sortMaterial=new Material(sortMaterial);
        workA=new RenderTexture(workA.descriptor){filterMode=FilterMode.Point};
        workB=new RenderTexture(workB.descriptor){filterMode=FilterMode.Point};
        published=new RenderTexture(published.descriptor){filterMode=FilterMode.Point};
#endif
        // SDK scene processing can remap renderer materials separately from the
        // serialized Udon array. Resolve the materials that actually draw slots.
        MeshRenderer[] renderers=cloud.GetComponentsInChildren<MeshRenderer>(true);
        Material[] visibleMaterials=new Material[drawMaterials.Length];
        for(int i=0;i<visibleMaterials.Length;i++)
        {
            for(int r=0;r<renderers.Length;r++)
            {
                if(renderers[r].gameObject.name=="Gaussian draw "+i)
                {
                    visibleMaterials[i]=renderers[r].sharedMaterial;
                    break;
                }
            }
            if(visibleMaterials[i]==null){Debug.LogError("GAUSSIAN_RUNTIME_MISSING_DRAW_SLOT "+i);return;}
            visibleMaterials[i].renderQueue=3000+i;
        }
        drawMaterials=visibleMaterials;
        sortMaterial.SetTexture("_Positions",positions);
        sortMaterial.SetInt("_Width",width);sortMaterial.SetInt("_Count",count);
        for(int i=0;i<drawMaterials.Length;i++){drawMaterials[i].SetFloat("_Width",width);drawMaterials[i].SetFloat("_Count",count);}
        workA.Create();workB.Create();published.Create();
        initialized=true;
    }
    private void OnEnable(){hasOrder=false;sorting=false;}
    private void OnDisable(){sorting=false;hasOrder=false;}
#if !VRC_SDK_VRCSDK3
    private void OnDestroy()
    {
        if(!initialized)return;
        for(int i=0;i<drawMaterials.Length;i++)
        {
            foreach(var renderer in unityRenderers)if(renderer!=null&&renderer.sharedMaterial==drawMaterials[i])renderer.sharedMaterial=sourceMaterials[i];
            Destroy(drawMaterials[i]);
        }
        Destroy(sortMaterial);workA.Release();workB.Release();published.Release();Destroy(workA);Destroy(workB);Destroy(published);
    }
#endif
    private void Update()
    {
        if(!initialized||cloud==null||(worldRoot!=null&&!worldRoot.activeInHierarchy))return;
#if VRC_SDK_VRCSDK3
        if(!Utilities.IsValid(Networking.LocalPlayer))return;
        Vector3 forward=Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation*Vector3.forward;
#else
        Camera camera=Camera.main;
        if(camera==null)return;
        Vector3 forward=camera.transform.forward;
#endif
        Matrix4x4 matrix=cloud.localToWorldMatrix;
        Vector3 direction=new Vector3(Vector3.Dot(matrix.GetColumn(0),forward),Vector3.Dot(matrix.GetColumn(1),forward),Vector3.Dot(matrix.GetColumn(2),forward));
        // Overlapping projected splats must be composited by view depth.
        if(!sorting&&(!hasOrder||Vector3.Angle(direction,orderDirection)>=angleThreshold))BeginSort(direction);
        if(!sorting)return;
        int budget=Mathf.Clamp(passesPerFrame,1,32);
        for(int i=0;i<budget&&sorting;i++)
        {
            sortMaterial.SetInt("_Stage",stage);sortMaterial.SetInt("_Step",step);
            SortBlit(current,next,1);
            RenderTexture old=current;current=next;next=old;
            step>>=1;
            if(step==0){stage<<=1;step=stage>>1;}
            if(stage>capacity)
            {
                // Publish only a completed global order. Intermediate bitonic runs
                // are intentionally never sampled by the visible renderer.
                SortBlit(current,published,2);
                for(int m=0;m<drawMaterials.Length;m++)drawMaterials[m].SetTexture("_Order",published);
                orderDirection=pendingDirection;hasOrder=true;sorting=false;
                completedSorts++;
                if(!loggedReady){loggedReady=true;Debug.Log("GAUSSIAN_RUNTIME_ORDER_READY count="+count+" budget="+budget+" viewDepth=True texelPublish=True");}
            }
        }
    }
    private void SortBlit(Texture source,RenderTexture destination,int pass)
    {
        sortPassesExecuted++;
#if VRC_SDK_VRCSDK3
        VRCGraphics.Blit(source,destination,sortMaterial,pass);
#else
        Graphics.Blit(source,destination,sortMaterial,pass);
#endif
    }
    private void BeginSort(Vector3 direction)
    {
        pendingDirection=direction;
        sortMaterial.SetInt("_SortMode",0);
        sortMaterial.SetVector("_Direction",direction);
        SortBlit(positions,workA,0);
        current=workA;next=workB;stage=2;step=1;sorting=true;
    }
}
