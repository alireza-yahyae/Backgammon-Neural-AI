using System.Collections.Generic;
using System.Threading.Tasks;

using BackgammonByHoratiu.Entities;
using BackgammonByHoratiu.GameLogic.AI.Neural;
using BackgammonByHoratiu.GameLogic.AI.Search;
using BackgammonByHoratiu.GameLogic.GameManagers;

namespace BackgammonByHoratiu.GameLogic.AI
{
    internal sealed class BackgammonAi
    {
        readonly IGameManager game;
        readonly Queue<MoveAction> plannedMoves;

        // Self-play trained network (Ai/net.bgnn). Null when no usable file exists,
        // in which case the original search-based AI plays.
        readonly NeuralMovePlanner neuralPlanner;

        Task<List<MoveAction>> neuralPlanTask;
        bool useLegacySearchThisTurn;

        internal BackgammonAi(IGameManager game)
        {
            this.game = game;
            plannedMoves = new Queue<MoveAction>();
            neuralPlanner = NeuralMovePlanner.TryLoadDefault();
        }

        internal void Reset()
        {
            plannedMoves.Clear();

            // A plan that is still being computed is simply ignored when it finishes.
            neuralPlanTask = null;
            useLegacySearchThisTurn = false;
        }

        internal void TryPlayMove()
        {
            if (game.Player2.MovesLeft.Count == 0)
            {
                Reset();
                TryNextTurn();

                return;
            }

            if (neuralPlanTask is not null)
            {
                if (!neuralPlanTask.IsCompleted)
                {
                    return;
                }

                AcceptNeuralPlan();

                if (plannedMoves.Count == 0)
                {
                    TryNextTurn();

                    return;
                }

                ExecuteNextPlannedMove();

                return;
            }

            if (plannedMoves.Count == 0)
            {
                if (neuralPlanner is not null && !useLegacySearchThisTurn)
                {
                    StartNeuralPlan();

                    return;
                }

                PlanTurnWithLegacySearch();
            }

            if (plannedMoves.Count == 0)
            {
                TryNextTurn();

                return;
            }

            ExecuteNextPlannedMove();
        }

        // The search runs on a worker thread so the window keeps responding while the AI thinks.
        // It works on a private copy of the board, so the game state is never touched from there.
        void StartNeuralPlan()
        {
            BgPosition position = NeuralMovePlanner.CreatePosition(game);
            List<int> dice = [.. game.Player2.MovesLeft];

            neuralPlanTask = Task.Run(() => neuralPlanner.Plan(position, dice));
        }

        void AcceptNeuralPlan()
        {
            Task<List<MoveAction>> finished = neuralPlanTask;
            neuralPlanTask = null;

            if (finished.IsFaulted || finished.IsCanceled)
            {
                _ = finished.Exception;
                useLegacySearchThisTurn = true;
                PlanTurnWithLegacySearch();

                return;
            }

            foreach (MoveAction action in finished.Result)
            {
                plannedMoves.Enqueue(action);
            }
        }

        void PlanTurnWithLegacySearch()
        {
            BoardSnapshot snapshot = new(game);
            MoveSequenceResult bestSequence = MoveSearcher.FindBestSequence(snapshot);

            foreach (MoveAction action in bestSequence.Actions)
            {
                plannedMoves.Enqueue(action);
            }
        }

        void ExecuteNextPlannedMove()
        {
            MoveAction action = plannedMoves.Dequeue();

            try
            {
                DispatchMove(action);
            }
            catch (PieceMoveException)
            {
                // The game refused a planned move: drop the plan and use the original search
                // for the rest of this turn, so the AI can never get stuck repeating the same move.
                plannedMoves.Clear();
                useLegacySearchThisTurn = true;
            }
        }

        void DispatchMove(MoveAction action)
        {
            switch (action.Type)
            {
                case MoveActionType.Normal:
                    game.MovePiece(action.SourceColumn, action.DieValue);
                    break;

                case MoveActionType.BarEntry:
                    game.MoveOutedPiece(action.DieValue);
                    break;

                case MoveActionType.BearOff:
                    game.BearOffPiece(action.SourceColumn);
                    break;
            }
        }

        void TryNextTurn()
        {
            try
            {
                game.NextTurn();
            }
            catch (PieceMoveException)
            {
            }
        }
    }
}
