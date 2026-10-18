Shader "Proxima/Animated Plasma URP"
{
 Properties {
  _BaseMap("Plasma LatLong (sRGB)",2D)="white"{}
  _EmissionStrength("Emission",Range(0,5))=1
  _FlowStrength("Surface Flow",Range(0,.2))=.065
  _Speed("Speed (40 seconds at 1)",Range(0,4))=1
  [HideInInspector] _PreviewTime("Preview time (-1 = live)",Float)=-1
 }
 SubShader {
  Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
  Pass {
   Name "Unlit Plasma" Tags {"LightMode"="UniversalForward"}
   Cull Back ZWrite On
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.5
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST;float _EmissionStrength,_FlowStrength,_Speed,_PreviewTime;
   CBUFFER_END
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
   struct V {float4 positionCS:SV_POSITION;float3 positionOS:TEXCOORD0;float3 positionWS:TEXCOORD1;float3 normalWS:TEXCOORD2;};
   V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.positionOS=i.positionOS.xyz;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(i.normalOS);return o;}
   float3 hash33(float3 p){return frac(sin(float3(dot(p,float3(127.1,311.7,74.7)),dot(p,float3(269.5,183.3,246.1)),dot(p,float3(113.5,271.9,124.6))))*43758.5453);}
   float3 noise33(float3 p){float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash33(i),hash33(i+float3(1,0,0)),f.x),lerp(hash33(i+float3(0,1,0)),hash33(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash33(i+float3(0,0,1)),hash33(i+float3(1,0,1)),f.x),lerp(hash33(i+float3(0,1,1)),hash33(i+1),f.x),f.y),f.z);}
   half4 frag(V i):SV_Target {
    float t=(_PreviewTime<0?_Time.y:_PreviewTime)*_Speed;
    float phase=t*6.28318530718/40;
    float3 offset=float3(.22*cos(phase),.22*sin(phase),.15*sin(phase));
    float3 p=normalize(float3(i.positionOS.x,i.positionOS.z,i.positionOS.y));
    float3 q=normalize(p+_FlowStrength*(noise33(p*8+offset)-noise33(p*8+float3(.22,0,0))));
    float2 uv=float2(atan2(q.y,q.x)/6.28318530718+.5,asin(clamp(q.z,-1,1))/3.14159265359+.5);
    float3 col=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb;
    float grain=1+.04*(noise33(p*115+offset*8).x-noise33(p*115+float3(1.76,0,0)).x);
    float facing=abs(dot(normalize(i.normalWS),GetWorldSpaceNormalizeViewDir(i.positionWS)));
    return half4(col*grain*(.48+.52*pow(facing,.28))*_EmissionStrength,1);
   }
   ENDHLSL
  }
 }
}
