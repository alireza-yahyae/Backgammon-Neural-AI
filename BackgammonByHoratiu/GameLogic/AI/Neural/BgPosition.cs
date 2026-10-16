using System;

namespace BackgammonByHoratiu.GameLogic.AI.Neural
{
    /// <summary>
    /// Two-sided position in the neural AI's own convention (the one its network was trained with).
    /// Board[i] &gt; 0: checkers of player 0 (the AI); Board[i] &lt; 0: checkers of player 1 (the human).
    /// Player 0 moves from high to low index (home board 0..5), player 1 from low to high (home board 18..23).
    /// </summary>
    internal sealed class BgPosition
    {
        internal const int PointCount = 24;
        internal const int CheckersPerSide = 15;

        internal int[] Board { get; }
        internal int[] Bar { get; }
        internal int[] Off { get; }

        internal BgPosition()
        {
            Board = new int[PointCount];
            Bar = new int[2];
            Off = new int[2];
        }

        internal BgPosition Copy()
        {
            BgPosition copy = new();

            Array.Copy(Board, copy.Board, PointCount);
            copy.Bar[0] = Bar[0];
            copy.Bar[1] = Bar[1];
            copy.Off[0] = Off[0];
            copy.Off[1] = Off[1];

            return copy;
        }

        // Compact key used to detect duplicate positions while generating turns.
        internal string Key()
        {
            char[] key = new char[PointCount + 4];

            for (int i = 0; i < PointCount; i++)
            {
                key[i] = (char)(Board[i] + CheckersPerSide);
            }

            key[PointCount] = (char)Bar[0];
            key[PointCount + 1] = (char)Bar[1];
            key[PointCount + 2] = (char)Off[0];
            key[PointCount + 3] = (char)Off[1];

            return new string(key);
        }

        // Readable form, identical to the one written by tools/make_golden.py.
        internal string ToText()
        {
            return string.Join(",", Board) + "|" + Bar[0] + "," + Bar[1] + "|" + Off[0] + "," + Off[1];
        }
    }
}
