Shader "OverlayMap/FogMultiply"
{
    Properties
    {
        _MainTex ("Explored Mask", 2D) = "white" {}
        _Feather ("Feather (texels)", Float) = 2
    }
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Zero SrcColor

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Feather;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 t = _MainTex_TexelSize.xy * max(_Feather, 0.0);
                float a = tex2D(_MainTex, i.uv).a * 0.34;
                a += tex2D(_MainTex, i.uv + float2( t.x, 0)).a * 0.165;
                a += tex2D(_MainTex, i.uv + float2(-t.x, 0)).a * 0.165;
                a += tex2D(_MainTex, i.uv + float2(0,  t.y)).a * 0.165;
                a += tex2D(_MainTex, i.uv + float2(0, -t.y)).a * 0.165;
                return fixed4(1, 1, 1, smoothstep(0.0, 1.0, a));
            }
            ENDCG
        }
    }
}
