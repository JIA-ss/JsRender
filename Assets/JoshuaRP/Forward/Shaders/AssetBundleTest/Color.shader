Shader "ShaderAssetBundle/Color"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }



    SubShader
    {
        Pass
        {
            Tags {"LightMode" = "JsRP PrePass"}
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #include "Assets/JoshuaRP/Forward/Shaders/ForwardCommon.hlsl"
            #pragma vertex PrepassVS
            #pragma fragment PrepassFS
            ENDHLSL
        }

        Pass
        {
            Tags {"LightMode" = "JsRP Opaque"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #pragma multi_compile _ RED GREEN BLUE

            #include "UnityCG.cginc"
            #include "Dependency.hlsl"
            ENDHLSL
        }

        Pass
        {
            Tags {"LightMode" = "ShadowCaster"}
            Cull Off
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #include "Assets/JoshuaRP/Forward/Shaders/ForwardCommon.hlsl"

            #pragma vertex ShadowCasterVS
            #pragma fragment ShadowCasterFS

            ENDHLSL
        }


    }
}
