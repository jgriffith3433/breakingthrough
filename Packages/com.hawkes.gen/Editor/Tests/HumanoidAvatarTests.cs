// EditMode check for a character3d export/fbx: Unity must import it as a valid Humanoid avatar
// with no missing required bones. Gen's unity.test places the file at Assets/HawkesGenCheck/character.fbx
// and runs category HawkesGen.Humanoid (docs/3d-characters.md).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HawkesGen.Tests
{
    public class HumanoidAvatarTests
    {
        const string Path = "Assets/HawkesGenCheck/character.fbx";

        static readonly string[] RequiredBones =
        {
            "Hips", "Spine", "Chest", "Head",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
            "RightUpperLeg", "RightLowerLeg", "RightFoot",
            "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightUpperArm", "RightLowerArm", "RightHand",
        };

        [Test, Category("HawkesGen.Humanoid")]
        public void ExportedFbx_IsValidHumanoid()
        {
            if (!File.Exists(Path))
                Assert.Ignore("No Assets/HawkesGenCheck/character.fbx (unity.test places it when checking a character3d export).");

            var importer = AssetImporter.GetAtPath(Path) as ModelImporter;
            Assert.IsNotNull(importer, "FBX has no ModelImporter at " + Path);

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();

            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(Path);
            Assert.IsNotNull(avatar, "No Avatar on the FBX after Humanoid import");
            Assert.IsTrue(avatar.isValid, "avatar.isValid is false");
            Assert.IsTrue(avatar.isHuman, "avatar.isHuman is false");

            var present = new HashSet<string>(avatar.humanDescription.human.Select(h => h.humanName));
            var missing = RequiredBones.Where(b => !present.Contains(b)).ToList();
            Assert.IsEmpty(missing, "Missing required Humanoid bones: " + string.Join(", ", missing));
        }
    }
}
