Shader "Hidden/MyUnityMCP/CaptureEvidence"
{
	Properties
	{
		_McpObjectIdColor ("Object ID Color", Color) = (0, 0, 0, 1)
		_McpNear ("Near", Float) = 0.1
		_McpFar ("Far", Float) = 1000.0
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque"
			"Queue" = "Geometry"
		}

		Pass
		{
			Name "ObjectId"
			ZWrite On
			ZTest LEqual
			Cull Off
			Blend Off

			HLSLPROGRAM
			#pragma target 3.0
			#pragma vertex VertObjectId
			#pragma fragment FragObjectId

			#include "UnityCG.cginc"

			float4 _McpObjectIdColor;

			struct S_AppData
			{
				float4 positionOS : POSITION;
			};

			struct S_Varyings
			{
				float4 positionCS : SV_POSITION;
			};

			S_Varyings VertObjectId(S_AppData input)
			{
				S_Varyings output;
				output.positionCS = UnityObjectToClipPos(input.positionOS);
				return output;
			}

			float4 FragObjectId(S_Varyings input) : SV_Target
			{
				return _McpObjectIdColor;
			}
			ENDHLSL
		}

		Pass
		{
			Name "LinearDepth"
			ZWrite On
			ZTest LEqual
			Cull Off
			Blend Off

			HLSLPROGRAM
			#pragma target 3.0
			#pragma vertex VertLinearDepth
			#pragma fragment FragLinearDepth

			#include "UnityCG.cginc"

			float _McpNear;
			float _McpFar;

			struct S_AppData
			{
				float4 positionOS : POSITION;
			};

			struct S_Varyings
			{
				float4 positionCS : SV_POSITION;
				float eyeDepth : TEXCOORD0;
			};

			S_Varyings VertLinearDepth(S_AppData input)
			{
				S_Varyings output;
				output.positionCS = UnityObjectToClipPos(input.positionOS);
				output.eyeDepth = -UnityObjectToViewPos(input.positionOS).z;
				return output;
			}

			float4 FragLinearDepth(S_Varyings input) : SV_Target
			{
				float depthRange = max(_McpFar - _McpNear, 0.00001);
				float linearDepth = saturate(
					(input.eyeDepth - _McpNear) / depthRange);
				return float4(
					linearDepth,
					linearDepth,
					linearDepth,
					1.0);
			}
			ENDHLSL
		}
	}

	Fallback Off
}
