using ChessChallenge.API;
using ChessChallenge.Chess;
using ChessChallenge.Example;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ChessChallenge.Application
{
    /// <summary>
    /// Runs multiple chess games in parallel using Tasks
    /// </summary>
    public class ParallelBatchRunner
    {
        public class BatchStats
        {
            private int botAWins;
            private int botALosses;
            private int botADraws;
            private int botATimeouts;
            private int botAIllegalMoves;
            private int botBWins;
            private int botBLosses;
            private int botBDraws;
            private int botBTimeouts;
            private int botBIllegalMoves;
            private int gamesCompleted;

            public int BotAWins => botAWins;
            public int BotALosses => botALosses;
            public int BotADraws => botADraws;
            public int BotATimeouts => botATimeouts;
            public int BotAIllegalMoves => botAIllegalMoves;
            public int BotBWins => botBWins;
            public int BotBLosses => botBLosses;
            public int BotBDraws => botBDraws;
            public int BotBTimeouts => botBTimeouts;
            public int BotBIllegalMoves => botBIllegalMoves;
            public int GamesCompleted => gamesCompleted;

            public void RecordResult(HeadlessGameRunner.GameResult result)
            {
                bool botAWasWhite = result.BotAWasWhite;
                var gameResult = result.Result;

                // Check for draw
                if (Arbiter.IsDrawResult(gameResult))
                {
                    Interlocked.Increment(ref botADraws);
                    Interlocked.Increment(ref botBDraws);
                }
                // White won
                else if (Arbiter.IsWhiteWinsResult(gameResult))
                {
                    if (botAWasWhite)
                    {
                        Interlocked.Increment(ref botAWins);
                        Interlocked.Increment(ref botBLosses);
                        if (gameResult == GameResult.BlackTimeout) Interlocked.Increment(ref botBTimeouts);
                        if (gameResult == GameResult.BlackIllegalMove) Interlocked.Increment(ref botBIllegalMoves);
                    }
                    else
                    {
                        Interlocked.Increment(ref botBWins);
                        Interlocked.Increment(ref botALosses);
                        if (gameResult == GameResult.WhiteTimeout) Interlocked.Increment(ref botATimeouts);
                        if (gameResult == GameResult.WhiteIllegalMove) Interlocked.Increment(ref botAIllegalMoves);
                    }
                }
                // Black won
                else if (Arbiter.IsBlackWinsResult(gameResult))
                {
                    if (botAWasWhite)
                    {
                        Interlocked.Increment(ref botBWins);
                        Interlocked.Increment(ref botALosses);
                        if (gameResult == GameResult.WhiteTimeout) Interlocked.Increment(ref botATimeouts);
                        if (gameResult == GameResult.WhiteIllegalMove) Interlocked.Increment(ref botAIllegalMoves);
                    }
                    else
                    {
                        Interlocked.Increment(ref botAWins);
                        Interlocked.Increment(ref botBLosses);
                        if (gameResult == GameResult.BlackTimeout) Interlocked.Increment(ref botBTimeouts);
                        if (gameResult == GameResult.BlackIllegalMove) Interlocked.Increment(ref botBIllegalMoves);
                    }
                }

                Interlocked.Increment(ref gamesCompleted);
            }

            public void PrintStats(string botAName, string botBName)
            {
                Console.WriteLine("\n=== Final Match Statistics ===");
                Console.WriteLine($"\n{botAName}:");
                Console.WriteLine($"  Wins: {BotAWins}");
                Console.WriteLine($"  Losses: {BotALosses}");
                Console.WriteLine($"  Draws: {BotADraws}");
                Console.WriteLine($"  Timeouts: {BotATimeouts}");
                Console.WriteLine($"  Illegal Moves: {BotAIllegalMoves}");
                Console.WriteLine($"\n{botBName}:");
                Console.WriteLine($"  Wins: {BotBWins}");
                Console.WriteLine($"  Losses: {BotBLosses}");
                Console.WriteLine($"  Draws: {BotBDraws}");
                Console.WriteLine($"  Timeouts: {BotBTimeouts}");
                Console.WriteLine($"  Illegal Moves: {BotBIllegalMoves}");
                Console.WriteLine($"\nTotal Games: {GamesCompleted}");
            }
        }

        private readonly string[] startingFens;
        private readonly int maxParallelGames;

        public ParallelBatchRunner(string[] startingFens, int maxParallelGames = 200)
        {
            this.startingFens = startingFens;
            this.maxParallelGames = maxParallelGames;
        }

        /// <summary>
        /// Run all games in parallel with progress reporting
        /// </summary>
        public async Task<BatchStats> RunAllGamesAsync(IChessBot botA, IChessBot botB, string botAName = "Bot A", string botBName = "Bot B")
        {
            var stats = new BatchStats();
            var totalGames = startingFens.Length * 2; // Each FEN played twice (swap colors)

            Console.WriteLine($"Starting parallel batch run: {botAName} vs {botBName}");
            Console.WriteLine($"Total games: {totalGames}");
            Console.WriteLine($"Max parallel games: {maxParallelGames}");
            Console.WriteLine($"Processor count: {Environment.ProcessorCount}");
            Console.WriteLine();

            var stopwatch = Stopwatch.StartNew();

            void PrintProgress(Stopwatch watch)
            {
                var elapsed = stopwatch.Elapsed;
                var completed = stats.GamesCompleted;
                var rate = completed / Math.Max(elapsed.TotalSeconds, 0.0001);
                var remaining = totalGames - completed;
                var eta = remaining / Math.Max(rate, 0.0001);

                Console.WriteLine(
                    $"Progress: {completed}/{totalGames} ({(completed * 100.0 / totalGames):F1}%) | " +
                    $"Rate: {rate:F1} games/sec | " +
                    $"Time spent on game: {watch.Elapsed:mm\\:ss} | " +
                    $"Elapsed: {elapsed:mm\\:ss} | " +
                    $"ETA: {TimeSpan.FromSeconds(eta):mm\\:ss} | " +
                    $"{botAName} {stats.BotAWins}-{stats.BotALosses}-{stats.BotADraws}"
                );
            }
            // Create all game tasks
            var gameTasks = new List<Task>();

            // Use SemaphoreSlim to limit concurrent games
            using var semaphore = new SemaphoreSlim(maxParallelGames);


            // Create tasks for all games
            for (int fenIndex = 0; fenIndex < startingFens.Length; fenIndex++)
            {
                var fen = startingFens[fenIndex];
                var gameIndex = fenIndex * 2;

                // Game 1: Bot A as white
                gameTasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var watch = Stopwatch.StartNew();
                        var runner = new HeadlessGameRunner(botA, botB);
                        var result = runner.RunGame(fen, gameIndex, botAPlaysWhite: true);
                        stats.RecordResult(result);
                        PrintProgress(watch);
                        watch.Stop();
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));

                // Game 2: Bot A as black
                gameTasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var watch = Stopwatch.StartNew();
                        var runner = new HeadlessGameRunner(botA, botB);
                        var result = runner.RunGame(fen, gameIndex + 1, botAPlaysWhite: false);
                        stats.RecordResult(result);
                        PrintProgress(watch);
                        watch.Stop();
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            // Wait for all games to complete
            await Task.WhenAll(gameTasks);

            stopwatch.Stop();

            // Final report
            Console.WriteLine($"\n=== Batch Complete ===");
            Console.WriteLine($"Total time: {stopwatch.Elapsed:mm\\:ss\\.ff}");
            Console.WriteLine($"Average game rate: {totalGames / stopwatch.Elapsed.TotalSeconds:F2} games/second");
            Console.WriteLine($"Average time per game: {stopwatch.Elapsed.TotalSeconds / totalGames:F2} seconds");

            stats.PrintStats(botAName, botBName);

            return stats;
        }

        /// <summary>
        /// Run specific number of games in parallel (for testing with 200 games)
        /// </summary>
        public async Task<BatchStats> RunNGamesAsync(int numGames, IChessBot botA, IChessBot botB, string botAName = "Bot A", string botBName = "Bot B")
        {
            // Take only the first N/2 FENs (since we play each twice)
            var fensToUse = startingFens.Take(numGames / 2).ToArray();
            var tempRunner = new ParallelBatchRunner(fensToUse, maxParallelGames);
            return await tempRunner.RunAllGamesAsync(botA, botB, botAName, botBName);
        }
    }
}
