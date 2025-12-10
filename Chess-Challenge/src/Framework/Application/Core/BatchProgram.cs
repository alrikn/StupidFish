using ChessChallenge.Example;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ChessChallenge.Application
{
    /// <summary>
    /// Entry point for headless batch mode - runs games in parallel without UI
    /// Usage: Pass --batch as command line argument to run in batch mode
    /// Optional: --games=N to specify number of games (default: all games from Fens.txt * 2)
    /// Optional: --parallel=N to specify max parallel games (default: 200)
    /// </summary>
    static class BatchProgram
    {
        public static async Task RunBatchMode(string[] args)
        {
            Console.WriteLine("=== Chess Challenge - Batch Mode ===\n");

            // Parse command line arguments
            int numGames = -1; // -1 means all games
            int maxParallel = 200;

            foreach (var arg in args)
            {
                if (arg.StartsWith("--games="))
                {
                    if (int.TryParse(arg.Substring("--games=".Length), out int parsed))
                    {
                        numGames = parsed;
                    }
                }
                else if (arg.StartsWith("--parallel="))
                {
                    if (int.TryParse(arg.Substring("--parallel=".Length), out int parsed))
                    {
                        maxParallel = parsed;
                    }
                }
            }

            // Load FENs
            string[] fens = FileHelper.ReadResourceFile("Fens.txt")
                .Split('\n')
                .Where(fen => fen.Length > 0)
                .ToArray();

            Console.WriteLine($"Loaded {fens.Length} starting positions");

            // Create bots no longer needed cus we are creating new instances
            //var myBot = new MyBot();
            //var evilBot = new V7_bot();

            // Create batch runner
            var batchRunner = new ParallelBatchRunner(fens, maxParallel);

            // Run games
            ParallelBatchRunner.BatchStats stats;
            if (numGames > 0)
            {
                Console.WriteLine($"Running {numGames} games...\n");
                // Pass factories so each game gets a fresh bot instance
                stats = await batchRunner.RunNGamesAsync(numGames, () => new MyBot(), () => new V7_bot(), "MyBot (StupidFish)", "EvilBot");
            }
            else
            {
                Console.WriteLine($"Running all games ({fens.Length * 2})...\n");
                stats = await batchRunner.RunAllGamesAsync(() => new MyBot(), () => new V7_bot(), "MyBot (StupidFish)", "EvilBot");
            }

            // Write results to file
            WriteResultsToFile(stats);

            Console.WriteLine("\nResults written to bot_match_results.txt");
            Console.WriteLine("Batch mode complete!");
        }

        private static void WriteResultsToFile(ParallelBatchRunner.BatchStats stats)
        {
            var lines = new[]
            {
                $"MyBot Wins: {stats.BotAWins}",
                $"MyBot Losses: {stats.BotALosses}",
                $"MyBot Draws: {stats.BotADraws}",
                $"MyBot Timeouts: {stats.BotATimeouts}",
                $"MyBot Illegal Moves: {stats.BotAIllegalMoves}",
                $"EvilBot Wins: {stats.BotBWins}",
                $"EvilBot Losses: {stats.BotBLosses}",
                $"EvilBot Draws: {stats.BotBDraws}",
                $"EvilBot Timeouts: {stats.BotBTimeouts}",
                $"EvilBot Illegal Moves: {stats.BotBIllegalMoves}",
                $"Total Games: {stats.GamesCompleted}"
            };

            File.WriteAllLines("bot_match_results.txt", lines);
        }
    }
}
