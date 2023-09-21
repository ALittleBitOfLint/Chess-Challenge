using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public class MyBot : IChessBot
{
    public MyBot()
    {

    }

    public Move Think(Board board, ChessChallenge.API.Timer timer)
    {
        Move[] moves = board.GetLegalMoves();
        int curScore = int.MinValue;
        Move bestMove = moves[0];
        int greatestScore = int.MinValue;

        foreach (var move in moves)
        {
            curScore = EvalMove(board, move);
            if (greatestScore < curScore)
            {
                greatestScore = curScore;
                bestMove = move;
            }
        }

        return bestMove;
    }


    private int EvalMove(Board board, Move move)
    {
        // base score using chess scoring
        board.MakeMove(move);

        int whitePiecesScore = GetTotalPieceScore(board, true);
        whitePiecesScore -= GetTotalPieceScore(board, false);

        // score for taking pieces is x100 greater than other scores
        whitePiecesScore *= 100;

        // coverage score
        whitePiecesScore += EvalTotalCoverageScore(board, true);


        board.UndoMove(move);

        return whitePiecesScore;
    }

    private int GetTotalPieceScore(Board board, bool scoreForWhite)
    {
        int score = 0;
        /// Gets an array of all the piece lists. In order these are:
        /// Pawns(white), Knights (white), Bishops (white), Rooks (white), Queens (white), King (white),
        /// Pawns (black), Knights (black), Bishops (black), Rooks (black), Queens (black), King (black)
        var piecesLists = board.GetAllPieceLists();
        int index = scoreForWhite ? 0 : 6;

        // Pawns
        score += piecesLists[index].Count() * GetPieceScore(PieceType.Pawn);
        // Knights
        score += piecesLists[++index].Count() * GetPieceScore(PieceType.Knight);
        // Bishops
        score += piecesLists[++index].Count() * GetPieceScore(PieceType.Bishop);
        // Rooks
        score += piecesLists[++index].Count() * GetPieceScore(PieceType.Rook);
        // Queens
        score += piecesLists[++index].Count() * GetPieceScore(PieceType.Queen);
        // King
        score += piecesLists[++index].Count() * GetPieceScore(PieceType.King);

        return score;
    }

    private int EvalTotalCoverageScore(Board board, bool scoreForWhite)
    {
        int coverageScore = 0;
        int threatScore = 0;

        var piecesLists = board.GetAllPieceLists().SelectMany(x => x);
        foreach (var piece in piecesLists)
        {
            if(piece.IsWhite == scoreForWhite)
            {
                var result = EvalPieceCoverage(board, piece);
                coverageScore += result.Item1;
                threatScore += result.Item2.Sum(x => GetPieceScore(x.PieceType));
            }
        }

        return coverageScore + (threatScore * 2);
    }

    private (int, IEnumerable<Piece>) EvalPieceCoverage(Board board, Piece piece)
    {
        List<Piece> pieces = new List<Piece>();
        (int, Piece) result;
        int pieceCoverageScore = 0;
        (int, List<Piece>) covResult;

        switch (piece.PieceType)
        {
            case PieceType.Pawn:
                result = EvalSquareCoverage(board, piece, piece.IsWhite ? 1 : -1, 1, 1);
                pieceCoverageScore += result.Item1;
                pieces.Add(result.Item2);

                result = EvalSquareCoverage(board, piece, piece.IsWhite ? 1 : -1, 1, 1);
                pieceCoverageScore += result.Item1;
                pieces.Add(result.Item2);
                break;
            case PieceType.Knight:
                // Loop over all possible knight moves
                int[] knightMoves = { 2,1,  2,-1,  1,2,  -1,2,  -2,1, -2,-1,  1,-2,  -1,2};
                for (int i = 0; i < knightMoves.Length; i += 2)
                {
                    result = EvalSquareCoverage(board, piece, knightMoves[i], knightMoves[i + 1], 1);
                    pieceCoverageScore += result.Item1;
                    pieces.Add(result.Item2);
                }
                break;
            case PieceType.Bishop:
                (pieceCoverageScore, pieces) = EvalDiagnalSquareCoverage(board, piece);
                break;
            case PieceType.Rook:
                (pieceCoverageScore, pieces) = EvalVertAndHorizSquareCoverage(board, piece);
                break;
            case PieceType.Queen:
                covResult = EvalVertAndHorizSquareCoverage(board, piece);
                pieceCoverageScore += covResult.Item1;
                pieces.AddRange(covResult.Item2);

                (pieceCoverageScore, pieces) = EvalDiagnalSquareCoverage(board, piece);
                pieceCoverageScore += covResult.Item1;
                pieces.AddRange(covResult.Item2);
                break;
            case PieceType.King:
                covResult = EvalVertAndHorizSquareCoverage(board, piece, 1);
                pieceCoverageScore += covResult.Item1;
                pieces.AddRange(covResult.Item2);

                (pieceCoverageScore, pieces) = EvalDiagnalSquareCoverage(board, piece, 1);
                pieceCoverageScore += covResult.Item1;
                pieces.AddRange(covResult.Item2);
                break;
            default:
                throw new ArgumentException("Invalid piece type");
        }

        return (pieceCoverageScore, pieces);
    }

    public (int, List<Piece>) EvalVertAndHorizSquareCoverage(Board board, Piece piece, int maxSquares = 8)
    {
        List<Piece> pieces = new List<Piece>();
        (int, Piece) result;
        int pieceCoverageScore = 0;

        // Horizontal and vertical movements
        result = EvalSquareCoverage(board, piece, 1, 0, maxSquares);
        pieceCoverageScore += result.Item1;
        pieces.Add(result.Item2);

        result = EvalSquareCoverage(board, piece, -1, 0, maxSquares);
        pieceCoverageScore += result.Item1;
        pieces.Add(result.Item2);

        result = EvalSquareCoverage(board, piece, 0, 1, maxSquares);
        pieceCoverageScore += result.Item1;
        pieces.Add(result.Item2);

        result = EvalSquareCoverage(board, piece, 0, -1, maxSquares);
        pieceCoverageScore += result.Item1;
        pieces.Add(result.Item2);

        return (pieceCoverageScore, pieces);
    }

    public (int, List<Piece>) EvalDiagnalSquareCoverage(Board board, Piece piece, int maxSquares = 8)
    {
        List<Piece> pieces = new List<Piece>();
        (int, Piece) result;
        int pieceCoverageScore = 0;

        // Diagonal movements
        result = EvalSquareCoverage(board, piece, 1, 1);
        pieceCoverageScore += result.Item1;
        pieces.Add(result.Item2);

        result = EvalSquareCoverage(board, piece, 1, -1);
        pieceCoverageScore += result.Item1;
        pieces.Add(result.Item2);

        result = EvalSquareCoverage(board, piece, -1, 1);
        pieceCoverageScore += result.Item1;
        pieces.Add(result.Item2);

        result = EvalSquareCoverage(board, piece, -1, -1);
        pieceCoverageScore += result.Item1;
        pieces.Add(result.Item2);

        return (pieceCoverageScore, pieces);
    }

    // u - up, d - down, l - left, r - right
    private (int, Piece) EvalSquareCoverage(Board board, Piece startingPiece, int rankDelta, int fileDelta, int maxSquares = 8)
    {
        Square curSquare = startingPiece.Square;
        Square newSquare;
        int numSquares = 0;
        Piece piece = new Piece(PieceType.None, true, curSquare);

        while (numSquares < maxSquares)
        {
            newSquare = new Square(curSquare.File + fileDelta, curSquare.Rank + rankDelta);

            if (!SquareIsOnBoard(newSquare))
            {
                break;
            }

            curSquare = newSquare;
            piece = board.GetPiece(curSquare);

            if (piece.PieceType != PieceType.None)
            {
                break;
            }

            numSquares++;
        }

        return (numSquares, piece);
    }


    private int GetPieceScore(PieceType pieceType)
    {
        switch (pieceType)
        {
            case PieceType.Pawn:
                return 1;
            case PieceType.Knight:
                return 3;
            case PieceType.Bishop:
                return 3;
            case PieceType.Rook:
                return 5;
            case PieceType.Queen:
                return 9;
            case PieceType.King:
                return 100;
            default:
                return 0;
        }
    }
    private static bool SquareIsOnBoard(Square square) => square.Index > 0 && square.Index < 63;

}