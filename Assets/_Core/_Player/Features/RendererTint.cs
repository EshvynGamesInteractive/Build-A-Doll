using UnityEngine;

namespace Game.Player.Features
{
    /// <summary>
    /// Tints renderers without touching their material assets (MaterialPropertyBlock), for URP Lit
    /// (_BaseColor) and Built-in (_Color) shaders. Sprites use their own colour.
    /// </summary>
    public static class RendererTint
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static MaterialPropertyBlock _block;

        public static void Set(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;

            if (renderer is SpriteRenderer sprite)
            {
                sprite.color = color;
                return;
            }

            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(_block);
        }

        public static void Clear(Renderer renderer)
        {
            if (renderer == null)
                return;

            if (renderer is SpriteRenderer sprite)
                sprite.color = Color.white;
            else
                renderer.SetPropertyBlock(null);
        }

        public static Color Get(Renderer renderer)
        {
            if (renderer == null)
                return Color.white;

            if (renderer is SpriteRenderer sprite)
                return sprite.color;

            if (renderer.HasPropertyBlock())
            {
                _block ??= new MaterialPropertyBlock();
                renderer.GetPropertyBlock(_block);
                if (_block.HasColor(BaseColorId))
                    return _block.GetColor(BaseColorId);
            }

            var material = renderer.sharedMaterial;
            if (material != null)
            {
                if (material.HasProperty(BaseColorId))
                    return material.GetColor(BaseColorId);
                if (material.HasProperty(ColorId))
                    return material.GetColor(ColorId);
            }

            return Color.white;
        }
    }
}
