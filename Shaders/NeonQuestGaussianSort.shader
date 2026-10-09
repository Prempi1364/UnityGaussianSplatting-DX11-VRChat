// Own DX11/Udon fragment-sort implementation. No compute or wave intrinsics.
Shader "NeonQuest/VRChat/Gaussian Sort"
{
 Properties { _MainTex("Input",2D)="black"{} _Positions("Positions",2D)="black"{} }
 SubShader
 {
  Cull Off ZWrite Off ZTest Always
  CGINCLUDE
  #include "UnityCG.cginc"
  Texture2D<float4> _MainTex, _Positions;
  int _Width, _Count, _Stage, _Step;
  float4 _Direction;
  uint Index(float4 p) { return (uint)p.y * (uint)_Width + (uint)p.x; }
  int3 Pixel(uint i) { return int3(i % (uint)_Width, i / (uint)_Width, 0); }
  float4 Initialize(v2f_img i):SV_Target
  {
   uint id=Index(i.pos);
   float key=id<(uint)_Count ? -dot(_Positions.Load(Pixel(id)).xyz,_Direction.xyz) : 3.402823e+38;
   return float4(key,(float)id,0,0);
  }
  float4 Compare(v2f_img i):SV_Target
  {
   uint id=Index(i.pos), peer=id^(uint)_Step;
   float2 a=_MainTex.Load(Pixel(id)).xy,b=_MainTex.Load(Pixel(peer)).xy;
   // Stable tie breaking also guarantees a complete permutation for coincident splats.
   bool aLess=a.x<b.x || (a.x==b.x && a.y<b.y);
   bool takeMin=((id&(uint)_Stage)==0)==((id&(uint)_Step)==0);
   return float4(takeMin ? (aLess?a:b) : (aLess?b:a),0,0);
  }
  ENDCG
  Pass
  {
   CGPROGRAM
   #pragma target 4.0
   #pragma vertex vert_img
   #pragma fragment Initialize
   ENDCG
  }
  Pass
  {
   CGPROGRAM
   #pragma target 4.0
   #pragma vertex vert_img
   #pragma fragment Compare
   ENDCG
  }
 }
}
