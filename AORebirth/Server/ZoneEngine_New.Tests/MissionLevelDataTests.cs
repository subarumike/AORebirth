namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.GameData;

[TestClass]
public sealed class MissionLevelDataTests
{
    const string Header = "Level,Q0,Q1,Q2,Q3,Q4,Q5,Q6,Q7,Q8,Q9,Q10,Tokens\n";

    [TestMethod]
    public void EditingDataChangesInterpretationWithoutChangingBinaryOrPriorSnapshot()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, Header + "1,1,1,1,1,1,1,1,1,1,1,1,1\n");
            var first = MissionLevelData.Load(path);
            File.WriteAllText(path, Header + "1,2,2,2,2,2,2,2,2,2,2,2,3\n");
            var second = MissionLevelData.Load(path);
            Assert.AreEqual(1, first.Quality(1, 0));
            Assert.AreEqual(2, second.Quality(1, 0));
            Assert.AreEqual(3, second.Tokens(1));
            Assert.AreSame(first.GetType().Assembly, second.GetType().Assembly);
        }
        finally { File.Delete(path); }
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("1,1,1,1,1,1,1,1,1,1,1,1,1\n1,1,1,1,1,1,1,1,1,1,1,1,1\n")]
    [DataRow("2,1,1,1,1,1,1,1,1,1,1,1,1\n")]
    [DataRow("1,2,1,1,1,1,1,1,1,1,1,1,1\n")]
    public void InvalidDataIsRejectedBeforePublishingSnapshot(string rows)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, Header + rows);
            Assert.ThrowsExactly<InvalidDataException>(() => MissionLevelData.Load(path));
        }
        finally { File.Delete(path); }
    }
}
