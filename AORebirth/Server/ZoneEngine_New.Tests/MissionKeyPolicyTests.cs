namespace ZoneEngine_New.Tests;

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AORebirth.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Quests.Dungeons;

[TestClass]
public sealed class MissionKeyPolicyTests
{
    const int FixtureKey = 101;
    const int FixtureDuplicator = 102;

    [TestMethod]
    public void LoadsConfiguredRolesAndGrantSettings()
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("mission-key-policy-");
        try
        {
            WritePolicy(root.FullName);
            MissionKeyPolicy policy = MissionKeyPolicy.Load(root.FullName, new Catalog());
            Assert.AreEqual(FixtureKey, policy.KeyTemplateId);
            Assert.AreEqual(FixtureDuplicator, policy.DuplicatorTemplateId);
            Assert.AreEqual(4, policy.KeyQuality);
            Assert.AreEqual(9, policy.MaxKeysPerQuest);
            Assert.AreEqual("Fixture key for ", policy.NamePrefix);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow("missing-policy")]
    [DataRow("missing-template")]
    [DataRow("wrong-key-role")]
    [DataRow("wrong-duplicator-role")]
    [DataRow("wrong-stored-quality")]
    [DataRow("nonusable-duplicator")]
    public void InvalidPolicyHasNoFallbackRole(string failure)
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("mission-key-policy-");
        try
        {
            if (failure != "missing-policy")
                WritePolicy(root.FullName);
            Assert.ThrowsExactly<InvalidDataException>(() => MissionKeyPolicy.Load(root.FullName, new Catalog(failure)));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    static void WritePolicy(string root)
    {
        string path = Path.Combine(root, MissionKeyPolicy.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            Version = 1,
            KeyTemplateId = FixtureKey,
            DuplicatorTemplateId = FixtureDuplicator,
            KeyQuality = 4,
            MaxKeysPerQuest = 9,
            NamePrefix = "Fixture key for "
        }));
    }

    sealed class Catalog(string? failure = null) : IItemTemplateCatalog
    {
        public bool TryGet(int aoid, out ItemTemplate template)
        {
            if (aoid != FixtureKey && aoid != FixtureDuplicator || aoid == FixtureDuplicator && failure == "missing-template")
            {
                template = null!;
                return false;
            }

            bool key = aoid == FixtureKey;
            IdentityType role = key
                ? failure == "wrong-key-role" ? IdentityType.MissionKeyDuplicator : IdentityType.MissionKey
                : failure == "wrong-duplicator-role" ? IdentityType.MissionKey : IdentityType.MissionKeyDuplicator;
            template = new ItemTemplate
            {
                Id = aoid,
                Quality = key && failure == "wrong-stored-quality" ? 3 : 4,
                DynelType = (int)role,
                Stats = new Dictionary<CharacterStat, int>
                {
                    [CharacterStat.Can] = (int)(!key && failure == "nonusable-duplicator" ? CanFlags.Carry : CanFlags.Carry | CanFlags.Use)
                }
            };
            return true;
        }

        public ItemTemplate Require(int aoid)
            => TryGet(aoid, out ItemTemplate template) ? template : throw new KeyNotFoundException();
    }
}
