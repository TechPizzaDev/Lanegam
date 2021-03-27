#version 450

layout(set = 1, binding = 0) uniform texture2D Texture0;
layout(set = 1, binding = 1) uniform sampler Sampler0;

layout(location = 0) in vec4 Color;
layout(location = 1) in vec2 TexCoord;

layout(location = 0) out vec4 s_OutputColor;

layout(constant_id = 103) const bool OutputFormatSrgb = true;

vec3 LinearToSrgb(vec3 linear)
{
    // http://chilliant.blogspot.com/2012/08/srgb-approximations-for-hlsl.html
    vec3 S1 = sqrt(linear);
    vec3 S2 = sqrt(S1);
    vec3 S3 = sqrt(S2);
    vec3 sRGB = 0.662002687 * S1 + 0.684122060 * S2 - 0.323583601 * S3 - 0.0225411470 * linear;
    return sRGB;
}

void main()
{
    vec4 color = texture(sampler2D(Texture0, Sampler0), TexCoord);
    color *= Color;

    if (!OutputFormatSrgb)
    {
        color = vec4(LinearToSrgb(color.rgb), 1);
    }

    s_OutputColor = color;
}
