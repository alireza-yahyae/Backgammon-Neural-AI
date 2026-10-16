using System;

namespace BackgammonByHoratiu.GameLogic.AI.Neural
{
    /// <summary>
    /// Position encoding for the value network: 214 numbers, seen from the player about to move
    /// (a port of features.py, feature version 2).
    /// </summary>
    internal static class FeatureEncoder
    {
        internal const int BaseCount = 196;
        internal const int FeatureCount = 214;
        internal const int FeatureVersion = 2;

        internal static float[] Encode(BgPosition position, int player)
        {
            int opponent = 1 - player;
            int[] mine = new int[BgPosition.PointCount];
            int[] opp = new int[BgPosition.PointCount];

            // The mover's own point order, with the mover's checkers positive.
            for (int k = 0; k < BgPosition.PointCount; k++)
            {
                int value = player == 0 ? position.Board[k] : -position.Board[BgPosition.PointCount - 1 - k];
                mine[k] = Math.Max(value, 0);
                opp[k] = Math.Max(-value, 0);
            }

            int myBar = position.Bar[player];
            int oppBar = position.Bar[opponent];
            int myOff = position.Off[player];
            int oppOff = position.Off[opponent];

            float[] x = new float[FeatureCount];

            for (int k = 0; k < BgPosition.PointCount; k++)
            {
                WriteUnits(x, k * 4, mine[k]);
                WriteUnits(x, 96 + k * 4, opp[k]);
            }

            x[192] = myBar / 2f;
            x[193] = oppBar / 2f;
            x[194] = myOff / 15f;
            x[195] = oppOff / 15f;

            float myPips = 25f * myBar;
            float oppPips = 25f * oppBar;

            for (int k = 0; k < BgPosition.PointCount; k++)
            {
                myPips += mine[k] * (k + 1);
                oppPips += opp[k] * (BgPosition.PointCount - k);
            }

            int myRearmost = -1;
            int oppFrontmost = 99;

            for (int k = 0; k < BgPosition.PointCount; k++)
            {
                if (mine[k] > 0)
                {
                    myRearmost = k;
                }

                if (opp[k] > 0 && oppFrontmost == 99)
                {
                    oppFrontmost = k;
                }
            }

            if (myBar > 0)
            {
                myRearmost = 24;
            }

            if (oppBar > 0)
            {
                oppFrontmost = -1;
            }

            bool contact = myRearmost > oppFrontmost;

            int myOutside = myBar;
            int oppOutside = oppBar;
            int myBlots = 0;
            int oppBlots = 0;
            int myHomePoints = 0;
            int oppHomePoints = 0;
            int myBackCheckers = myBar;
            int oppBackCheckers = oppBar;
            int myBackPoints = 0;
            int oppBackPoints = 0;

            for (int k = 0; k < BgPosition.PointCount; k++)
            {
                if (mine[k] == 1)
                {
                    myBlots++;
                }

                if (opp[k] == 1)
                {
                    oppBlots++;
                }

                if (k < 6)
                {
                    oppBackCheckers += opp[k];

                    if (mine[k] >= 2)
                    {
                        myHomePoints++;
                    }

                    if (opp[k] >= 2)
                    {
                        oppBackPoints++;
                    }
                }

                if (k >= 6)
                {
                    myOutside += mine[k];
                }

                if (k < 18)
                {
                    oppOutside += opp[k];
                }

                if (k >= 18)
                {
                    myBackCheckers += mine[k];

                    if (opp[k] >= 2)
                    {
                        oppHomePoints++;
                    }

                    if (mine[k] >= 2)
                    {
                        myBackPoints++;
                    }
                }
            }

            x[196] = myPips / 200f;
            x[197] = oppPips / 200f;
            x[198] = Math.Clamp((oppPips - myPips) / 40f, -2f, 2f);
            x[199] = contact ? 1f : 0f;
            x[200] = myBlots / 4f;
            x[201] = oppBlots / 4f;
            x[202] = myHomePoints / 6f;
            x[203] = oppHomePoints / 6f;
            x[204] = LongestRun(mine) / 6f;
            x[205] = LongestRun(opp) / 6f;
            x[206] = myBackCheckers / 15f;
            x[207] = oppBackCheckers / 15f;
            x[208] = myBackPoints / 6f;
            x[209] = oppBackPoints / 6f;
            x[210] = myOutside / 15f;
            x[211] = oppOutside / 15f;
            x[212] = myOutside == 0 ? 1f : 0f;
            x[213] = oppOutside == 0 ? 1f : 0f;

            return x;
        }

        static void WriteUnits(float[] x, int offset, int count)
        {
            x[offset] = count >= 1 ? 1f : 0f;
            x[offset + 1] = count >= 2 ? 1f : 0f;
            x[offset + 2] = count >= 3 ? 1f : 0f;
            x[offset + 3] = Math.Max(count - 3, 0) / 2f;
        }

        // Longest run of consecutive points holding at least two checkers.
        static int LongestRun(int[] counts)
        {
            int best = 0;
            int run = 0;

            foreach (int count in counts)
            {
                if (count >= 2)
                {
                    run++;
                    best = Math.Max(best, run);
                }
                else
                {
                    run = 0;
                }
            }

            return best;
        }
    }
}
