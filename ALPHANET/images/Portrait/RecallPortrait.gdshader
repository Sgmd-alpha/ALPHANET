shader_type canvas_item;

// 如果你单独画了一张 250x190 的纯净小图，可以从材质面板拖进这个槽位
// 如果不传，小图状态下会自动把原图上半部分的弹幕抹成纯白
uniform sampler2D small_texture : source_color, filter_linear_mipmap;
uniform bool has_custom_small_tex = false;

// 弹幕区域的纵向分界线（0.62 刚好在两个小人头顶上方）
uniform float barrage_cutoff_y : hint_range(0.0, 1.0) = 0.62;

// 触发大图显示的判断阈值（值越小代表卡牌在屏幕上被放得越大）
uniform float threshold : hint_range(0.0005, 0.01) = 0.0025;

void fragment() {
    // fwidth(UV.x) 表示屏幕上每 1 个物理像素跨越了多少 UV 距离
    // 假设卡牌小图在屏幕上宽约 200 像素，uv_step 约为 1.0 / 200 = 0.005
    // 假设右键放大检视时宽约 600 像素，uv_step 约为 1.0 / 600 = 0.0016
    float uv_step = fwidth(UV.x);
    
    // Smoothstep 实现丝滑过渡：当卡牌放大（uv_step 变小）时，is_large 趋近于 1.0
    float is_large = 1.0 - smoothstep(threshold * 0.8, threshold * 1.2, uv_step);
    
    vec4 large_col = texture(TEXTURE, UV);
    vec4 small_col;
    
    if (has_custom_small_tex) {
        // 方案 A：使用你单独提供的 250x190 小图
        small_col = texture(small_texture, UV);
    } else {
        // 方案 B：无需额外画图，直接将原图上半部分（弹幕区）替换为纯白背景
        if (UV.y < barrage_cutoff_y) {
            small_col = vec4(1.0, 1.0, 1.0, large_col.a);
        } else {
            small_col = large_col;
        }
    }
    
    // 根据当前卡牌在屏幕上的实际渲染大小输出结果
    COLOR = mix(small_col, large_col, is_large);
}