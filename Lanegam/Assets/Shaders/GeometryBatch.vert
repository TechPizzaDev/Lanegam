#version 450

layout(set = 0, binding = 0) uniform ProjectionMatrix
{
    mat4 Projection;
    mat4 View;
    mat4 World;
};

layout(set = 1, binding = 0) uniform texture2D Texture0;
layout(set = 1, binding = 1) uniform sampler Sampler0;

layout(location = 0) in vec3 Position;
layout(location = 1) in uvec4 Color;
layout(location = 2) in uvec2 TexCoord;

layout(location = 0) out vec4 f_Color;
layout(location = 1) out vec2 f_TexCoord;

void main()
{
    mat4 wvp = Projection * World;
    vec4 pos =  Projection * World * vec4(Position, 1);

    vec2 texSize = vec2(textureSize(sampler2D(Texture0, Sampler0), 0));

    f_Color = Color / 255.0;
    f_TexCoord = TexCoord / texSize;
    gl_Position = pos;
}
