using _Core._Player;
using Game.Player.Features;
using UnityEngine;

namespace Game.Hurdles.Effects
{
    /// <summary>Terrain patch: switches what she runs on mid-run (e.g. land to water). Needs a RunnerTerrain on the doll.</summary>
    public class TerrainEffect : MonoBehaviour, IRunnerEffect
    {
        [SerializeField] private TerrainType terrain = TerrainType.Water;

        public void Apply(RunnerController runner)
        {
            if (runner.TryGetFeature(out RunnerTerrain runnerTerrain))
                runnerTerrain.Set(terrain);
        }
    }
}
