using UnityEditor;

// Blender models under Resources/Models and Art/Arena: keep meshes readable so birds can be tinted
// at runtime, skip imported materials (the game assigns the vertex-color knoll shader), import no rig,
// and let Unity smooth the normals so curved shapes show no polygon edges. Folds sharper than the
// smoothing angle (box corners, feather edges) stay crisp.
class ModelImportRules : AssetPostprocessor
{
    const float SmoothingAngle = 60f;

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith("Assets/Resources/Models/") && !assetPath.StartsWith("Assets/Art/Arena/"))
            return;
        var importer = (ModelImporter)assetImporter;
        importer.isReadable = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importNormals = ModelImporterNormals.Calculate;
        importer.normalSmoothingAngle = SmoothingAngle;
        importer.normalCalculationMode = ModelImporterNormalCalculationMode.AreaAndAngleWeighted;

        // Gen robot bots (YoBot / SpiderBot): textured Generic rigs with procedural clips.
        // Stand-in / Blender bird exports stay vertex-color, no animation.
        if (assetPath.StartsWith("Assets/Resources/Models/Bots/"))
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            return;
        }

        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
    }
}
