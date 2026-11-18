using _Core._Player;

namespace Game.Player.Features
{
    /// <summary>
    /// Finds optional parts of a runner (body, paint, status...) without the RunnerController having
    /// to know about each one. A doll without the part simply ignores that track element.
    /// </summary>
    public static class RunnerFeatureExtensions
    {
        public static bool TryGetFeature<T>(this RunnerController runner, out T feature) where T : class
        {
            var found = runner != null ? runner.GetComponentInChildren<T>(true) : null;

            // Unity's destroyed / missing objects are not C# null, so check them the Unity way.
            feature = found is UnityEngine.Object unityObject && unityObject == null ? null : found;
            return feature != null;
        }
    }
}
