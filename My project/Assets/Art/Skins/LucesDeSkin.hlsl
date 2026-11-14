// F35 (Skins de armas): la luz de las luces puntuales (el fogonazo, la brasa de la bronca, las del mapa) en los shaders
// propios de las skins, que solo tomaban la luz principal. Sirve con Forward+ (el que usa el juego en PC) y con Forward.
// Cada shader agrega:
//   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
//   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#ifndef LUCES_DE_SKIN_INCLUDED
#define LUCES_DE_SKIN_INCLUDED

// La luz difusa de las otras luces en ese punto (color por atenuación por N·L), y en "brillo" su reflejo (Blinn-Phong).
float3 LucesExtra(float3 posWS, float3 N, float3 V, float4 positionCS, float potencia, out float3 brillo)
{
    float3 suma = 0;
    brillo = 0;
#if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP) || defined(_FORWARD_PLUS)
    InputData inputData = (InputData)0;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    inputData.positionWS = posWS;
    uint cantidad = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(cantidad)
        Light l = GetAdditionalLight(lightIndex, posWS);
        float3 llega = l.color * l.distanceAttenuation;
        suma += llega * saturate(dot(N, l.direction));
        brillo += llega * pow(saturate(dot(N, normalize(l.direction + V))), potencia);
    LIGHT_LOOP_END
#endif
    return suma;
}

#endif
