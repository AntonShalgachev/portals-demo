float4 _ClippingPlane;
bool _ClippingPlaneEnabled = false;

void ClipPlane(half3 worldPos)
{
    // TODO improve
    if (_ClippingPlaneEnabled)
    {
        float distance = dot(worldPos, _ClippingPlane.xyz);
        distance = distance + _ClippingPlane.w;
        
        clip(distance);
    }
}