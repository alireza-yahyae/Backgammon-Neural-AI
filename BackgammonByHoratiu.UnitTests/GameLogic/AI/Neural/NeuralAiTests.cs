using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using BackgammonByHoratiu.Entities;
using BackgammonByHoratiu.GameLogic.AI.Neural;
using BackgammonByHoratiu.GameLogic.AI.Search;
using BackgammonByHoratiu.GameLogic.GameManagers;
using Moq;
using NUnit.Framework;

namespace BackgammonByHoratiu.UnitTests.GameLogic.AI.Neural
{
    /// <summary>
    /// Parity tests: Fixtures/neural/golden.json was produced by the Python AI (tools/make_golden.py)
    /// from the same small random network (golden_net.bgnn), so every number here must match it.
    /// </summary>
    [TestFixture]
    public class NeuralAiTests
    {
        sealed class GoldenCase
        {
            public int[] Board { get; set; }
            public int[] Bar { get; set; }
            public int[] Off { get; set; }
            public int Player { get; set; }
            public int[] Dice { get; set; }
            public float[] Features { get; set; }
            public float[] Value { get; set; }
            public string[] TurnKeys { get; set; }
            public string OnePlyKey { get; set; }
            public string TwoPlyKey { get; set; }
        }

        static string FixturePath(string file)
            => Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "neural", file);

        static List<GoldenCase> LoadCases()
        {
            JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };

            return JsonSerializer.Deserialize<List<GoldenCase>>(File.ReadAllText(FixturePath("golden.json")), options);
        }

        static BgPosition ToPosition(GoldenCase golden)
        {
            BgPosition position = new();

            Array.Copy(golden.Board, position.Board, BgPosition.PointCount);
            position.Bar[0] = golden.Bar[0];
            position.Bar[1] = golden.Bar[1];
            position.Off[0] = golden.Off[0];
            position.Off[1] = golden.Off[1];

            return position;
        }

        static List<int> ToDice(GoldenCase golden)
        {
            if (golden.Dice[0] == golden.Dice[1])
            {
                return new List<int> { golden.Dice[0], golden.Dice[0], golden.Dice[0], golden.Dice[0] };
            }

            return new List<int> { golden.Dice[0], golden.Dice[1] };
        }

        [Test]
        public void Features_MatchPython()
        {
            foreach (GoldenCase golden in LoadCases())
            {
                float[] actual = FeatureEncoder.Encode(ToPosition(golden), golden.Player);

                Assert.That(actual, Is.EqualTo(golden.Features).Within(1e-5));
            }
        }

        [Test]
        public void NetworkOutputs_MatchPython()
        {
            NeuralNetwork network = NeuralNetwork.Load(FixturePath("golden_net.bgnn"));

            foreach (GoldenCase golden in LoadCases())
            {
                float[] actual = network.Value(FeatureEncoder.Encode(ToPosition(golden), golden.Player));

                Assert.That(actual, Is.EqualTo(golden.Value).Within(1e-4));
            }
        }

        [Test]
        public void LegalTurns_MatchPython()
        {
            foreach (GoldenCase golden in LoadCases())
            {
                List<string> actual = [.. BgRules
                    .LegalTurns(ToPosition(golden), golden.Player, ToDice(golden))
                    .Select(turn => turn.After.ToText())
                    .OrderBy(key => key, StringComparer.Ordinal)];

                List<string> expected = [.. golden.TurnKeys.OrderBy(key => key, StringComparer.Ordinal)];

                Assert.That(actual, Is.EqualTo(expected));
            }
        }

        [Test]
        public void OnePlyChoice_MatchesPython()
        {
            AssertChoice(depth: 1, golden => golden.OnePlyKey);
        }

        [Test]
        public void TwoPlyChoice_MatchesPython()
        {
            AssertChoice(depth: 2, golden => golden.TwoPlyKey);
        }

        static void AssertChoice(int depth, Func<GoldenCase, string> expectedKey)
        {
            NeuralPolicy policy = new(NeuralNetwork.Load(FixturePath("golden_net.bgnn")), depth, 5);

            foreach (GoldenCase golden in LoadCases())
            {
                BgPosition position = ToPosition(golden);
                List<BgTurn> turns = BgRules.LegalTurns(position, golden.Player, ToDice(golden));

                BgTurn chosen = turns[policy.ChooseTurn(position, golden.Player, turns)];

                Assert.That(chosen.After.ToText(), Is.EqualTo(expectedKey(golden)));
            }
        }

        [Test]
        public void CreatePosition_FlipsTheTableIntoTheNetworksConvention()
        {
            int[] table = new int[24];
            table[0] = 2;
            table[5] = -5;
            table[7] = -3;
            table[11] = 5;
            table[12] = -5;
            table[16] = 3;
            table[18] = 5;
            table[23] = -2;

            Mock<IGameManager> game = new();
            game.Setup(g => g.TableValues).Returns(table);
            game.Setup(g => g.Player1).Returns(new Player { OutedPieces = 1, CompletedPieces = 4 });
            game.Setup(g => g.Player2).Returns(new Player { OutedPieces = 2, CompletedPieces = 3 });

            BgPosition position = NeuralMovePlanner.CreatePosition(game.Object);

            Assert.That(position.Board[5], Is.EqualTo(5));
            Assert.That(position.Board[23], Is.EqualTo(2));
            Assert.That(position.Board[0], Is.EqualTo(-2));
            Assert.That(position.Board[11], Is.EqualTo(-5));
            Assert.That(position.Bar, Is.EqualTo(new[] { 2, 1 }));
            Assert.That(position.Off, Is.EqualTo(new[] { 3, 4 }));
        }

        [Test]
        public void ToActions_MapsBarEntryNormalMoveAndBearOff()
        {
            BgTurn turn = new(
                [
                    new BgStep(BgRules.FromBar, 20, 4),
                    new BgStep(10, 7, 3),
                    new BgStep(2, BgRules.BearOff, 3)
                ],
                new BgPosition());

            List<MoveAction> actions = NeuralMovePlanner.ToActions(turn);

            Assert.That(actions.Select(a => a.Type), Is.EqualTo(new[]
            {
                MoveActionType.BarEntry, MoveActionType.Normal, MoveActionType.BearOff
            }));
            Assert.That(actions[0].DieValue, Is.EqualTo(4));
            Assert.That(actions[1].SourceColumn, Is.EqualTo(10));
            Assert.That(actions[2].SourceColumn, Is.EqualTo(2));
        }

        [Test]
        public void Load_RejectsAFileThatIsNotANetwork()
        {
            string path = Path.GetTempFileName();

            try
            {
                File.WriteAllBytes(path, [1, 2, 3, 4, 5, 6, 7, 8]);

                Assert.Throws<InvalidDataException>(() => NeuralNetwork.Load(path));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
