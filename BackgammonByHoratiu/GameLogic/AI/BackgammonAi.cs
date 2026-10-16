using System.Collections.Generic;
using System.Threading.Tasks;

using BackgammonByHoratiu.Entities;
using BackgammonByHoratiu.GameLogic.AI.Neural;
using BackgammonByHoratiu.GameLogic.AI.Search;
using BackgammonByHoratiu.GameLogic.GameManagers;

namespace BackgammonByHoratiu.GameLogic.AI
{
    /// <summary>
    /// Plays Player 2 with the self-play trained network (Ai/net.bgnn, see NeuralMovePlanner).
    /// </summary>
    internal sealed class BackgammonAi
    {
        // After this many refused plans in one turn the AI stops trying and passes the turn.
        const int MaxRefusedPlansPerTurn = 3;

        readonly IGameManager game;
        readonly Queue<MoveAction> plannedMoves;

        Task<List<MoveAction>> planTask;
        int refusedPlans;

        internal BackgammonAi(IGameManager game)
        {
            this.game = game;
            plannedMoves = new Queue<MoveAction>();
        }

        internal void Reset()
        {
            plannedMoves.Clear();

            // A plan that is still being computed is simply ignored when it finishes.
            planTask = null;
            refusedPlans = 0;
        }

        internal void TryPlayMove()
        {
            if (game.Player2.MovesLeft.Count == 0)
            {
                Reset();
                TryNextTurn();

                return;
            }

            if (refusedPlans >= MaxRefusedPlansPerTurn)
            {
                TryNextTurn();

                return;
            }

            if (planTask is not null)
            {
                if (!planTask.IsCompleted)
                {
                    return;
                }

                AcceptPlan();

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
                StartPlan();

                return;
            }

            ExecuteNextPlannedMove();
        }

        // The search runs on a worker thread so the window keeps responding while the AI thinks.
        // It works on a private copy of the board, so the game state is never touched from there.
        void StartPlan()
        {
            NeuralMovePlanner planner = NeuralMovePlanner.Shared;
            BgPosition position = NeuralMovePlanner.CreatePosition(game);
            List<int> dice = [.. game.Player2.MovesLeft];

            planTask = Task.Run(() => planner.Plan(position, dice));
        }

        void AcceptPlan()
        {
            Task<List<MoveAction>> finished = planTask;
            planTask = null;

            // A crash inside the search is a bug, not something to hide: let it surface.
            foreach (MoveAction action in finished.GetAwaiter().GetResult())
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
                // The game refused a planned move: drop the plan and plan again from the current board.
                plannedMoves.Clear();
                refusedPlans++;
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
