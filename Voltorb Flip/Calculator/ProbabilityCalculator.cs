using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Voltorb_Flip.Calculator
{
    partial class ProbabilityCalculator
    {
        public byte[,] GameBoard { get; } = new byte[5,5];
        // X-value of Point represents point values per column, y-value is voltorb numbers
        Triple[,] VoltorbBoard { get; } = new Triple[2, 5]; // Row 1 is Vertical, 2 is Horizontal
        public List<byte>[,] PossibleValues { get; } = new List<byte>[5, 5];
        public byte[,] Probabilities { get; } = new byte[5, 5];

        readonly byte[] allPossible = { 0, 1, 2, 3 };

        record struct Triple(int Points, int Voltorbs, int Squares);

        public ProbabilityCalculator(MainWindow window)
        {
            this.window = window;

            // Initialize top rows of selected and unselected top-left squares
            Rectangle selectedBounds = new(0, 0, topLeftSelected.Width, 1);
            topRowSelected = topLeftSelected.Clone(selectedBounds, topLeftSelected.PixelFormat);
            Rectangle unselectedBounds = new(0, 0, topLeftUnselected.Width, 1);
            topRowUnselected = topLeftUnselected.Clone(unselectedBounds, topLeftUnselected.PixelFormat);

            // Initialize Reference Quantities
            topLeftSelectedHeight = topLeftSelected.Height;
            topLeftSelectedWidth = topLeftSelected.Width;
            topLeftSurroundingColor = topLeftSelected.GetPixel(0, 0);
            topLeftSelectedBorderColor = topLeftSelected.GetPixel(1, 0);
            topLeftUnselectedBorderColor = topLeftUnselected.GetPixel(1, 0);

            // Intialize Reference Voltorb Card Images
            voltorbBitmaps = new Bitmap[10];
            for (int i = 0; i < 10; i++)
            {
                voltorbBitmaps[i] = Image.FromFile(string.Format(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\voltorb{0}.png",
                    i + 1)) as Bitmap;
            }

            ResetBoard();
        }

        /// <summary>
        /// Resets the GameBoard and PossibleValues lists
        /// </summary>
        public void ResetBoard()
        {
            // Initialize all cards as hidden
            // Initialize empty lists in possible values
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 5; j++)
                {
                    GameBoard[i, j] = 4;
                    PossibleValues[i, j] = new();
                }
        }

        /// <summary>
        /// Fill in known quantities from game board
        /// </summary>
        void FillInKnownValues()
        {
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 5; j++)
                {
                    byte val = GameBoard[i, j];
                    if (val == 4)
                        PossibleValues[i, j].AddRange(allPossible);
                    else PossibleValues[i, j].Add(val);
                }
        }

        public void CalculateUnknowns()
        {
            // Loop through VoltorbBoard list to find row/column information
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    VoltorbBoard[i, j] = UpdateKnownValues(i, j, VoltorbBoard[i, j]);
                    Triple vals = VoltorbBoard[i, j];
                    int points = vals.Points;
                    int voltorbs = vals.Voltorbs;
                    int numSquares = vals.Squares;

                    // Check Voltorb Count
                    RuleAction VoltorbRule = null;
                    // Check if row/col has 0 voltorbs
                    if (voltorbs == 0) VoltorbRule = Voltorb0;
                    // If not, check for all voltorbs
                    else if (voltorbs == numSquares) VoltorbRule = Voltorb5;
                    // If not, check if there is 1 free position
                    else if (voltorbs == numSquares - 1) VoltorbRule = Voltorb4;
                    if (VoltorbRule != null) RuleTest(i, j, vals, VoltorbRule);

                    // Check Total of Points + Voltorbs
                    RuleAction TotalRule = null;
                    // Check if points + voltorb = numSquares
                    if (points + voltorbs == numSquares) TotalRule = Total5;
                    // If not, check if points + voltorb = numSquares + 1
                    else if (points + voltorbs == numSquares + 1) TotalRule = Total6;
                    if (TotalRule != null) RuleTest(i, j, vals, TotalRule);

                    // Check if points are too high for 1s
                    if (points >= 2 + (numSquares - 1) * 3) RuleTest(i, j, vals, No1s);
                }
            }
            // Loop through VoltorbBoard again to perform final eliminations
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    Triple vals = VoltorbBoard[i, j];
                    int points = vals.Points;
                    int voltorbs = vals.Voltorbs;
                    int freeSquares = vals.Squares - voltorbs;

                    // Check point values compared with voltorbs to determine how
                    // many 1s 2s and 3s there are in the row/column (minimum)
                    int num1s = 0;
                    int num2s = 0;
                    int num3s = 0;
                    if (points < 2 * freeSquares)
                        num1s = 2 * freeSquares - points;
                    else if (points > 2 * freeSquares)
                        num3s = points - 2 * freeSquares;
                    if (points % 2 != freeSquares % 2)
                        num2s = 1;

                    EliminatePossibilities(i, j, num1s, num2s, num3s);
                }
            }
            // Loop once again for Combination Analysis
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    Triple vals = VoltorbBoard[i, j];
                    int points = vals.Points;
                    int voltorbs = vals.Voltorbs;
                    int freeSquares = vals.Squares - voltorbs;

                    List<List<byte>> allCombinations = GetAllCombinations(points, freeSquares);
                    // Analyze differences between combinations to get more information
                    if (allCombinations.Count > 1)
                        //AnalyzeCombinations(allCombinations, i, j, points, freeSquares);

                    // Perform final voltorb sweep
                    VoltorbSweep(i, j, voltorbs);
                }
            }

            // Update game board based on guaranteed values
            bool updated = false;
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    if (GameBoard[r, c] == 4 && PossibleValues[r, c].Count == 1)
                    {
                        updated = true;
                        GameBoard[r, c] = PossibleValues[r, c][0];
                    }
                }
            }
            // Recheck with updated GameBoard
            //if (updated) CalculateUnknowns();
        }

        /// <summary>
        /// Loops through the provided row and column and eliminates possibilities
        /// based on the provided minimum numbers of 1s, 2s, and 3s
        /// </summary>
        /// <param name="i">The row of the VoltorbBoard</param>
        /// <param name="j">The index of the voltorb card within its row/column</param>
        /// <param name="num1s">Minimum number of 1s in the row (0 if unknown)</param>
        /// <param name="num2s">Minimum number of 2s in the row (0 if unknown)</param>
        /// <param name="num3s">Minimum number of 3s in the row (0 if unknown)</param>
        void EliminatePossibilities(int i, int j, int min1s, int min2s, int min3s)
        {
            int num1s = 0, num2s = 0, num3s = 0;
            for (int n = 0; n < 5; n++)
            {
                int row = i * n + (1 - i) * j;
                int col = (1 - i) * n + i * j;

                // Ignore flipped cards
                if (GameBoard[row, col] != 4) continue;

                List<byte> values = PossibleValues[row, col];
                foreach (byte val in values)
                {
                    switch (val)
                    {
                        case 1:
                            num1s++; break;
                        case 2:
                            num2s++; break;
                        case 3:
                            num3s++; break;
                    }
                }
            }
            // Loop through cards again to eliminate possibilities
            int[] allNums = { num1s, num2s, num3s };
            int[] allMins = { min1s, min2s, min3s };
            for (int n = 0; n < 5; n++)
            {
                int row = i * n + (1 - i) * j;
                int col = (1 - i) * n + i * j;

                if (GameBoard[row, col] != 4) continue;

                List<byte> values = PossibleValues[row, col];
                for (byte idx = 1; idx < 4; idx++)
                {
                    int num = allNums[idx - 1];
                    int min = allMins[idx - 1];
                    if (num == min && values.Contains(idx))
                        values.RemoveAll(x => x != idx);
                    else if (num < min) values.Remove((byte)num);
                }
            }
        }

        void AnalyzeCombinations(List<List<byte>> allCombinations, int i, int j, int totalPoints, int freeSquares)
        {
            // 5 bit integer
            byte twos = 0x00000;
            byte threes = 0x00000;
            int num2s = 0, num3s = 0;
            // Look through cards to find all occurrences of 2 and 3
            for (int n = 0; n < 5; n++)
            {
                int row = i * n + (1 - i) * j;
                int col = (1 - i) * n + i * j;
                
                // Ignore flipped cards
                if (GameBoard[row, col] != 4) continue;

                List<byte> values = PossibleValues[row, col];

                if (values.Contains(2))
                {
                    twos |= (byte)(1 << n);
                    num2s++;
                }
                if (values.Contains(3))
                {
                    threes |= (byte)(1 << n);
                    num3s++;
                }
            }

            // Check Combinations for guaranteed values
            byte guaranteed = 0x00000;
            // Check each combination for the same number of 2s or 3s
            foreach (List<byte> combination in allCombinations)
            {
                // Check if no guaranteed yet but the number of 2s matches
                if (num2s == combination.Count(x => x == 2))
                {
                    if (guaranteed == 0)
                        guaranteed = twos;
                    // Otherwise, only shared positions are guaranteed
                    else
                        guaranteed &= twos;
                }
                // Check if no guaranteed yet but number of 3s matches
                if (num3s == combination.Count(x => x == 3))
                {
                    if (guaranteed == 0)
                        guaranteed = threes;
                    else
                        guaranteed &= threes;
                }
            }

            // Loop through cards again to update guaranteed cards
            for (int n = 0; n < 5; n++)
            {
                // Check if card is guaranteed
                if ((guaranteed & (1 << n)) != 0)
                {
                    int row = i * n + (1 - i) * j;
                    int col = (1 - i) * n + i * j;

                    // Ignore flipped cards
                    if (GameBoard[row, col] != 4) continue;

                    List<byte> values = PossibleValues[row, col];

                    // Leave only 2 and 3 as options
                    values.RemoveAll(x => x < 2);
                }
            }
        }

        /// <summary>
        /// Looks at the number of possible voltorbs in the row and if it matches
        /// the number of total voltorbs, updates possibilities accordingly
        /// </summary>
        /// <param name="i">The row of the VoltorbBoard</param>
        /// <param name="j">The index of the voltorb card within its row/column</param>
        /// <param name="voltorbs">Total number of voltorbs in the row/column</param>
        void VoltorbSweep(int i, int j, int voltorbs)
        {
            int numVs = 0;
            int guaranteedVs = 0;
            // Loop through cards to find how many voltorbs are in the row/column
            for (int n = 0; n < 5; n++)
            {
                int row = i * n + (1 - i) * j;
                int col = (1 - i) * n + i * j;

                // Ignore flipped cards
                if (GameBoard[row, col] != 4) continue;

                List<byte> values = PossibleValues[row, col];

                if (values.Contains(0))
                {
                    if (values.Count == 1) guaranteedVs++;
                    numVs++;
                }
            }
            if (numVs == voltorbs || guaranteedVs == voltorbs)
            {
                // Loop through again to remove voltorbs as possibilities
                for (int n = 0; n < 5; n++)
                {
                    int row = i * n + (1 - i) * j;
                    int col = (1 - i) * n + i * j;

                    // Ignore flipped cards
                    if (GameBoard[row, col] != 4) continue;

                    List<byte> values = PossibleValues[row, col];

                    if (numVs == voltorbs)
                    {
                        if (values.Contains(0)) values.RemoveAll(x => x != 0);
                        else values.Remove(0);
                    }
                    else if (values.Count > 1)
                        values.Remove(0);
                }
            }
        }

        /// <summary>
        /// Find all combinations of 1s, 2s, and 3s of length <paramref name="numSquares"/>
        /// that sum to <paramref name="totalPoints"/>
        /// </summary>
        /// <param name="totalPoints">The total that each individual combination
        /// should sum to</param>
        /// <param name="numSquares">How long each combination should be</param>
        /// <returns>The <see cref="List{byte}"/> of all possible combinations</returns>
        List<List<byte>> GetAllCombinations(int totalPoints, int numSquares)
        {
            List<List<byte>> combinations = new();

            FindNumbers(combinations, new List<byte>(), totalPoints, numSquares, numSquares);

            return combinations;
        }
        /// <summary>
        /// Find all combinations of 1s, 2s, and 3s of length <paramref name="numTerms"/>
        /// that sum to <paramref name="total"/>
        /// </summary>
        /// <param name="combinations">The list that contains all found combinations</param>
        /// <param name="temp">A temporary list that contains each individual combination</param>
        /// <param name="total">The total that all the numbers in a combination
        /// should sum to</param>
        /// <param name="termsLeft">How many terms are left in the sequence to find</param>
        /// <param name="numTerms">How many terms are there in the full sequence</param>
        void FindNumbers(List<List<byte>> combinations, List<byte> temp, int total, int termsLeft, int numTerms)
        {
            if (total == 0)
            {
                // Has to be numTerms length
                if (termsLeft > 0) return;

                temp.Sort();
                // Look for a copy of temp in combinations
                foreach (List<byte> combo in combinations)
                {
                    if (combo.SequenceEqual(temp)) return;
                }
                combinations.Add(new List<byte>(temp));
                return;
            }

            for (byte i = 1; i <= 3; i++)
            {
                // Too big
                if (total - i < 0) continue;
                // Too small
                if (total - i - 3 * (termsLeft - 1) > 0) continue;

                temp.Add(i);
                FindNumbers(combinations, temp, total - i, termsLeft - 1, numTerms);
                // Backtrack to consider other possibilities
                temp.Remove(i);
            }
        }

        /// <summary>
        /// Update the points, voltorbs, and number of squares at the specific
        /// position based on known values from GameBoard
        /// </summary>
        /// <param name="i">The row of the VoltorbBoard</param>
        /// <param name="j">The index of the voltorb card within its row/column</param>
        /// <param name="vals">The point, voltorb, and square number values
        /// at the provided position</param>
        /// <returns></returns>
        Triple UpdateKnownValues(int i, int j, Triple vals)
        {
            for (int n = 0; n < 5; n++)
            {
                int row = i * n + (1 - i) * j;
                int col = (1 - i) * n + i * j;

                int val = GameBoard[row, col];
                if (val != 4)
                {
                    // We know this value, so remove these points from the total
                    vals.Points -= val;
                    vals.Squares--;
                }
            }
            return vals;
        }

        /// <summary>
        /// Test a certain rule at a certain position
        /// </summary>
        /// <param name="i">The row of the VoltorbBoard</param>
        /// <param name="j">The index of the voltorb card within its row/column</param>
        /// <param name="vals">The point, voltorb, and square number values
        /// at the provided position</param>
        /// <param name="Func">What action to perform on each card in the row/column</param>
        void RuleTest(int i, int j, Triple vals, RuleAction Func)
        {
            for (int n = 0; n < 5; n++)
            {
                int row = i * n + (1 - i) * j;
                int col = (1 - i) * n + i * j;
                // If we already know this position, just skip
                if (GameBoard[row, col] != 4) continue;
                // Activate RuleAction on each card in the row/column
                Func?.Invoke(row, col, vals);
            }
        }

        delegate void RuleAction(int row, int col, Triple vals);
        /// <summary>
        /// Checks the provided row/column for if it has 0 voltorbs and removes 
        /// possibilities accordingly
        /// </summary>
        void Voltorb0(int row, int col, Triple vals)
        {
            // Remove Voltorb as a possibility
            PossibleValues[row, col].Remove(0);
        }
        /// <summary>
        /// Checks the provided row/column for if it has 5 voltorbs and removes
        /// possibilities accordingly
        /// </summary>
        void Voltorb5(int row, int col, Triple vals)
        {
            // Remove all except 0
            PossibleValues[row, col].RemoveAll(x => x > 0);
        }
        /// <summary>
        /// If there are 4 voltorbs, the total points in the row/col will be in 1 card,
        /// so the only possibilities are the total points or Voltorb
        /// </summary>
        void Voltorb4(int row, int col, Triple vals)
        {
            // Remove all except total point values
            int totalPoints = vals.Points;
            PossibleValues[row, col].RemoveAll(x => x != 0 && x != totalPoints);
        }
        /// <summary>
        /// If the total of points and voltorbs is 5, only 1 and Voltorb are possibilities
        /// </summary>
        void Total5(int row, int col, Triple vals)
        {
            // Remove 2 and 3
            PossibleValues[row, col].RemoveAll(x => x > 1);
        }
        /// <summary>
        /// If the total of points and voltorbs is 6, there can't be a 3 anywhere
        /// </summary>
        void Total6(int row, int col, Triple vals)
        {
            // Remove 3s
            PossibleValues[row, col].Remove(3);
        }
        /// <summary>
        /// If the total number of points is too high for there to be 1s anywhere,
        /// remove 1s as a possibility
        /// </summary>
        void No1s(int row, int col, Triple vals)
        {
            // Remove 1s
            PossibleValues[row, col].Remove(1);
        }
    }
}
