// <copyright file="CapturedAreteExactInteractionTests.cs" company="AORebirth">
// Copyright (c) AORebirth. All rights reserved.
// </copyright>

namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Arete;
    using ZoneEngine.Core.Arete.Dialogue;

    [TestClass]
    public class CapturedAreteExactInteractionTests
    {
        [TestMethod]
        public void ExactReplyCatalogPreservesFiniteCapturedSequencesAndFailsClosed()
        {
            Assert.AreEqual(27, CapturedAreteExactInteractionCatalog.GetObservationCount("Mario Carles"));
            Assert.AreEqual(1, CapturedAreteExactInteractionCatalog.GetObservationCount("Robotic Guard Dog"));
            Assert.AreEqual(3, CapturedAreteExactInteractionCatalog.GetObservationCount("Shady Guy"));

            string reply;
            Assert.IsTrue(CapturedAreteExactInteractionCatalog.TryGetReply("Mario Carles", 0, out reply));
            Assert.AreEqual("Out of my way.", reply);
            Assert.IsTrue(CapturedAreteExactInteractionCatalog.TryGetReply("Mario Carles", 1, out reply));
            Assert.AreEqual("Move.", reply);
            Assert.IsTrue(CapturedAreteExactInteractionCatalog.TryGetReply("Mario Carles", 26, out reply));
            Assert.AreEqual("Move.", reply);
            Assert.IsFalse(CapturedAreteExactInteractionCatalog.TryGetReply("Mario Carles", 27, out reply));

            Assert.IsTrue(CapturedAreteExactInteractionCatalog.TryGetReply("Robotic Guard Dog", 0, out reply));
            Assert.AreEqual("Woof woof woof!!!!", reply);
            Assert.IsFalse(CapturedAreteExactInteractionCatalog.TryGetReply("Robotic Guard Dog", 1, out reply));

            Assert.IsTrue(CapturedAreteExactInteractionCatalog.TryGetReply("Shady Guy", 2, out reply));
            Assert.AreEqual("Useless..", reply);
            Assert.IsFalse(CapturedAreteExactInteractionCatalog.TryGetReply("Shady Guy", 3, out reply));
            Assert.IsFalse(CapturedAreteExactInteractionCatalog.TryGetReply("Unknown", 0, out reply));
            Assert.IsFalse(CapturedAreteExactInteractionCatalog.TryGetReply("Mario Carles", -1, out reply));
        }

        [TestMethod]
        public void CapturedJunePackLoadsExactOptionsWithoutInventingPromptOrAnswerSemantics()
        {
            string file = Path.Combine(
                FindRepositoryRoot(),
                "AORebirth",
                "Server",
                "ZoneEngine",
                "Content",
                "Arete",
                "flint-novak",
                "dialogue",
                "captured-june-interactions.dialogue.json");

            AreteContentLoadResult<DialogueContentPack> result =
                new DialogueContentPackLoader().LoadFile(file);

            Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.Validation.Errors));
            DialogueContentPack pack = result.Packs.Single();
            Assert.AreEqual(3, pack.Npcs.Count);

            AssertRoot(
                pack,
                "SimpleChar:782DE582",
                "barry_root",
                "Desmond Calitri sent me here for a Bronto Burger.",
                "What do you have to sell?",
                "Goodbye");
            AssertRoot(
                pack,
                "SimpleChar:782DE699",
                "boris_root",
                "Who are you?",
                "I haven't seen any violence...",
                "Goodbye");
            AssertRoot(
                pack,
                "SimpleChar:782DE57C",
                "desmond_root",
                "Do you have any work around here?",
                "I have some questions...",
                "Goodbye");

            DialogueNpcEntry barry = pack.Npcs.Single(npc => npc.NpcIdentity == "SimpleChar:782DE582");
            Assert.IsTrue(
                barry.Nodes.Any(
                    node => node.Options.Any(option => option.Text == "What is in these Bronto burgers?")));
            DialogueNpcEntry boris = pack.Npcs.Single(npc => npc.NpcIdentity == "SimpleChar:782DE699");
            Assert.IsTrue(
                boris.Nodes.Any(
                    node => node.Options.Any(option => option.Text == "Tell me more about the different Suppression Gas values.")));
            DialogueNpcEntry desmond = pack.Npcs.Single(npc => npc.NpcIdentity == "SimpleChar:782DE57C");
            Assert.IsTrue(
                desmond.Nodes.Any(
                    node => node.Options.Any(option => option.Text == "I have your Bronto Burger.")));
            Assert.IsTrue(
                desmond.Nodes.Any(
                    node => node.Options.Any(option => option.Text == "I took care of those protesters.")));

            Assert.IsTrue(pack.Npcs.SelectMany(npc => npc.Nodes).All(node => node.PromptText == string.Empty));
            Assert.IsTrue(
                pack.Npcs.SelectMany(npc => npc.Nodes)
                    .SelectMany(node => node.Options)
                    .All(option => option.Actions.Count == 1
                                   && option.Actions[0].Type == "EndDialogue"
                                   && option.NextNodeId == "close"));
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "AI_START_HERE.md")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Repository root not found.");
        }

        private static void AssertRoot(
            DialogueContentPack pack,
            string npcIdentity,
            string rootNodeId,
            params string[] expectedOptions)
        {
            DialogueNpcEntry npc = pack.Npcs.Single(entry => entry.NpcIdentity == npcIdentity);
            Assert.AreEqual(rootNodeId, npc.RootNodeId);
            DialogueNode root = npc.Nodes.Single(node => node.Id == rootNodeId);
            CollectionAssert.AreEqual(
                expectedOptions,
                root.Options.OrderBy(option => option.Index).Select(option => option.Text).ToArray());
            Assert.AreEqual("not-captured", root.PromptTextConfidence);
        }
    }
}
