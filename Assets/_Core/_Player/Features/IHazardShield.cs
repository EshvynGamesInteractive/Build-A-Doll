namespace Game.Player.Features
{
    /// <summary>Something on the doll that stops harmful track elements (shield status, invulnerable ride).</summary>
    public interface IHazardShield
    {
        /// <summary>True when the hit is blocked. May use the shield up.</summary>
        bool TryBlock();
    }
}
