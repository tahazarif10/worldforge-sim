shader_type spatial;
render_mode cull_disabled, diffuse_lambert, specular_schlick_ggx;

// Light, low-cost color movement. Designed for the OpenGL Compatibility renderer;
// no depth-texture, screen grab, SSR or Forward+-only functions.
uniform vec4 deep_blue : source_color = vec4(0.055, 0.30, 0.43, 1.0);
uniform vec4 turquoise : source_color = vec4(0.20, 0.60, 0.68, 1.0);
uniform float wave_speed = 0.42;
uniform float wave_strength = 0.25;

void fragment() {
    vec2 p = UV * vec2(42.0, 33.0);
    float crest = sin(p.x * 1.18 + TIME * wave_speed)
                * cos(p.y * 0.88 - TIME * wave_speed * 0.72);
    float streak = sin(p.x * 3.8 + p.y * 0.63 + TIME * 0.37);
    float t = clamp(0.42 + crest * wave_strength + max(0.0, streak - 0.84) * 0.18, 0.0, 1.0);
    ALBEDO = mix(deep_blue.rgb, turquoise.rgb, t);
    ROUGHNESS = 0.32;
    METALLIC = 0.06;
    SPECULAR = 0.55;
}
