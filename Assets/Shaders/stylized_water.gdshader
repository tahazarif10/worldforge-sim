shader_type spatial;
render_mode cull_disabled, diffuse_lambert, specular_schlick_ggx;

// Lightweight stylized lagoon for OpenGL Compatibility (older GPUs).
// One RGBA8 texture read. Opaque surface, no depth/screen reads, no
// transparency sorting, no reflection pass, no normal maps or mesh waves.
// Shore mask is generated/updated only on terrain edits, never per frame.
uniform sampler2D shore_mask : filter_linear, repeat_disable;
uniform vec2 map_size = vec2(120.0, 88.0);
uniform vec4 deep_ocean : source_color = vec4(0.055, 0.245, 0.345, 1.0);
uniform vec4 blue_lagoon : source_color = vec4(0.115, 0.46, 0.53, 1.0);
uniform vec4 shore_turquoise : source_color = vec4(0.27, 0.69, 0.63, 1.0);
uniform vec4 foam_tint : source_color = vec4(0.84, 0.94, 0.83, 1.0);
uniform float wave_speed = 0.39;
uniform float water_detail = 1.0; // 0 Eco; 1 Balanced; uniform branch

varying vec2 world_cells;

void vertex() {
    // PlaneMesh stays centered in model space: stable cell-scale motion
    // independent of map size, orthographic zoom or camera movement.
    world_cells = VERTEX.xz + map_size * 0.5;
}

void fragment() {
    vec2 uv = clamp(world_cells / max(map_size, vec2(1.0)),
                    vec2(0.0005), vec2(0.9995));
    vec4 shore = texture(shore_mask, uv);
    float t = TIME * wave_speed;
    vec2 p = world_cells;

    // Two broad crossing swell waves: slow, calm motion with no geometry work.
    float swell = sin(p.x * 1.02 + p.y * 0.31 + t)
                * cos(p.y * 0.91 - p.x * 0.16 - t * 0.72);
    float surface = 0.32 + 0.15 * swell + 0.40 * shore.g;
    vec3 base = mix(deep_ocean.rgb, blue_lagoon.rgb,
                    clamp(surface, 0.0, 1.0));
    base = mix(base, shore_turquoise.rgb,
               clamp(shore.r * 0.48 + shore.g * 0.25, 0.0, 0.55));

    // Distance-graded coastline is precomputed on CPU, smoothly bilinear
    // filtered. Animate width lightly to avoid rigid, tiled white bands.
    float tide = 0.87 + 0.13 * sin(t * 1.1 + p.x * 1.6 - p.y * 1.3);
    float foam = smoothstep(0.17, 0.77, shore.r * tide);
    foam *= 0.64;

    if (water_detail > 0.5) {
        // Eco mode skips secondary high-frequency ripples and glint; one
        // distant-water color remains attractive at strategic zoom.
        float ripples = sin(p.x * 2.41 - p.y * 1.39 + t * 1.21);
        float glimmer = pow(max(0.0, ripples * swell), 7.0);
        base += vec3(0.045, 0.075, 0.07) * glimmer;
        foam += shore.r * max(0.0, ripples) * 0.065;
    }
    ALBEDO = mix(base, foam_tint.rgb, clamp(foam, 0.0, 0.65));
    ROUGHNESS = mix(0.49, 0.31, clamp(surface, 0.0, 1.0));
    METALLIC = 0.0;
    SPECULAR = 0.32;
}
