using UnityEngine;

public class VideoMaterialController : MonoBehaviour
{
    public Material videoMaterial;
    [Range(0, 360)] public float rotation = 0;
    public Vector2 tiling = Vector2.one;
    public Vector2 offset = Vector2.zero;

    void Update()
    {
        if (videoMaterial == null) return;
        videoMaterial.SetFloat("_Rotation", rotation);
        videoMaterial.SetVector("_Tiling", new Vector4(tiling.x, tiling.y, 0, 0));
        videoMaterial.SetVector("_Offset", new Vector4(offset.x, offset.y, 0, 0));
    }
}