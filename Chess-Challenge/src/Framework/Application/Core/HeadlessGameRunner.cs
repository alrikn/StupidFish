using ChessChallenge.API;
using ChessChallenge.Chess;
using ChessChallenge.Example;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using APIBoard = ChessChallenge.API.Board;
using APIMove = ChessChallenge.API.Move;
using APITimer = ChessChallenge.API.Timer;

namespace ChessChallenge.Application
{
    /// <summary>
    /// Runs chess games headlessly without UI for parallel execution
    /// </summary>
    public class HeadlessGameRunner
    {
        public class GameResult
        {
            public Chess.GameResult Result { get; set; }
            public string FenPosition { get; set; }
            public int GameIndex { get; set; }
            public bool BotAWasWhite { get; set; }
            public double DurationSeconds { get; set; }
        }

        private readonly IChessBot botA;
        private readonly IChessBot botB;
        private readonly int gameDurationMs;
        private readonly int incrementMs;
        private readonly MoveGenerator moveGenerator;

        public HeadlessGameRunner(IChessBot botA, IChessBot botB, int gameDurationMs = 60000, int incrementMs = 0)
        {
            this.botA = botA;
            this.botB = botB;
            this.gameDurationMs = gameDurationMs;
            this.incrementMs = incrementMs;
            this.moveGenerator = new MoveGenerator();
        }

        /// <summary>
        /// Run a single game from a starting FEN position
        /// </summary>
        public GameResult RunGame(string startFen, int gameIndex, bool botAPlaysWhite)
        {
            var startTime = DateTime.UtcNow;
            var board = new Chess.Board();
            board.LoadPosition(startFen);

            // Create players with time control
            var whiteBot = botAPlaysWhite ? botA : botB;
            var blackBot = botAPlaysWhite ? botB : botA;

            double whiteTimeMs = gameDurationMs;
            double blackTimeMs = gameDurationMs;

            Chess.GameResult result = Chess.GameResult.InProgress;

            // Game loop
            while (result == Chess.GameResult.InProgress)
            {
                var playerBot = board.IsWhiteToMove ? whiteBot : blackBot;
                ref double playerTimeMs = ref (board.IsWhiteToMove ? ref whiteTimeMs : ref blackTimeMs);
                double opponentTimeMs = board.IsWhiteToMove ? blackTimeMs : whiteTimeMs;

                // Check for timeout
                if (playerTimeMs <= 0)
                {
                    result = board.IsWhiteToMove ? Chess.GameResult.WhiteTimeout : Chess.GameResult.BlackTimeout;
                    break;
                }

                // Get bot move with time tracking
                var thinkStartTime = DateTime.UtcNow;
                APIBoard apiBoard = new APIBoard(board);
                APITimer timer = new APITimer((int)playerTimeMs, (int)opponentTimeMs, gameDurationMs, incrementMs);

                Chess.Move chosenMove;
                try
                {
                    APIMove apiMove = playerBot.Think(apiBoard, timer);
                    chosenMove = new Chess.Move(apiMove.RawValue);
                }
                catch (Exception)
                {
                    // Bot crashed or threw exception - illegal move
                    result = board.IsWhiteToMove ? Chess.GameResult.WhiteIllegalMove : Chess.GameResult.BlackIllegalMove;
                    break;
                }

                var thinkDuration = (DateTime.UtcNow - thinkStartTime).TotalMilliseconds;
                playerTimeMs -= thinkDuration;

                // Validate move
                if (!IsLegalMove(board, chosenMove))
                {
                    result = board.IsWhiteToMove ? Chess.GameResult.WhiteIllegalMove : Chess.GameResult.BlackIllegalMove;
                    break;
                }

                // Apply increment
                playerTimeMs += incrementMs;

                // Make move
                board.MakeMove(chosenMove, inSearch: false);

                // Check game state
                result = Arbiter.GetGameState(board);
            }

            return new GameResult
            {
                Result = result,
                FenPosition = startFen,
                GameIndex = gameIndex,
                BotAWasWhite = botAPlaysWhite,
                DurationSeconds = (DateTime.UtcNow - startTime).TotalSeconds
            };
        }

        private bool IsLegalMove(Chess.Board board, Chess.Move givenMove)
        {
            var moves = moveGenerator.GenerateMoves(board);
            foreach (var legalMove in moves)
            {
                if (givenMove.Value == legalMove.Value)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
