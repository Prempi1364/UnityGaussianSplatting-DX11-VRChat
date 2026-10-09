using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Rendering;

// Local GPU sorting is visual state; each visitor needs their own viewing order.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class NeonQuestGaussian : UdonSharpBehaviour
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
    public int passesPerFrame=8;
    public float angleThreshold=2f;
    private RenderTexture current;
    private RenderTexture next;
    private int stage;
    private int step;
    private bool sorting;
    private bool hasOrder;
    private Vector3 orderForward;
    private Vector3 pendingForward;

    private void Start()
    {
        sortMaterial.SetTexture("_Positions",positions);
        sortMaterial.SetInt("_Width",width);sortMaterial.SetInt("_Count",count);
        for(int i=0;i<drawMaterials.Length;i++){drawMaterials[i].SetFloat("_Width",width);drawMaterials[i].SetFloat("_Count",count);}
        workA.Create();workB.Create();published.Create();
    }
    private void Update()
    {
        if(worldRoot==null||!worldRoot.activeInHierarchy||cloud==null||!Utilities.IsValid(Networking.LocalPlayer))return;
        Vector3 forward=Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation*Vector3.forward;
        if(!sorting && (!hasOrder||Vector3.Angle(forward,orderForward)>=angleThreshold))BeginSort(forward);
        if(!sorting)return;
        int budget=Mathf.Clamp(passesPerFrame,1,32);
        for(int i=0;i<budget&&sorting;i++)
        {
            sortMaterial.SetInt("_Stage",stage);sortMaterial.SetInt("_Step",step);
            VRCGraphics.Blit(current,next,sortMaterial,1);
            RenderTexture old=current;current=next;next=old;
            step>>=1;
            if(step==0){stage<<=1;step=stage>>1;}
            if(stage>capacity)
            {
                // Publish only a completed global order. Intermediate bitonic runs
                // are intentionally never sampled by the visible renderer.
                VRCGraphics.Blit(current,published);
                for(int m=0;m<drawMaterials.Length;m++)drawMaterials[m].SetTexture("_Order",published);
                orderForward=pendingForward;hasOrder=true;sorting=false;
            }
        }
    }
    private void BeginSort(Vector3 forward)
    {
        pendingForward=forward;
        // Dot products use the transpose of the object-to-world matrix; this
        // also handles nonuniform and negative cloud scaling correctly.
        Matrix4x4 matrix=cloud.localToWorldMatrix;
        Vector3 direction=new Vector3(Vector3.Dot(matrix.GetColumn(0),forward),Vector3.Dot(matrix.GetColumn(1),forward),Vector3.Dot(matrix.GetColumn(2),forward));
        sortMaterial.SetVector("_Direction",direction);
        VRCGraphics.Blit(positions,workA,sortMaterial,0);
        current=workA;next=workB;stage=2;step=1;sorting=true;
    }
}
