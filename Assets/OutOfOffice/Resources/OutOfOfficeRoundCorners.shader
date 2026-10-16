// Cuts rounded corners into a wardrobe icon as IconStudio copies it into place (OUT-36), so the tiles need no
// stencil mask. The corner radius is a fraction of the icon's side.
Shader "Hidden/OutOfOffice/RoundCorners"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Radius ("Corner radius", Float) = 0.19
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Radius;

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float2 q = abs(i.uv - 0.5) - (0.5 - _Radius);
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - _Radius;  // < 0 inside the rounded square
                float aa = max(fwidth(d), 1e-5);
                c.a *= saturate(0.5 - d / aa);
                return c;
            }
            ENDCG
        }
    }
}
