shader_type spatial;
render_mode cull_disabled, diffuse_lambert, specular_schlick_ggx;

// Godot 4.7 Compatibility-friendly stylized lagoon. Two low-cost wave layers,
// one coastline mask sample. No screen-space reflection, depth texture,
// volumetric fog or Forward+-exclusive features.
uniform sampler2D shore_mask : filter_nearest, repeat_disable;
uniform vec2 map_size = vec2(120.0, 88.0);
uniform vec4 deep_blue : source_color = vec4(0.055, 0.28, 0.41, 1.0);
uniform vec4 shallow_teal : source_color = vec4(0.20, 0.60, 0.63, 1.0);
uniform vec4 foam_color : source_color = vec4(0.81, 0.92, 0.80, 1.0);
uniform float wave_speed = 0.48;
uniform float wave_amount = 0.23;

varying vec2 cell_position;

void vertex() {
    // PlaneMesh is centered on worldWidth/height. Convert local position to
    // absolute logical tile coordinates; no wave-scale drift across map size.
    cell_position = VERTEX.xz + map_size * 0.5;
}

void fragment() {
    vec2 tile_uv = clamp(cell_position / max(map_size, vec2(1.0)),
                         vec2(0.001), vec2(0.999));
    vec4 coast = texture(shore_mask, tile_uv);
    vec2 p = cell_position;
    float wave_a = sin(p.x * 1.54 + p.y * 0.43 + TIME * wave_speed) *
                   cos(p.y * 1.08 - TIME * wave_speed * 0.71);
    float wave_b = sin(p.x * 2.83 - p.y * 1.17 - TIME * wave_speed * 0.62);
    float water_tone = clamp(0.30 + 0.28 * coast.g +
                            wave_a * wave_amount + wave_b * 0.07, 0.0, 1.0);

    vec3 lagoon = mix(deep_blue.rgb, shallow_teal.rgb, water_tone);
    float moving_crest = 0.38 + 0.62 * smoothstep(0.15, 0.85,
                                      wave_a * 0.5 + wave_b * 0.25 + 0.50);
    float foam = coast.r * moving_crest * 0.76;
    ALBEDO = mix(lagoon, foam_color.rgb, foam);
    ROUGHNESS = mix(0.38, 0.23, water_tone);
    METALLIC = 0.02;
    SPECULAR = 0.48;
}
