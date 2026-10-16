using System;
using System.Collections.Generic;
using System.IO;

using BackgammonByHoratiu.GameLogic.AI.Search;
using BackgammonByHoratiu.GameLogic.GameManagers;

namespace BackgammonByHoratiu.GameLogic.AI.Neural
{
    /// <summary>
    /// Plays the AI's turn with the self-play trained network.
    /// The game's Player 2 (columns decrease, home board 0-5) is "player 0" of the network's convention,
    /// so the table is read with its sign flipped.
    /// </summary>
    internal sealed class NeuralMovePlanner
    {
        internal const string NetworkPathVariable = "BACKGAMMON_AI_NET";
        internal const string DefaultNetworkFile = "net.bgnn";

        const int AiPlayer = 0;
        const int DefaultDepth = 2;
        const int DefaultTopCandidates = 5;

        readonly NeuralPolicy policy;

        internal NeuralMovePlanner(NeuralPolicy policy)
        {
            this.policy = policy;
        }

        /// <summary>
        /// Loads the network from BACKGAMMON_AI_NET, or from "Ai/net.bgnn" next to the executable.
        /// Returns null (and the original search AI keeps playing) if there is no usable file.
        /// </summary>
        internal static NeuralMovePlanner TryLoadDefault()
        {
            string path = Environment.GetEnvironmentVariable(NetworkPathVariable);

            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Combine(AppContext.BaseDirectory, "Ai", DefaultNetworkFile);
            }

            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                NeuralNetwork network = NeuralNetwork.Load(path);

                return new NeuralMovePlanner(new NeuralPolicy(network, DefaultDepth, DefaultTopCandidates));
            }
            catch (Exception exception) when (
                exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Neural AI disabled: {exception.Message}");

                return null;
            }
        }

        // Must be called on the game thread; the result is a private copy that is safe to search elsewhere.
        internal static BgPosition CreatePosition(IGameManager game)
        {
            BgPosition position = new();

            for (int i = 0; i < BgPosition.PointCount; i++)
            {
                position.Board[i] = -game.TableValues[i];
            }

            position.Bar[0] = game.Player2.OutedPieces;
            position.Bar[1] = game.Player1.OutedPieces;
            position.Off[0] = game.Player2.CompletedPieces;
            position.Off[1] = game.Player1.CompletedPieces;

            return position;
        }

        internal List<MoveAction> Plan(BgPosition position, IReadOnlyList<int> dice)
        {
            List<BgTurn> turns = BgRules.LegalTurns(position, AiPlayer, dice);
            BgTurn chosen = turns[policy.ChooseTurn(position, AiPlayer, turns)];

            return ToActions(chosen);
        }

        internal static List<MoveAction> ToActions(BgTurn turn)
        {
            List<MoveAction> actions = [];

            foreach (BgStep step in turn.Steps)
            {
                if (step.Source == BgRules.FromBar)
                {
                    actions.Add(new MoveAction(MoveActionType.BarEntry, -1, step.Destination, step.Die));
                }
                else if (step.Destination == BgRules.BearOff)
                {
                    actions.Add(new MoveAction(MoveActionType.BearOff, step.Source, -1, step.Die));
                }
                else
                {
                    actions.Add(new MoveAction(MoveActionType.Normal, step.Source, step.Destination, step.Die));
                }
            }

            return actions;
        }
    }
}
