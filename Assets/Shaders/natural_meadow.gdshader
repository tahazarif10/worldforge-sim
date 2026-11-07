shader_type spatial;
render_mode diffuse_burley, specular_disabled, cull_disabled;

// Continuous world-space meadow shading: no UV tile/checker pattern.
// This material is installed only by the manual V2 landscape adapter.
uniform sampler2D biome_map : filter_linear, repeat_disable;
uniform vec2 map_size = vec2(192.0, 144.0);
uniform float seed_offset = 0.0;
uniform vec4 house_a = vec4(0.0);
uniform vec4 house_b = vec4(0.0);
uniform vec4 house_c = vec4(0.0);
varying vec3 ground_world;

float hash21(vec2 p) {
    vec3 q = fract(vec3(p.xyx) * 0.1031);
    q += dot(q, q.yzx + 33.33);
    return fract((q.x + q.y) * q.z);
}

float noise2(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash21(i), hash21(i + vec2(1.0, 0.0)), u.x),
               mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0)), u.x), u.y);
}

float yard_weight(vec2 p, vec4 house) {
    if (house.z < 0.5) return 0.0;
    vec2 local = p - (house.xy + vec2(1.0));
    vec2 outside = max(abs(local) - vec2(0.78, 0.76), vec2(0.0));
    float irregular = (noise2(p * 2.4 + house.w) - 0.5) * 0.22;
    float apron = 1.0 - smoothstep(0.15, 0.72, length(outside) + irregular);
    float along = p.y - (house.y + 1.83);
    float bend = sin(along * 1.45 + house.w * 0.13) * 0.10;
    float center = house.x + 0.60 + bend;
    float width = abs(p.x - center) + (noise2(p * 3.1) - 0.5) * 0.10;
    float path = (1.0 - smoothstep(0.20, 0.45, width)) *
                 smoothstep(-0.18, 0.20, along) *
                 (1.0 - smoothstep(1.50, 2.65, along));
    return max(apron * 0.88, path);
}

void vertex() {
    ground_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec2 p = ground_world.xz;
    vec4 biomes = texture(biome_map, p / map_size);
    float green = clamp(biomes.r + biomes.g, 0.0, 1.0);
    float woods = biomes.g / max(0.001, green);
    vec2 seed = vec2(seed_offset * 0.031, seed_offset * 0.017);
    vec2 warp = vec2(noise2(p * 0.13 + seed), noise2(p.yx * 0.16 + seed + 19.4));
    vec2 q = p + (warp - 0.5) * 2.5;
    float broad = noise2(q * 0.24 + seed);
    float middle = noise2(q * 0.91 + seed + 7.1);
    float grass_mix = clamp(broad * 0.74 + middle * 0.26, 0.0, 1.0);
    vec3 grass = mix(vec3(0.245, 0.355, 0.150), vec3(0.410, 0.510, 0.255), grass_mix);
    vec3 forest = mix(vec3(0.205, 0.295, 0.170), vec3(0.315, 0.400, 0.220), grass_mix);
    vec3 meadow = mix(grass, forest, woods * 0.85);
    float dry = smoothstep(0.64, 0.84, broad) * 0.20;
    meadow = mix(meadow, vec3(0.48, 0.43, 0.28), dry);

    // Fine, antialiased variation fades at distant zoom, not square speckle.
    float pixel_span = max(length(dFdx(p)), length(dFdy(p)));
    float detail = 1.0 - smoothstep(0.025, 0.15, pixel_span);
    float grain = noise2(p * 6.3 + seed) - 0.5;
    meadow *= 1.0 + grain * 0.045 * detail;

    vec3 view_up = normalize((VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
    float top_face = smoothstep(0.65, 0.93, dot(normalize(NORMAL), view_up));
    float meadow_weight = green * top_face;
    ALBEDO = mix(COLOR.rgb, meadow, meadow_weight);

    // Meadow center-fan normals used to reveal the square simulation cells.
    // Keep height/picking intact and soften ONLY gently sloping green tops.
    NORMAL = normalize(mix(NORMAL, view_up, meadow_weight * 0.94));
    float yard = max(yard_weight(p, house_a), max(yard_weight(p, house_b), yard_weight(p, house_c)));
    float yard_allowed = clamp(biomes.r + biomes.b, 0.0, 1.0) * top_face;
    vec3 earth = mix(vec3(0.32, 0.255, 0.17), vec3(0.49, 0.405, 0.275), middle);
    ALBEDO = mix(ALBEDO, earth, yard * yard_allowed * 0.87);
    ROUGHNESS = 0.96;
    METALLIC = 0.0;
    SPECULAR = 0.0;
}
