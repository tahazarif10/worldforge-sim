shader_type spatial;
render_mode diffuse_burley, specular_disabled, cull_disabled;

// Mip-filtered world-space meadow layers, not simulation-cell colors.
uniform sampler2D biome_map : filter_linear, repeat_disable;
uniform sampler2D surface_heights : filter_linear, repeat_disable;
uniform sampler2D meadow_detail : filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec2 map_size = vec2(192.0, 144.0);
uniform float seed_offset = 0.0;
uniform vec4 meadow_shade : source_color = vec4(0.285, 0.420, 0.195, 1.0);
uniform vec4 meadow_body : source_color = vec4(0.405, 0.550, 0.255, 1.0);
uniform vec4 meadow_sun : source_color = vec4(0.525, 0.630, 0.310, 1.0);
uniform vec4 woodland : source_color = vec4(0.270, 0.385, 0.205, 1.0);
uniform vec4 dry_grass : source_color = vec4(0.560, 0.555, 0.345, 1.0);
uniform vec4 earth_dark : source_color = vec4(0.395, 0.315, 0.215, 1.0);
uniform vec4 earth_light : source_color = vec4(0.575, 0.475, 0.320, 1.0);
uniform vec4 sand_color : source_color = vec4(0.745, 0.675, 0.465, 1.0);
uniform vec4 house_a = vec4(0.0);
uniform vec4 house_b = vec4(0.0);
uniform vec4 house_c = vec4(0.0);
varying vec3 ground_world;
varying float ground_top;

vec2 turn(vec2 p) {
    return vec2(p.x * 0.800 - p.y * 0.600, p.x * 0.600 + p.y * 0.800);
}

float yard_weight(vec2 p, vec4 house) {
    if (house.z < 0.5) { return 0.0; }
    vec2 local = p - house.xy - vec2(1.0);
    vec2 outside = max(abs(local) - vec2(0.79, 0.78), vec2(0.0));
    float edge = (texture(meadow_detail, p * 0.19 + house.w * 0.071).g - 0.5) * 0.24;
    float apron = 1.0 - smoothstep(0.10, 0.78, length(outside) + edge);
    float along = p.y - house.y - 1.82;
    float center = house.x + 0.60 + sin(along * 1.37 + house.w * 0.17) * 0.12;
    float path = (1.0 - smoothstep(0.18, 0.53, abs(p.x - center) + edge * 0.36)) *
        smoothstep(-0.18, 0.24, along) * (1.0 - smoothstep(1.55, 2.88, along));
    return max(apron * 0.90, path);
}

float terrain_height(vec2 p) {
    // Height texels are at terrain corners, not independently shaded cells.
    vec2 uv = (clamp(p, vec2(0.0), map_size) + vec2(0.5)) / (map_size + vec2(1.0));
    return texture(surface_heights, uv).r;
}

void vertex() {
    ground_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    vec3 authored_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
    // Two-sided terrain must not accidentally classify its top as a cliff
    // because the fragment normal was flipped by face orientation.
    ground_top = smoothstep(0.48, 0.82, abs(authored_normal.y));
}

void fragment() {
    vec2 p = ground_world.xz;
    vec2 seed = vec2(seed_offset * 0.00317, seed_offset * 0.00193);
    vec4 broad = texture(meadow_detail, p * 0.024 + seed);
    vec2 warped = p + vec2(broad.g - 0.5, broad.r - 0.5) * 2.4;
    vec4 middle = texture(meadow_detail, turn(warped) * 0.083 + seed.yx + vec2(0.31, 0.73));
    vec4 small = texture(meadow_detail, turn(p) * 0.43 + seed + vec2(0.17, 0.61));
    vec4 blades = texture(meadow_detail, p * 0.61 + seed.yx + vec2(0.53, 0.29));
    vec2 biome_warp = (middle.gb - vec2(0.5)) * 0.18;
    vec3 biomes = texture(biome_map, (p + biome_warp) / map_size).rgb;
    float green = clamp(biomes.r + biomes.g, 0.0, 1.0);
    float woods = biomes.g / max(green, 0.001);
    float ground_weight = clamp(green + biomes.b, 0.0, 1.0) * ground_top;

    float coverage = smoothstep(0.23, 0.77, broad.r * 0.68 + middle.g * 0.32);
    vec3 grass = mix(meadow_shade.rgb, meadow_body.rgb, coverage);
    grass = mix(grass, meadow_sun.rgb, smoothstep(0.54, 0.85, middle.r) * 0.64);
    float dry = smoothstep(0.64, 0.88, broad.g) * smoothstep(0.32, 0.72, middle.b);
    grass = mix(grass, dry_grass.rgb, dry * 0.38);
    grass = mix(grass, woodland.rgb * (0.91 + middle.g * 0.18), woods * 0.56);

    // Curved, antialiased blade strokes supply a low carpet beneath real
    // instanced grass. Mipmaps integrate this detail at distant zoom.
    float span = max(length(dFdx(p)), length(dFdy(p)));
    float near_detail = 1.0 - smoothstep(0.06, 0.32, span);
    float tuft = clamp(small.a * 0.72 + blades.a * 0.48, 0.0, 1.0);
    grass *= 0.96 + (small.b - 0.5) * 0.15 * near_detail;
    grass = mix(grass, meadow_shade.rgb * 0.84, tuft * 0.32 * near_detail);
    grass += meadow_sun.rgb * blades.a * 0.055 * near_detail;

    vec3 sand = sand_color.rgb * (0.92 + middle.b * 0.13 + (small.b - 0.5) * 0.07 * near_detail);
    vec3 ground_color = mix(sand, grass, green / max(green + biomes.b, 0.001));
    float yard = max(yard_weight(p, house_a), max(yard_weight(p, house_b), yard_weight(p, house_c)));
    float yard_allowed = clamp(biomes.r + biomes.b, 0.0, 1.0);
    // Soft hand-worn earth: mixed broad/middle/fine soil tones with no
    // new texture fetches or geometric squares along the cottage path.
    float soil_tone = clamp(middle.b * 0.55 + small.b * 0.28 +
        broad.b * 0.17, 0.0, 1.0);
    vec3 earth = mix(earth_dark.rgb, earth_light.rgb, soil_tone);
    earth *= 0.975 + (small.r - 0.5) * 0.085 * near_detail;
    float soft_yard = smoothstep(0.06, 0.95, yard);
    ground_color = mix(ground_color, earth, soft_yard * yard_allowed * 0.84);
    ALBEDO = mix(COLOR.rgb, ground_color, ground_weight);

    // Continuous lighting follows actual editable corner heights. Disabling
    // synthetic per-cell center relief also removes its repeated self-shadow.
    float d = 0.70;
    float dx = terrain_height(p - vec2(d, 0.0)) - terrain_height(p + vec2(d, 0.0));
    float dz = terrain_height(p - vec2(0.0, d)) - terrain_height(p + vec2(0.0, d));
    vec3 world_normal = normalize(vec3(dx, 2.0 * d, dz));
    vec3 view_normal = normalize((VIEW_MATRIX * vec4(world_normal, 0.0)).xyz);
    NORMAL = normalize(mix(NORMAL, view_normal, ground_weight));
    ROUGHNESS = 0.98;
    METALLIC = 0.0;
    SPECULAR = 0.0;
    AO = 1.0 - yard * yard_allowed * 0.08;
}
