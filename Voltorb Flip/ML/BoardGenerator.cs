using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Voltorb_Flip.Calculator;

namespace Voltorb_Flip.ML
{
    class BoardGenerator
    {
        // Number of boards for training
        const int NUM_BOARDS = 10000;
        readonly Random rng;

        // List of Coordinates
        readonly List<(int r, int c)> _coordinates;

        public BoardGenerator()
        {
            // Initialize random seed
            rng = new();

            // Store random coordinates
            List<int> rows = Enumerable.Range(0, 5).ToList();
            _coordinates = rows.Join(rows, r => true, c => true, (r, c) => (r, c)).ToList();
        }

        /// <summary>
        /// Generates a random board at a random level of the game
        /// </summary>
        /// <returns>The <see cref="Board"/> object created</returns>
        public Board Generate()
        {
            // Randomize level
            int level = rng.Next(8) + 1;
            return Generate(level);
        }
        /// <summary>
        /// Generates a random board at a the given level of the game
        /// </summary>
        /// <returns>The <see cref="Board"/> object created</returns>
        public Board Generate(int level)
        {
            // Randomize board layout
            (int, int, int)[] possiblePoints = Solver.PossibleBoards[level - 1];
            (int twos, int threes, int voltorbs) = possiblePoints[rng.Next(5)];
            int[] layout = { twos, threes, voltorbs };

            // Copy coordinates
            List<(int r, int c)> coordinates = new(_coordinates);

            // Set up board
            int[] board = new int[25]; // 5 x 5
            int[] pointNums = new int[20]; // 2 x 5 x 2
            // Randomly block out some coordinates
            int[] knownBoard = new int[25];
            int numBlocked = rng.Next(25 - voltorbs);

            // Set 2s, 3s, and Vs
            for (int i = 0; i < layout.Length; i++)
            {
                for (int j = 0; j < layout[i]; j++)
                {
                    int idx = rng.Next(coordinates.Count);
                    (int row, int col) = coordinates[idx];
                    coordinates.RemoveAt(idx);

                    // Voltorb
                    if (i == 2)
                    {
                        pointNums[2 * row + 1]++;
                        pointNums[10 + 2 * col + 1]++;
                        board[5 * row + col] = 1; // 1 is Voltorb here
                        // Don't put voltorbs on known board
                        knownBoard[5 * row + col] = -1; // -1 is Unknown
                    }
                    else // 2 or 3
                    {
                        pointNums[2 * row] += 2 + i;
                        pointNums[10 + 2 * col] += 2 + i;
                        board[5 * row + col] = 2 + i;
                        knownBoard[5 * row + col] = 2 + i;
                    }
                }
            }

            // Add 1 point for remaining coordinates
            foreach ((int row, int col) in coordinates)
            {
                pointNums[2 * row]++;
                pointNums[10 + 2 * col]++;
            }

            // Block out random coordinates
            coordinates = new(_coordinates); // Reset coordinates
            for (int i = 0; i < numBlocked; i++)
            {
                int idx = rng.Next(coordinates.Count);
                (int row, int col) = coordinates[idx];
                coordinates.RemoveAt(idx);

                // If this square is Voltorb, it's already unkown, so skip over it
                if (board[5 * row + col] == 1) { i++; continue; }

                // -1 here means unknown
                knownBoard[5 * row + col] = -1;
            }

            return new Board()
            {
                Level = level,
                VoltorbNumbers = pointNums,
                KnownBoardState = knownBoard,
                FullBoardState = board
            };
        }
    
        /// <summary>
        /// Generate a bunch of <see cref="Board"/>s and write their data to a
        /// training file.
        /// </summary>
        public void CreateTrainingData()
        {
            List<Board> trainingData = new();
            while (trainingData.Count < NUM_BOARDS)
            {
                Board b = Generate();
                if (!trainingData.Contains(b)) trainingData.Add(b); // Uses Board.Equals for comparison
            }
            
            // Initialize ML data
            MLContext mlContext = new();
            IDataView data = mlContext.Data.LoadFromEnumerable(trainingData);

            // Write to file
            string path = @"D:\\Other Stuff\\Voltorb Flip\\Voltorb Flip\\ML\\training_data.csv";
            File.WriteAllText(path, "Level(1), Voltorb Numbers(20), Board State(25)\r\n");
            using (FileStream stream = new(path, FileMode.Append))
                mlContext.Data.SaveAsText(data, stream);
        }
    }
}
