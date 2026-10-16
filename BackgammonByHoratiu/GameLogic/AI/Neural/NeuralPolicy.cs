using System;
using System.Collections.Generic;
using System.Linq;

namespace BackgammonByHoratiu.GameLogic.AI.Neural
{
    /// <summary>
    /// Chooses a full turn with the value network (a port of selfplay.choose_turn and search.two_ply_policy).
    /// depth 1: pick the turn whose resulting position the network likes best.
    /// depth 2: take the best few depth-1 candidates; for each, look at all 21 opponent rolls, let the
    /// opponent answer with the reply that is worst for us, and average over the rolls.
    /// </summary>
    internal sealed class NeuralPolicy
    {
        static readonly (int First, int Second)[] Rolls = BuildRolls();

        readonly NeuralNetwork network;
        readonly int depth;
        readonly int topCandidates;

        internal NeuralPolicy(NeuralNetwork network, int depth = 2, int topCandidates = 5)
        {
            this.network = network;
            this.depth = depth;
            this.topCandidates = Math.Max(1, topCandidates);
        }

        static (int First, int Second)[] BuildRolls()
        {
            List<(int First, int Second)> rolls = [];

            for (int a = 1; a <= 6; a++)
            {
                for (int b = a; b <= 6; b++)
                {
                    rolls.Add((a, b));
                }
            }

            return [.. rolls];
        }

        /// <summary>Index into <paramref name="turns"/> of the turn to play for <paramref name="player"/>.</summary>
        internal int ChooseTurn(BgPosition position, int player, List<BgTurn> turns)
        {
            if (turns.Count == 1)
            {
                return 0;
            }

            int winning = WinningTurn(player, turns);

            if (winning >= 0)
            {
                return winning;
            }

            double[] scores = LiveScores(player, turns);

            if (depth < 2)
            {
                return IndexOfBest(scores);
            }

            int[] candidates = [.. Enumerable.Range(0, turns.Count)
                .OrderByDescending(i => scores[i])
                .Take(topCandidates)];

            int bestIndex = candidates[0];
            double bestValue = double.NegativeInfinity;

            foreach (int index in candidates)
            {
                double value = ExpectedPointsAfter(turns[index].After, player);

                if (value > bestValue)
                {
                    bestIndex = index;
                    bestValue = value;
                }
            }

            return bestIndex;
        }

        // The turn that wins the game right now with the most points, or -1.
        static int WinningTurn(int player, List<BgTurn> turns)
        {
            int bestIndex = -1;
            int bestPoints = 0;

            for (int i = 0; i < turns.Count; i++)
            {
                if (!BgRules.HasWon(turns[i].After, player))
                {
                    continue;
                }

                int points = BgRules.OutcomePoints(turns[i].After);

                if (points >= bestPoints)
                {
                    bestIndex = i;
                    bestPoints = points;
                }
            }

            return bestIndex;
        }

        // Expected points for the mover after each turn (none of the turns ends the game).
        double[] LiveScores(int player, List<BgTurn> turns)
        {
            int opponent = 1 - player;
            double[] scores = new double[turns.Count];

            for (int i = 0; i < turns.Count; i++)
            {
                float[] outputs = network.Value(FeatureEncoder.Encode(turns[i].After, opponent));
                scores[i] = -NeuralNetwork.Equity(outputs);
            }

            return scores;
        }

        static int IndexOfBest(double[] scores)
        {
            int best = 0;

            for (int i = 1; i < scores.Length; i++)
            {
                if (scores[i] > scores[best])
                {
                    best = i;
                }
            }

            return best;
        }

        // Expected points for `player` (cubeless), averaged over the opponent's 21 rolls, for the position
        // after the player's turn. The opponent picks the reply that is worst for the player.
        double ExpectedPointsAfter(BgPosition after, int player)
        {
            int opponent = 1 - player;
            double total = 0;

            foreach ((int first, int second) in Rolls)
            {
                List<int> dice = first == second
                    ? new List<int> { first, first, first, first }
                    : new List<int> { first, second };
                double worst = double.PositiveInfinity;

                foreach (BgTurn reply in BgRules.LegalTurns(after, opponent, dice))
                {
                    double value;

                    if (BgRules.HasWon(reply.After, opponent))
                    {
                        value = -BgRules.OutcomePoints(reply.After);
                    }
                    else
                    {
                        float[] outputs = network.Value(FeatureEncoder.Encode(reply.After, player));
                        value = NeuralNetwork.Equity(outputs);
                    }

                    worst = Math.Min(worst, value);
                }

                total += (first == second ? 1 : 2) * worst;
            }

            return total / 36.0;
        }
    }
}
