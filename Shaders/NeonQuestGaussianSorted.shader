// SPDX-License-Identifier: MIT
// Covariance projection follows Aras Pranckevicius' UnityGaussianSplatting
// (2023). Original notice: Dependencies/GaussianSplatting/LICENSE.md.
// Texture-driven DX11/Udon renderer and sort integration: Neon Quest.
Shader "NeonQuest/VRChat/Gaussian Sorted DX11"
{
 Properties
 {
  _Opacity("Opacity",Float)=1 _SplatScale("Scale",Float)=1
  _Positions("Positions / opacity",2D)="black"{}
  _Scales("Scales",2D)="black"{}
  _Rotations("Rotations",2D)="black"{}
  _Colors("Colors",2D)="black"{}
  _Order("Depth order",2D)="black"{}
  [HideInInspector] _Width("Data width",Float)=1024
  [HideInInspector] _Count("Splat count",Float)=0
 }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
  Cull Off ZWrite Off Blend One OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma target 4.0
   #pragma vertex vert
   #pragma geometry geom
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   Texture2D<float4> _Positions,_Scales,_Rotations,_Colors,_Order;
   float _Width,_Count;
   float _Opacity,_SplatScale;
   struct appdata { float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct data { float4 position:SV_POSITION; float3 view:TEXCOORD0; float3 scale:TEXCOORD1; float4 rotation:TEXCOORD2; float4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
   struct output { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };
   int3 Pixel(uint id) { return int3(id%(uint)_Width,id/(uint)_Width,0); }
   data vert(appdata v)
   {
    data o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(data,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    uint slot=(uint)v.vertex.x,id=(uint)_Order.Load(Pixel(slot)).y;
    if(id>=(uint)_Count){o.position=float4(0,0,0,-1);return o;}
    float4 p=_Positions.Load(Pixel(id));
    o.position=UnityObjectToClipPos(float4(p.xyz,1));o.view=UnityObjectToViewPos(p.xyz);
    o.scale=_Scales.Load(Pixel(id)).xyz*_SplatScale;o.rotation=_Rotations.Load(Pixel(id));
    o.color=float4(_Colors.Load(Pixel(id)).rgb,p.w*_Opacity);return o;
   }
   [maxvertexcount(4)]
   void geom(point data input[1],inout TriangleStream<output> stream)
   {
    data d=input[0];UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(d);
    if(d.position.w<=0||d.color.a<1.0/255.0)return;
    float4 q=d.rotation;
    float3x3 r=float3x3(1-2*(q.y*q.y+q.z*q.z),2*(q.x*q.y-q.z*q.w),2*(q.x*q.z+q.y*q.w),
     2*(q.x*q.y+q.z*q.w),1-2*(q.x*q.x+q.z*q.z),2*(q.y*q.z-q.x*q.w),
     2*(q.x*q.z-q.y*q.w),2*(q.y*q.z+q.x*q.w),1-2*(q.x*q.x+q.y*q.y));
    float3 a=mul((float3x3)UNITY_MATRIX_MV,mul(r,float3(d.scale.x,0,0)));
    float3 b=mul((float3x3)UNITY_MATRIX_MV,mul(r,float3(0,d.scale.y,0)));
    float3 c=mul((float3x3)UNITY_MATRIX_MV,mul(r,float3(0,0,d.scale.z)));
    float z=max(.001,-d.view.z);
    float2 viewXY=clamp(d.view.xy/z,-1.3/abs(float2(UNITY_MATRIX_P._m00,UNITY_MATRIX_P._m11)),1.3/abs(float2(UNITY_MATRIX_P._m00,UNITY_MATRIX_P._m11)))*z;
    float fx=UNITY_MATRIX_P._m00*_ScreenParams.x*.5;
    float fy=UNITY_MATRIX_P._m11*_ScreenParams.y*.5;
    float3 px=float3(a.x+viewXY.x*a.z/z,b.x+viewXY.x*b.z/z,c.x+viewXY.x*c.z/z)*(fx/z);
    float3 py=float3(a.y+viewXY.y*a.z/z,b.y+viewXY.y*b.z/z,c.y+viewXY.y*c.z/z)*(fy/z);
    float xx=dot(px,px)+.3, yy=dot(py,py)+.3, xy=dot(px,py);
    float mid=(xx+yy)*.5,radius=length(float2((xx-yy)*.5,xy));
    float2 dir=abs(xy)>.000001?normalize(float2(xy,mid+radius-xx)):float2(xx>=yy?1:0,xx>=yy?0:1);
    float2 axis1=dir*min(4096,3*sqrt(max(.1,mid+radius)));
    float2 axis2=float2(-dir.y,dir.x)*min(4096,3*sqrt(max(.1,mid-radius)));
    float2 corners[4]={float2(-1,-1),float2(1,-1),float2(-1,1),float2(1,1)};
    for(int i=0;i<4;i++)
    {
     output o;UNITY_INITIALIZE_OUTPUT(output,o);UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(d,o);
     o.position=d.position;o.position.xy+=(axis1*corners[i].x+axis2*corners[i].y)*2/_ScreenParams.xy*d.position.w;
     o.uv=corners[i]*3;o.color=d.color;UNITY_TRANSFER_FOG(o,d.position);stream.Append(o);
    }
   }
   float4 frag(output i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float alpha=saturate(exp(-.5*dot(i.uv,i.uv))*i.color.a);clip(alpha-1.0/255.0);
    float4 color=float4(GammaToLinearSpace(i.color.rgb),alpha);UNITY_APPLY_FOG(i.fogCoord,color);
    return float4(color.rgb*alpha,alpha);
   }
   ENDCG
  }
 }
}
