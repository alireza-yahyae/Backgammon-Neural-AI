using System;
using System.Collections.Generic;
using System.Linq;

namespace BackgammonByHoratiu.GameLogic.AI.Neural
{
    internal readonly record struct BgStep(int Source, int Destination, int Die);

    internal sealed class BgTurn
    {
        internal List<BgStep> Steps { get; }
        internal BgPosition After { get; }

        internal BgTurn(List<BgStep> steps, BgPosition after)
        {
            Steps = steps;
            After = after;
        }
    }

    /// <summary>
    /// Standard backgammon rules for the neural AI's search (a faithful port of backgammon.py).
    /// Used only for look-ahead; the real game is still played by Table.
    /// </summary>
    internal static class BgRules
    {
        internal const int FromBar = -1;
        internal const int BearOff = -2;

        internal static List<(int Source, int Destination)> SingleMoves(BgPosition position, int player, int die)
        {
            int[] board = position.Board;
            int sign = player == 0 ? 1 : -1;
            List<(int Source, int Destination)> moves = [];

            if (position.Bar[player] > 0)
            {
                int entry = player == 0 ? 24 - die : die - 1;

                if (board[entry] * sign >= -1)
                {
                    moves.Add((FromBar, entry));
                }

                return moves;
            }

            int first = -1;
            int last = -1;

            for (int i = 0; i < BgPosition.PointCount; i++)
            {
                if (board[i] * sign > 0)
                {
                    if (first < 0)
                    {
                        first = i;
                    }

                    last = i;
                }
            }

            if (first < 0)
            {
                return moves;
            }

            int step = player == 0 ? -die : die;
            bool bearing = player == 0 ? last < 6 : first >= 18;

            for (int i = first; i <= last; i++)
            {
                if (board[i] * sign <= 0)
                {
                    continue;
                }

                int destination = i + step;

                if (destination >= 0 && destination <= 23)
                {
                    if (board[destination] * sign >= -1)
                    {
                        moves.Add((i, destination));
                    }
                }
                else if (bearing)
                {
                    int pointNumber = player == 0 ? i + 1 : 24 - i;

                    if (pointNumber == die)
                    {
                        moves.Add((i, BearOff));
                    }
                    else if (pointNumber < die)
                    {
                        // A larger die may bear off only from the farthest occupied point.
                        int farthest = player == 0 ? last + 1 : 24 - first;

                        if (pointNumber == farthest)
                        {
                            moves.Add((i, BearOff));
                        }
                    }
                }
            }

            return moves;
        }

        internal static void Apply(BgPosition position, int player, int source, int destination)
        {
            int sign = player == 0 ? 1 : -1;

            if (source == FromBar)
            {
                position.Bar[player]--;
            }
            else
            {
                position.Board[source] -= sign;
            }

            if (destination == BearOff)
            {
                position.Off[player]++;
                return;
            }

            if (position.Board[destination] * sign == -1)
            {
                position.Board[destination] = 0;
                position.Bar[1 - player]++;
            }

            position.Board[destination] += sign;
        }

        /// <summary>
        /// All distinct legal full turns for <paramref name="player"/> with the dice still to play.
        /// Enforces: enter from the bar first, use as many dice as possible, and when only one of two
        /// different dice can be played it must be the larger one. Turns that lead to the same position
        /// are merged. If nothing can be played, a single turn with no steps is returned.
        /// </summary>
        internal static List<BgTurn> LegalTurns(BgPosition position, int player, IReadOnlyList<int> dice)
        {
            List<(List<BgStep> Steps, BgPosition State)> leaves = [];

            foreach (int[] order in DistinctOrders(dice))
            {
                HashSet<string> seen = [];
                Explore(position.Copy(), player, order, 0, [], seen, leaves);
            }

            int maxLength = 0;

            foreach (var leaf in leaves)
            {
                maxLength = Math.Max(maxLength, leaf.Steps.Count);
            }

            leaves = [.. leaves.Where(leaf => leaf.Steps.Count == maxLength)];

            if (maxLength == 1 && dice.Distinct().Count() > 1)
            {
                int bigDie = dice.Max();

                if (leaves.Any(leaf => leaf.Steps[0].Die == bigDie))
                {
                    leaves = [.. leaves.Where(leaf => leaf.Steps[0].Die == bigDie)];
                }
            }

            List<BgTurn> turns = [];
            HashSet<string> seenPositions = [];

            foreach (var leaf in leaves)
            {
                if (seenPositions.Add(leaf.State.Key()))
                {
                    turns.Add(new BgTurn(leaf.Steps, leaf.State));
                }
            }

            return turns;
        }

        static void Explore(
            BgPosition state,
            int player,
            int[] order,
            int depth,
            List<BgStep> steps,
            HashSet<string> seen,
            List<(List<BgStep> Steps, BgPosition State)> leaves)
        {
            if (depth < order.Length)
            {
                int die = order[depth];
                List<(int Source, int Destination)> nextMoves = SingleMoves(state, player, die);

                if (nextMoves.Count > 0)
                {
                    foreach ((int source, int destination) in nextMoves)
                    {
                        BgPosition next = state.Copy();
                        Apply(next, player, source, destination);

                        if (!seen.Add(next.Key() + (char)(depth + 1)))
                        {
                            continue;
                        }

                        List<BgStep> extended = [.. steps, new BgStep(source, destination, die)];
                        Explore(next, player, order, depth + 1, extended, seen, leaves);
                    }

                    return;
                }
            }

            leaves.Add((steps, state));
        }

        // Every distinct order in which the dice can be played (doubles: just one).
        static List<int[]> DistinctOrders(IReadOnlyList<int> dice)
        {
            List<int[]> orders = [];

            if (dice.Count == 0 || dice.Distinct().Count() == 1)
            {
                orders.Add([.. dice]);
                return orders;
            }

            HashSet<string> seen = [];
            Permute(dice, new bool[dice.Count], new int[dice.Count], 0, orders, seen);

            return orders;
        }

        static void Permute(
            IReadOnlyList<int> dice,
            bool[] used,
            int[] current,
            int depth,
            List<int[]> orders,
            HashSet<string> seen)
        {
            if (depth == dice.Count)
            {
                if (seen.Add(string.Join(",", current)))
                {
                    orders.Add((int[])current.Clone());
                }

                return;
            }

            for (int i = 0; i < dice.Count; i++)
            {
                if (used[i])
                {
                    continue;
                }

                used[i] = true;
                current[depth] = dice[i];
                Permute(dice, used, current, depth + 1, orders, seen);
                used[i] = false;
            }
        }

        internal static bool HasWon(BgPosition position, int player)
        {
            return position.Off[player] == BgPosition.CheckersPerSide;
        }

        // 1 = single game, 2 = gammon, 3 = backgammon, for the winner of a finished game.
        internal static int OutcomePoints(BgPosition position)
        {
            int winner = HasWon(position, 0) ? 0 : 1;
            int loser = 1 - winner;
            int loserSign = loser == 0 ? 1 : -1;

            if (position.Off[loser] > 0)
            {
                return 1;
            }

            if (position.Bar[loser] > 0)
            {
                return 3;
            }

            int homeFirst = winner == 0 ? 0 : 18;

            for (int i = homeFirst; i < homeFirst + 6; i++)
            {
                if (position.Board[i] * loserSign > 0)
                {
                    return 3;
                }
            }

            return 2;
        }
    }
}
