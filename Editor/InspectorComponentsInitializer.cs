using UnityEditor;

namespace InspectorComponents.Editor
{
    // wait for assets postprocessing after domain reload in order to load the uxml and uss assets
    internal sealed class InspectorComponentsInitializer : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (!didDomainReload) return;

            EditorApplication.delayCall -= InspectorComponents.Initialize;
            EditorApplication.delayCall += InspectorComponents.Initialize;
        }
    }
}