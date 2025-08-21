uniform float3 iResolution;      // Viewport resolution (pixels)
uniform float  iTime;            // Shader playback time (s)
uniform float4 iMouse;           // Mouse drag pos=.xy Click pos=.zw (pixels)
uniform float3 iImageResolution; // iImage1 resolution (pixels)
uniform shader iImage1;          // An input image.

//uniform float origin;
//uniform vec4 container;
//uniform float cornerRadius;

//const float origin = 0;
//const vec4 container = vec4(0.0, 0.0, 0.0, 0.0);
const float cornerRadius = 0;

const float r = 175.0;
const float scaleFactor = 0.5;

const float  PI = 3.14159265359;
const vec4 TRANSPARENT = vec4(0.0, 0.0, 0.0, 0.0);

mat3 translate(vec2 p) {
return mat3(1.0, 0.0, 0.0, 0.0, 1.0, 0.0, p.x, p.y, 1.0);
}

mat3 scale(vec2 s, vec2 p) {
return translate(p) * mat3(s.x, 0.0, 0.0, 0.0, s.y, 0.0, 0.0, 0.0, 1.0) * translate(-p);
}

vec2 project(vec2 p, mat3 m) {
return (inverse(m) * vec3(p, 1.0)).xy;
}

 


float inRect(vec2 p, vec4 rct) {
    bool inRct = p.x > rct.x && p.x < rct.z && p.y > rct.y && p.y < rct.w;
    if (!inRct) {
        return 0.0;
    }
    // Top left corner
    if (p.x < rct.x + cornerRadius && p.y < rct.y + cornerRadius) {
        return length(p - vec2(rct.x + cornerRadius, rct.y + cornerRadius)) < cornerRadius ? 1.0 : 0.0;
    }
    // Top right corner
    if (p.x > rct.z - cornerRadius && p.y < rct.y + cornerRadius) {
        return length(p - vec2(rct.z - cornerRadius, rct.y + cornerRadius)) < cornerRadius ? 1.0 : 0.0;
    }
    // Bottom left corner
    if (p.x < rct.x + cornerRadius && p.y > rct.w - cornerRadius) {
        return length(p - vec2(rct.x + cornerRadius, rct.w - cornerRadius)) < cornerRadius ? 1.0 : 0.0;
    }
    // Bottom right corner
    if (p.x > rct.z - cornerRadius && p.y > rct.w - cornerRadius) {
        return length(p - vec2(rct.z - cornerRadius, rct.w - cornerRadius)) < cornerRadius ? 1.0 : 0.0;
    }
    return 1.0;
}

half4 main(float2 fragCoord) {

float origin = 500;//iMouse.x;           
float pointer = iMouse.x;  // Animation parameter

float2 renderingScale = iImageResolution.xy / iResolution.xy;
vec4 container = vec4(0.0, 0.0, iResolution.x, iResolution.y); // Creates a rectangle covering the entire viewport
vec2 center = iResolution.xy * 0.5;
vec4 fragColor = TRANSPARENT;
               
vec2 xy = fragCoord.xy;             
float dx = origin - pointer;
float x = container.z - dx;
float d = xy.x - x;

if (d > r) {
    fragColor = TRANSPARENT;
    if (inRect(xy, container) != 0) {
        fragColor.a = mix(0.5, 0.0, (d-r)/r);
    }
}

else
if (d > 0.0) {
    float theta = asin(d / r);
    float d1 = theta * r;
    float d2 = (3.14159265 - theta) * r;

    vec2 s = vec2(1.0 + (1.0 - sin(3.14159265/2.0 + theta)) * 0.1);
    mat3 transform = scale(s, center);
    vec2 uv = project(xy, transform);
    vec2 p1 = vec2(x + d1, uv.y);

    s = vec2(1.1 + sin(3.14159265/2.0 + theta) * 0.1);
    transform = scale(s, center);
    uv = project(xy, transform);
    vec2 p2 = vec2(x + d2, uv.y);

    if (inRect(p2, container)!= 0) {

        fragColor = iImage1.eval(p2*renderingScale);

    } else if (inRect(p1, container)!= 0) {

        fragColor = iImage1.eval(p1*renderingScale);

        fragColor.rgb *= pow(clamp((r - d) / r, 0.0, 1.0), 0.2);

    } else if (inRect(xy, container)!= 0) {
        fragColor = vec4(0.0, 0.0, 0.0, 0.5);
    }
}
else {
    vec2 s = vec2(1.2);
    mat3 transform = scale(s, center);
    vec2 uv = project(xy, transform);

    vec2 p = vec2(x + abs(d) + 3.14159265 * r, uv.y);
    if (inRect(p, container)!= 0) {
        fragColor = iImage1.eval(p*renderingScale);
    } else {
        fragColor = iImage1.eval(xy*renderingScale);
    }
}
           
return fragColor;
}