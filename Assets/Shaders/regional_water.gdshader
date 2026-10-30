shader_type spatial;
render_mode cull_disabled, diffuse_lambert, specular_schlick_ggx;

// Local per-visible-chunk sea shader: NO global 8192x8192 water texture.
// No transparency, screen/depth reads, mesh waves, SSR or extra render pass.
uniform vec4 ocean_color : source_color = vec4(0.055,0.25,0.37,1.0);
uniform vec4 lagoon_color : source_color = vec4(0.16,0.49,0.55,1.0);
varying vec2 absolute_world;

void vertex() {
    absolute_world = VERTEX.xz;
}

void fragment() {
    float wave = sin(absolute_world.x * 1.03 + TIME * 0.41) *
                 cos(absolute_world.y * .91 - TIME * .29);
    float shimmer = clamp(wave * .18 + .52, 0.0, 1.0);
    ALBEDO = mix(ocean_color.rgb, lagoon_color.rgb, shimmer);
    ROUGHNESS = .43;
    METALLIC = 0.0;
    SPECULAR = .28;
}
