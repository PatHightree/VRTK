Shader "Unlit/Confetti"
{
    Properties
    {
        _BackColor ("Back Color", Color) = (0,0,0,1)
        [HDR]_GlitterColor("Glitter Color", Color) = (0,0,0)
        _GlitterProbability ("Glitter probability", Range(10,1)) = 1
        _GlitterIntensity ("Glitter intensity", Range(0,1)) = 1
    }
    SubShader
    {
        Pass
        {
            Cull Off // turn off backface culling

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            
			struct appdata {
				float4 vertex : POSITION;
				float4 color : COLOR;
				float3 normal : NORMAL;
			};
            
  			struct v2f {
				float4 vertex : POSITION;
				float4 color : COLOR;
				float glitter : TEXCOORD0;
			};          
            
            fixed4 _BackColor;
            fixed4 _GlitterColor;
            float _GlitterProbability;
            float _GlitterIntensity;

            v2f vert (appdata v)
            {
                v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.color = v.color;
				// Calculate glitter
                float3 normal = normalize(UnityObjectToWorldNormal(v.normal));
                float3 cameraDir = normalize(mul((float3x3)unity_CameraToWorld, float3(0,0,1)));
                o.glitter = clamp(pow(max(0.0, dot(normal, -cameraDir)), pow(_GlitterProbability, 3)), 0, _GlitterIntensity);

				return o;
            }

            fixed4 frag (v2f i, fixed facing : VFACE) : COLOR
            {
                // VFACE input positive for frontfaces,
                // negative for backfaces. Output one
                // of the two colors depending on that.
                return facing > 0 
                ? i.color + i.glitter * _GlitterColor
                : _BackColor;
            }
            ENDCG
        }
    }
}
