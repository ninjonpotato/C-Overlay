
Texture2D screenTexture : register(t0);
SamplerState screenSampler : register(s0);
cbuffer PositionBuffer : register(b0) //a változóknak
{
    float2 offset; //vector2
    float2 padding;
};

float4 VSMain(uint vertexID : SV_VertexID) : SV_POSITION //háromszöget határoz meg, lefedi a képernyőt vele, ez foglalkozik a vektorokkal
{
    float2 positions[3] =
    {
        float2(-1.0, -1.0),
        float2(-1.0,  3.0),
        float2( 3.0, -1.0)
    };

    return float4(positions[vertexID], 0.0, 1.0);
}

float4 PSMain(float4 position : SV_POSITION) : SV_TARGET //adott pixel új színét adja vissza
{ 
    uint width, height;
    screenTexture.GetDimensions(width, height); 

    float2 size = float2(width, height);//a képernyő méretét megkapjuk
    // 0.5 = az eredeti kép feleakkora lesz
    float scale = 0.5;

    // A célpixelhez tartozó forráspixel megkeresése
   float2 sourcePixel =
    (position  - offset) / scale;

    // A kép körüli terület fekete lesz
    if (any(sourcePixel < 0.0) ||
        any(sourcePixel >= size))
    {
        return float4(0.0, 0.0, 0.0, 1.0);
    }

    return screenTexture.Load(
        int3(int2(sourcePixel), 0)
    );
}
