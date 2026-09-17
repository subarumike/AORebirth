namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Inventory;

    [TestClass]
    public sealed class RequirementExpressionTests
    {
        [TestMethod]
        public void AndLinkOperatorAloneDoesNotBlock()
        {
            var template = new ItemTemplate
            {
                Actions =
                [
                    new ItemAction
                    {
                        ActionType = (int)ActionType.ToUse,
                        Requirements =
                        [
                            new ItemRequirement
                            {
                                StatNumber = 0,
                                Operator = (int)Operator.And,
                                Value = 0
                            }
                        ]
                    }
                ]
            };

            Assert.IsTrue(template.MeetsActionRequirements(_ => 528961, ActionType.ToUse));
        }

        [TestMethod]
        public void FlagsBitAndIsStillEvaluated()
        {
            var reqs = new List<ItemRequirement>
            {
                new()
                {
                    StatNumber = 0,
                    Operator = (int)Operator.BitAnd,
                    Value = 0x20
                }
            };

            Assert.IsTrue(ItemTemplate.MeetsRequirements(reqs, _ => 0x20));
            Assert.IsFalse(ItemTemplate.MeetsRequirements(reqs, _ => 0x01));
        }

        [TestMethod]
        public void ChildOperatorOrAcceptsEitherLeaf()
        {
            var reqs = new List<ItemRequirement>
            {
                new()
                {
                    StatNumber = (int)CharacterStat.Strength,
                    Operator = (int)Operator.GreaterThan,
                    Value = 100,
                    ChildOperator = (int)Operator.And
                },
                new()
                {
                    StatNumber = (int)CharacterStat.Agility,
                    Operator = (int)Operator.GreaterThan,
                    Value = 100,
                    ChildOperator = (int)Operator.Or
                }
            };

            Assert.IsTrue(ItemTemplate.MeetsRequirements(
                reqs,
                stat => stat == CharacterStat.Agility ? 150 : 1));
            Assert.IsFalse(ItemTemplate.MeetsRequirements(
                reqs,
                _ => 1));
        }

        [TestMethod]
        public void IncompletePostfixCannotFallBackToAnAcceptingLeafFold()
        {
            ItemRequirement leaf = new()
            {
                StatNumber = (int)CharacterStat.Strength,
                Operator = (int)Operator.EqualTo,
                Value = 100,
            };
            ItemRequirement orLink = new() { Operator = (int)Operator.Or };
            Assert.IsFalse(ItemTemplate.MeetsRequirements([leaf, orLink], _ => 100));
            Assert.IsFalse(ItemTemplate.MeetsRequirements([orLink, leaf], _ => 100));
            Assert.IsFalse(ItemTemplate.MeetsRequirements([leaf, leaf, orLink, leaf], _ => 100));
        }

        [TestMethod]
        public void NotLinkInvertsAccumulatedResult()
        {
            var reqs = new List<ItemRequirement>
            {
                new()
                {
                    StatNumber = (int)CharacterStat.ComputerLiteracy,
                    Operator = (int)Operator.GreaterThan,
                    Value = 90
                },
                new()
                {
                    StatNumber = 0,
                    Operator = (int)Operator.Not,
                    Value = 0
                }
            };

            Assert.IsFalse(ItemTemplate.MeetsRequirements(reqs, _ => 100));
            Assert.IsTrue(ItemTemplate.MeetsRequirements(reqs, _ => 50));
        }
    }
}
