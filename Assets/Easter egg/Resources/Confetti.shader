Shader "Unlit/Confetti"
{
    Properties
    {
        _BackColor ("Back Color", Color) = (0,0,0,1)
        [HDR]_GlitterColor("Glitter Color", Color) = (0,0,0)
        _GlitterAmount ("Glitter", Range(0,10)) = 1
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
            
			struct appdata_t {
				float4 vertex : POSITION;
				float4 color : COLOR;
				float3 normal : NORMAL;
			};
            
  			struct v2f {
				float4 vertex : POSITION;
				float4 color : COLOR;
				float bling : TEXCOORD0;
			};          
            
            fixed4 _BackColor;
            fixed4 _GlitterColor;
            float _GlitterAmount;

            v2f vert (appdata_t v)
            {
                v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.color = v.color;
                float3 normal = UnityObjectToWorldNormal(v.normal); 
				float3 viewDir = WorldSpaceViewDir(v.vertex);
                o.bling = pow(max(0.0, dot(normal, viewDir)), _GlitterAmount);
				return o;
            }

            fixed4 frag (v2f i, fixed facing : VFACE) : COLOR
            {
                // VFACE input positive for frontfaces,
                // negative for backfaces. Output one
                // of the two colors depending on that.
                return facing > 0 
                ? i.color * i.bling * _GlitterColor
                : _BackColor;
            }
            ENDCG
        }
    }
}
