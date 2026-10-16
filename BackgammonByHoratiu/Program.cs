using System;
using System.IO;

using BackgammonByHoratiu.GameLogic.AI.Neural;
using BackgammonByHoratiu.Settings;

namespace BackgammonByHoratiu
{
    static class Program
    {
        public static GameWindow Game { get; private set; }

        internal static void RunGame()
        {
            Game = new GameWindow();
            Game.Run();
            Game.Dispose();
        }

        internal static void PrepareFiles()
        {
            if (!Directory.Exists(ApplicationPaths.UserDataDirectory))
            {
                Directory.CreateDirectory(ApplicationPaths.UserDataDirectory);
            }
        }

        [STAThread]
        static void Main()
        {
            PrepareFiles();

            try
            {
                NeuralMovePlanner.EnsureLoaded();
            }
            catch (Exception exception) when (
                exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine(exception.Message);
                Environment.ExitCode = 2;

                return;
            }

            RunGame();
        }
    }
}
