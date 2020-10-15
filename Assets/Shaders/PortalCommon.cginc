float4 _Plane;
bool _PlaneClip = false;

void ClipPlane(half3 worldPos)
{
    // TODO improve
    if (_PlaneClip)
    {
        float distance = dot(worldPos, _Plane.xyz);
        distance = distance + _Plane.w;
        
        clip(distance);
    }
}