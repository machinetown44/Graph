Shader "Custom/Graph"
{
    Properties
    {
        _MainTex ("Lookup Texture", 2D) = "white" {}
        _SegmentData("Segment data", 2D) = "black" {}
        _Thicc("Thickness", Range(0,0.02)) = 0.002
        _BackColor("Background color", Color) = (1, 0, 0, 1)
        _BackFade("Background fade", Range(0, 1)) = 0
        _BackOffset("Background offset", Range(0, 2)) = 0
        _FrontColor("Foreground color", Color) = (0, 1, 0, 1)
        _FrontFade("Background fade", Range(0, 1)) = 0
        _FrontOffset("Background offset", Range(0, 2)) = 0
        _LineColor("Line color", Color) = (0, 0, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _BackColor, _FrontColor, _LineColor;
            float _BackFade, _BackOffset, _FrontFade, _FrontOffset;
            float _Thicc;
            float offset;
            float segmentCount;
            

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex, _SegmentData;
            float4 _MainTex_ST;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed2 DrawLine(float4 seg, float2 coord)
            {
                coord.x -= offset;
                float2 offset = float2(coord.x - seg.x, coord.y - seg.y);
                float2 dir = float2(cos(seg.z), sin(seg.z));
                float dist = dot(offset, dir * seg.w) / (seg.w);
                dist = clamp(dist, 0, seg.w);
                float2 projPoint = float2(seg.x, seg.y) + dist * dir;

                float2 poffset = projPoint - coord;
                float pointDist = dot(poffset, poffset);
                
                float output = step(pointDist, _Thicc * _Thicc);
                
                return float2(output, (1-output)*sign(poffset.y));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half index = tex2D(_MainTex, i.uv);
                half4 segA = tex2D(_SegmentData, float2(index*segmentCount, 0));
                half4 segB = tex2D(_SegmentData, float2((index + 1)*segmentCount, 0));
                half4 segC = tex2D(_SegmentData, float2((index - 1)*segmentCount, 0));
                half2 coord = i.uv;

                float2 mainSeg = DrawLine(segA, coord);
                float lineMult = max(mainSeg.x, DrawLine(segB, coord).x);
                lineMult = max(lineMult, DrawLine(segC, coord).x);

                fixed4 backColor = _BackColor * step(mainSeg.y, 0) * (1-i.uv.y + _BackOffset) * _BackFade;
                fixed4 frontColor = _FrontColor * step(0, mainSeg.y) * (i.uv.y + _FrontOffset) * _FrontFade;

                fixed4 col = lerp(max(backColor, frontColor), _LineColor, lineMult);
                
                return col;
            }


            ENDCG
        }
    }
}
