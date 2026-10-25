Shader "BombMeBaby/Transparent"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,0.25)
        _Glossiness ("Smoothness", Range(0,1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        LOD 200
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0

        fixed4 _Color;
        half _Glossiness;

        struct Input
        {
            float3 worldPos;
        };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            output.Albedo = _Color.rgb;
            output.Metallic = 0;
            output.Smoothness = _Glossiness;
            output.Alpha = _Color.a;
        }
        ENDCG
    }

    Fallback "Transparent/VertexLit"
}
