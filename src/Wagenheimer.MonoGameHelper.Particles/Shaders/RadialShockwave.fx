// Native-resolution scene refraction. Wave units: normalized by viewport height.
#if OPENGL
#define SV_POSITION POSITION
#define PS_SHADERMODEL ps_3_0
#else
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif
sampler TextureSampler : register(s0);
float2 Resolution;
float4 Waves[4]; // normalized center XY, radius/height, amplitude/height
float4 Widths; // ring width/height
float4 MainPS(float4 position:SV_POSITION,float4 color:COLOR0,float2 uv:TEXCOORD0):COLOR0
{
    float2 aspect=float2(Resolution.x/Resolution.y,1);
    float2 displacement=0;
    float rim=0;
    for(int i=0;i<4;i++)
    {
        float2 delta=(uv-Waves[i].xy)*aspect;
        float dist=length(delta);
        float d=(dist-Waves[i].z)/max(Widths[i],0.001);
        float band=exp(-d*d*3);
        float2 normal=delta/max(dist,0.0001);
        displacement+=normal*(band*d*Waves[i].w)/aspect;
        rim+=band*Waves[i].w*12;
    }
    float2 edge=.5/Resolution;
    float4 scene=tex2D(TextureSampler,clamp(uv-displacement,edge,1-edge));
    scene.rgb+=rim*float3(.8,.9,1);
    return scene*color;
}
technique Shockwave { pass P0 { PixelShader=compile PS_SHADERMODEL MainPS(); } }
