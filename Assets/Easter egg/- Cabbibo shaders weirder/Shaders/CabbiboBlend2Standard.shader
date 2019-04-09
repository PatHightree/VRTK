Shader "Custom/CabbiboBlend2Standard"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

		_EffectBlend("Effect blend", Range(0,1)) = 0.5
		_Emission("Emission", Range(0,1)) = 0.5

 		// This is how many steps the trace will take.
 		// Keep in mind that increasing this will increase
 		// Cost
		_NumberSteps( "Number Steps", Int ) = 3

		// Total Depth of the trace. Deeper means more parallax
		// but also less precision per step
		_TotalDepth( "Total Depth", Float ) = 0.16

		_NoiseSize( "Noise Size", Float ) = 10
		_NoiseSpeed( "Noise Speed", Float ) = .3
		_HueSize( "Hue Size", Float ) = .3
		_BaseHue( "Base Hue", Float ) = .3

    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
			float3 worldPos;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

		uniform float _EffectBlend;
		uniform float _Emission;
		uniform int _NumberSteps;
		uniform float _TotalDepth;
		uniform float _NoiseSize;
		uniform float _NoiseSpeed;
		uniform float _HueSize;
		uniform float _BaseHue;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)


		float3 hsv(float h, float s, float v)
		{
			return lerp( float3( 1.0,1,1 ), clamp(( abs( frac(h + float3( 3.0, 2.0, 1.0 ) / 3.0 )
				* 6.0 - 3.0 ) - 1.0 ), 0.0, 1.0 ), s ) * v;
		}


		// Taken from https://www.shadertoy.com/view/4ts3z2
		// By NIMITZ  (twitter: @stormoid)
		// good god that dudes a genius...

		float tri( float x )
		{ 
			return abs( frac(x) - .5 );
		}

		float3 tri3( float3 p )	
		{
			return float3( 
				tri( p.z + tri( p.y * 1. ) ), 
				tri( p.z + tri( p.x * 1. ) ), 
				tri( p.y + tri( p.x * 1. ) ));
		}
			                                 
		float triNoise3D( float3 p, float spd , float time)
		{
			float z  = 1.4;
			float rz =  0.;
			float3  bp =   p;

			for( float i = 0.; i <= 3.; i++ )
			{
				float3 dg = tri3( bp * 2. );
				p += ( dg + time * .1 * spd );

				bp *= 1.8;
				z  *= 1.5;
				p  *= 1.2; 
			      
				float t = tri( p.z + tri( p.x + tri( p.y )));
				rz += t / z;
				bp += 0.14;
			}
			return rz;
		}

		float getFogVal( float3 pos )
		{
			//float s1 = 3 * _NoiseSize 
			float v =  triNoise3D( pos , _NoiseSpeed , _Time.y ) * 2;
			return v;
		}

		float4 Cabbibo(float3 ro, float3 rd)
		{
			// Our color starts off at zero,   
			float3 col = float3( 0.0 , 0.0 , 0.0 );
			float3 p;

			for( int i = 0; i < _NumberSteps; i++ )
			{
				float stepVal = float(i)/_NumberSteps;
				// We get out position by adding the ray direction to the ray origin
				// Keep in mind thtat because the ray direction is normalized, the depth
				// into the step will be defined by our number of steps and total depth
				p = ro + rd * stepVal * _TotalDepth ;

				// We get our value of how much of the volumetric material we have gone through
				// using the position
				float val = getFogVal( p * _NoiseSize );	

				if( val > .55 && val < .65 )
				{ 
					val = 1 - (abs( val - .6 ) * 10);
				}
				else
				{
					val = 0;
				}
				col += 3 * hsv( stepVal * _HueSize + _BaseHue, 1 , 1) * val ;
			}

			col /= _NumberSteps;
			fixed4 color = fixed4( col , 1. );
			return color;
		}

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
			// World space noise
			//float3 rayOrigin = mul(unity_WorldToObject, float4(IN.worldPos, 1));
			//float3 rayDirection = normalize(mul(unity_WorldToObject, float4(IN.worldPos - _WorldSpaceCameraPos), 1));

			// Object space noise
			float3 rayOrigin = mul(unity_WorldToObject, float4(IN.worldPos, 1));
			float4 objectSpaceCamPos = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos,1));
			float4 objectSpaceVertPos = mul(unity_WorldToObject, float4(IN.worldPos,1));
			float3 rayDirection = normalize(objectSpaceVertPos - objectSpaceCamPos);

            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = lerp(c.rgb, Cabbibo(rayOrigin, rayDirection).rgb, _EffectBlend);
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = o.Albedo * _Emission;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
