#version 450

layout(location = 0) in vec3 Position;
layout(location = 1) in uvec4 Color;
layout(location = 2) in uvec2 TexCoord;

layout(location = 0) out vec4 f_Color;
layout(location = 1) out vec2 f_TexCoord;

void main()
{
    f_Color = Color / 255.0;
    f_TexCoord = vec2(0, 0); // TODO: divide by tex size // TexCoord;
    gl_Position = vec4(Position, 1);
}
