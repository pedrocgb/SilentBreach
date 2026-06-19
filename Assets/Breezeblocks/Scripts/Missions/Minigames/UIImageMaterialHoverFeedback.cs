using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.Missions
{

/// <summary>
/// Swaps a UI Image material while preserving the original material for hover feedback reuse.
/// </summary>
public sealed class UIImageMaterialHoverFeedback
{
    private Image targetImage;
    private Material originalMaterial;
    private Material highlightMaterial;
    private bool isBound;

    /// <summary>
    /// Caches the target image, original material, and highlight material used by hover feedback.
    /// </summary>
    public void Bind(Image image, Material hoverMaterial)
    {
        targetImage = image;
        highlightMaterial = hoverMaterial;
        originalMaterial = targetImage != null ? targetImage.material : null;
        isBound = targetImage != null;
    }

    /// <summary>
    /// Updates the highlight material while keeping the cached original material intact.
    /// </summary>
    public void SetHighlightMaterial(Material hoverMaterial)
    {
        highlightMaterial = hoverMaterial;
    }

    /// <summary>
    /// Applies or removes the highlight material on the target image.
    /// </summary>
    public void SetHighlighted(bool highlighted)
    {
        if (!isBound || targetImage == null)
            return;

        targetImage.material = highlighted && highlightMaterial != null ? highlightMaterial : originalMaterial;
    }

    /// <summary>
    /// Restores the original material and clears the cached target reference.
    /// </summary>
    public void Clear()
    {
        SetHighlighted(false);
        targetImage = null;
        originalMaterial = null;
        highlightMaterial = null;
        isBound = false;
    }
}

}
