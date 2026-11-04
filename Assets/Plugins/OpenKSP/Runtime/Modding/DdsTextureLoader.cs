using OpenKSP.Core.Modding;
using UnityEngine;

// =============================================================================
// DdsTextureLoader — Unity bridge over the Core Dds/Mbm decoders
// (step-03 Task 8). Picks the decoder by magic: "DDS " → Dds.Decode,
// KSP mbm magic (0x50534B03 LE) → Mbm.Decode; both hand back row-major RGBA,
// which LoadRawTextureData takes verbatim (the decoders emit the same
// memory order the byte format stores; Unity's raw loader consumes the same
// order, so no flip is applied — pinned at the first-pixel level by T21).
// One-time load allocations only.
// =============================================================================

public static class DdsTextureLoader
{
    /// <summary>Decode a .dds or .mbm blob into an RGBA32 texture.</summary>
    public static Texture2D LoadTexture(byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length < 16)
            throw new System.FormatException("DdsTextureLoader: image data too short for any known magic");

        int width, height;
        byte[] rgba;
        if (imageBytes[0] == (byte)'D' && imageBytes[1] == (byte)'D' &&
            imageBytes[2] == (byte)'S' && imageBytes[3] == (byte)' ')
        {
            DdsImage img = Dds.Decode(imageBytes);
            width = img.Width;
            height = img.Height;
            rgba = img.Rgba;
        }
        else
        {
            MbmImage img = Mbm.Decode(imageBytes);
            width = img.Width;
            height = img.Height;
            rgba = img.Rgba;
        }

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        tex.LoadRawTextureData(rgba);
        tex.Apply(false, false);
        tex.filterMode = FilterMode.Point;
        return tex;
    }
}
