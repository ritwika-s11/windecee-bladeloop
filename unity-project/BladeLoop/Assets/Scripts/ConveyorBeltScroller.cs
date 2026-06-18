using UnityEngine;

public class ConveyorBeltScroller : MonoBehaviour
{
    [Tooltip("How fast the texture scrolls. Try 0.5 to start.")]
    public float scrollSpeed = 0.5f;

    [Tooltip("Scroll direction. (1,0) = horizontal, (0,1) = vertical.")]
    public Vector2 scrollDirection = new Vector2(1f, 0f);

    [Tooltip("How many stripes across the belt.")]
    public int stripeCount = 16;

    [Tooltip("Tiling — how many times the texture repeats across the belt surface.")]
    public Vector2 tiling = new Vector2(4f, 1f);

    private Material beltMaterial;
    private Vector2 currentOffset;
    private const string TEX_PROP = "_BaseMap";

    void Start()
    {
        beltMaterial = GetComponent<Renderer>().material;

        // Build a small striped texture procedurally so we have something to scroll
        Texture2D stripeTex = new Texture2D(stripeCount, 4, TextureFormat.RGBA32, false);
        stripeTex.filterMode = FilterMode.Point;
        stripeTex.wrapMode = TextureWrapMode.Repeat;

        Color dark = new Color(0.15f, 0.15f, 0.15f);
        Color light = new Color(0.45f, 0.45f, 0.45f);

        for (int x = 0; x < stripeCount; x++)
        {
            Color c = (x % 2 == 0) ? dark : light;
            for (int y = 0; y < 4; y++)
                stripeTex.SetPixel(x, y, c);
        }
        stripeTex.Apply();

        beltMaterial.SetTexture(TEX_PROP, stripeTex);
        beltMaterial.SetTextureScale(TEX_PROP, tiling);
    }

    void Update()
    {
        currentOffset += scrollDirection * scrollSpeed * Time.deltaTime;
        beltMaterial.SetTextureOffset(TEX_PROP, currentOffset);
    }
}