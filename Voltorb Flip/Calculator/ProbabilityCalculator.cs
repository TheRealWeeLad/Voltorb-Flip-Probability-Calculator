using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Voltorb_Flip.Calculator
{
    partial class ProbabilityCalculator
    {
        public byte[,] GameBoard { get; private set; } = new byte[5, 5];
        byte[,] InternalGameBoard { get; set; } = new byte[5, 5];
        // X-value of Point represents point values per column, y-value is voltorb numbers
        Triple[,] VoltorbBoard { get; } = new Triple[2, 5]; // Row 1 is Vertical, 2 is Horizontal
        Triple[,] InternalVoltorbBoard { get; set; } = new Triple[2, 5];
        public List<byte>[,] PossibleValues { get; set; } = new List<byte>[5, 5];
        List<byte>[,] LastPossibleValues { get; set; } = new List<byte>[5, 5];
        public float[,] Probabilities { get; } = new float[5, 5];
        // (2, 3, V) that have already been found
        (int, int, int) currentFoundValues = new();
        List<(int, int, int)> currentPossibleBoards = new();

        readonly byte[] allPossible = { 0, 1, 2, 3 };

        record struct Triple(int Points, int Voltorbs, int Squares);

        public static int CurrentLevel { get; set; } = 1;

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
                voltorbBitmaps[i] = System.Drawing.Image.FromFile(string.Format(@"D:\Other Stuff\Voltorb Flip\Voltorb Flip\Assets\voltorb{0}.png",
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
            InternalGameBoard = GameBoard.Clone() as byte[,];
            currentFoundValues = new();
            currentPossibleBoards = new(PossibleBoards[CurrentLevel - 1]);
        }

        /// <summary>
        /// Fill in known quantities from game board
        /// </summary>
        void FillInKnownValues()
        {
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 5; j++)
                {
                    byte val = InternalGameBoard[i, j];
                    if (val == 4)
                        PossibleValues[i, j].AddRange(allPossible);
                    else PossibleValues[i, j].Add(val);
                }
        }

        /// <summary>
        /// Go through PossibleValues and VoltorbBoard to eliminate possibilities
        /// </summary>
        void CalculateUnknowns()
        {
            // Loop through VoltorbBoard list to find row/column information
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    InternalVoltorbBoard[i, j] = UpdateKnownValues(i, j, VoltorbBoard[i, j]);
                    Triple vals = InternalVoltorbBoard[i, j];
                    int points = vals.Points;
                    int voltorbs = vals.Voltorbs;
                    int numSquares = vals.Squares;
                    int freeSquares = vals.Squares - voltorbs;

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

                    // Perform final eliminations
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

                    // Combination Analysis
                    List<List<byte>> allCombinations = GetAllCombinations(points, freeSquares);
                    // Eliminate impossible combinations
                    foreach(List<byte> combo in new List<List<byte>>(allCombinations))
                        if (!IsCombinationPossible(combo)) allCombinations.Remove(combo);
                    // Analyze differences between combinations to get more information
                    if (allCombinations.Count > 1)
                        AnalyzeCombinations(allCombinations, i, j, points, freeSquares);

                    // Perform final voltorb sweep
                    VoltorbSweep(i, j, voltorbs);
                }
            }

            // Analyze possible board states to check for guaranteed points
            AnalyzeBoards();

            // Recheck with updated GameBoard and PossibilityBoard
            if (UpdateInformation()) CalculateUnknowns();
        }

        /// <summary>
        /// Updates InternalGameBoard and currentPossibleBoards based on calculated
        /// information
        /// </summary>
        /// <returns>True if information was updated, False otherwise</returns>
        bool UpdateInformation()
        {
            // Update game board based on guaranteed values
            // Update possibility board based on new information
            bool updated = false;
            int poss2s = 0;
            int poss3s = 0;
            int possVs = 0;
            currentFoundValues = new();
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    List<byte> vals = PossibleValues[r, c];
                    if (vals.Contains(2)) poss2s++;
                    if (vals.Contains(3)) poss3s++;
                    if (vals.Contains(0)) possVs++;

                    if (vals != LastPossibleValues[r, c])
                        updated = true;

                    byte val = InternalGameBoard[r, c];
                    if (val == 4 && vals.Count == 1)
                    {
                        updated = true;
                        InternalGameBoard[r, c] = vals[0];
                    }
                    if (val == 2) currentFoundValues.Item1++;
                    else if (val == 3) currentFoundValues.Item2++;
                    else if (val == 0) currentFoundValues.Item3++;
                }
            }
            // Store this recursion's possible values
            LastPossibleValues = PossibleValues.Clone() as List<byte>[,];
            // Update possible boards this level
            foreach ((int, int, int) board in new List<(int, int, int)>(currentPossibleBoards))
            {
                // If we've found more 2s, 3s, or Vs than is possible in this board
                // OR there aren't enough 2s, 3s, or Vs for the board to be possible
                if (board.Item1 < currentFoundValues.Item1 ||
                    board.Item2 < currentFoundValues.Item2 ||
                    board.Item3 < currentFoundValues.Item3 ||
                    poss2s < board.Item1 ||
                    poss3s < board.Item2 ||
                    possVs < board.Item3)
                {
                    updated = true;
                    // Remove this possibility
                    currentPossibleBoards.Remove(board);
                }
            }

            return updated;
        }

        // BROKEN / DOES NOT WORK
        /// <summary>
        /// Calculate the probability that each square in the board is safe by crosschecking all possible boards
        /// </summary>
        void CalculateProbabilities()
        {
            // Look through PossibleValues to find all possible 2s, 3s, and Vs
            int num2s = 0;
            int num3s = 0;
            int numVs = 0;
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    // Ignore known squares
                    if (InternalGameBoard[r, c] != 4) continue;

                    List<byte> values = PossibleValues[r, c];
                    if (values.Contains(2)) num2s++;
                    if (values.Contains(3)) num3s++;
                    if (values.Contains(0)) numVs++;
                }
            }

            // Loop through it again to determine probabilities
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    List<byte> values = PossibleValues[r, c];

                    // Ignore Flipped Squares
                    if (GameBoard[r, c] != 4) continue;

                    // If no voltorb, 100% safe
                    if (!values.Contains(0))
                    {
                        Probabilities[r, c] = 1;
                        continue;
                    }
                    // If only voltorb, 0% safe
                    if (values.Count == 0 && values[0] == 0)
                    {
                        Probabilities[r, c] = 0;
                        continue;
                    }

                    float prob2 = 0;
                    float prob3 = 0;
                    float probV = 1;
                    float totalProb2 = 0;
                    float totalProb3 = 0;
                    float totalProbV = 0;

                    if (values.Contains(2)) prob2 = 1;
                    if (values.Contains(3)) prob3 = 1;

                    // Look at all possible boards to determine probabilities
                    int numBoards = currentPossibleBoards.Count;
                    foreach ((int, int, int) board in currentPossibleBoards)
                    {
                        // Probability = # Combinations with 2 in this position / # Combinations
                        // Numerator = (num2s - 1) CHOOSE (num2sInBoard - 1)
                        // Denominator = (num2s) CHOOSE (num2sInBoard)
                        // Simplifies to num2sInBoard / num2s
                        if (num2s != 0)
                            totalProb2 += prob2 * board.Item1 / num2s / numBoards;
                        if (num3s != 0)
                            totalProb3 += prob3 * board.Item2 / num3s / numBoards;
                        if (numVs != 0)
                            totalProbV += probV * board.Item3 / numVs / numBoards;
                    }

                    if (prob2 == 0 && prob3 == 0)
                        Probabilities[r, c] =  1 - totalProbV;
                    else Probabilities[r, c] = totalProb2 + totalProb3;
                }
            }
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
                if (InternalGameBoard[row, col] != 4) continue;

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

                if (InternalGameBoard[row, col] != 4) continue;

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

        /// <summary>
        /// Analyze the possible combinations of point values that sum to the needed
        /// total value to figure out which squares MUST be points in order to achieve
        /// the desired sum
        /// </summary>
        /// <param name="allCombinations"><see cref="List{T}"/> of all possible 
        /// combinations of point values</param>
        /// <param name="i">The row of the VoltorbBoard</param>
        /// <param name="j">The index of the voltorb card within its row/column</param>
        /// <param name="totalPoints">Total number of points we need to achieve</param>
        /// <param name="freeSquares">How many squares are left for points to be in 
        /// (number of squares - number of voltorbs)</param>
        void AnalyzeCombinations(List<List<byte>> allCombinations, int i, int j, int totalPoints, int freeSquares)
        {
            // Each bit represents position in row/column
            List<List<byte>> possibleVals = new();
            // Look through cards to find all occurrences of 2 and 3
            int num2s = 0;
            int num3s = 0;
            for (int n = 0; n < 5; n++)
            {
                int row = i * n + (1 - i) * j;
                int col = (1 - i) * n + i * j;
                
                // Ignore known cards
                if (InternalGameBoard[row, col] != 4)
                {
                    // Add dummy value to make possibleVals the correct length
                    possibleVals.Add(new() { 182 });
                    continue;
                }

                List<byte> value = PossibleValues[row, col];
                if (value.Contains(2)) num2s++;
                if (value.Contains(3)) num3s++;

                List<byte> valueCopy = new(value);
                // Ignore voltorbs in combination matching
                valueCopy.Remove(0);
                possibleVals.Add(valueCopy);
            }

            // Use number of twos and threes to eliminate possible combinations
            foreach (List<byte> combo in new List<List<byte>>(allCombinations))
            {
                // If there are too few 2s or 3s for the combination, eliminate it
                if (num2s < combo.Count(x => x == 2) ||
                    num3s < combo.Count(x => x == 3))
                    allCombinations.Remove(combo);
            }

            // Loop again to eliminate possible combinations
            for (int n = 0; n < 5; n++)
            {
                int row = i * n + (1 - i) * j;
                int col = (1 - i) * n + i * j;

                // Ignore known cards
                if (InternalGameBoard[row, col] != 4) continue;

                List<byte> value = PossibleValues[row, col];

                // Check if values in this position are possible based on allCombinations
                foreach (byte val in new List<byte>(value))
                {
                    if (val == 0) continue;
                    // Increment number of 2s and 3s
                    if (val == 2) num2s++;
                    else if (val == 3) num3s++;

                    // If value is not present in any combination, it is not possible
                    if (allCombinations.All(combo => !combo.Contains(val)))
                        value.Remove(val);
                }
            }

            // Check Combinations for guaranteed values
            List<Dictionary<byte, byte>> matchingPositions = new();
            // Check each combination for the matching combinations of possible values
            for (int k = 0; k < allCombinations.Count; k++)
                matchingPositions.AddRange(GetAllCombinations(possibleVals, allCombinations[k]));
            
            // Make sure there are some matching positions
            if (matchingPositions.Count == 0) return;

            // Only positions in every single combination are guaranteed to 
            // be a number
            Dictionary<byte, List<byte>> guaranteedPositions = GetCommonPositions(matchingPositions);
            
            // Loop through cards again to update guaranteed cards
            foreach (byte position in guaranteedPositions.Keys)
            {
                int row = i * position + (1 - i) * j;
                int col = (1 - i) * position + i * j;

                // Ignore known cards
                if (InternalGameBoard[row, col] != 4) continue;

                List<byte> guaranteedValues = guaranteedPositions[position];

                // Leave only guaranteed value as an option
                PossibleValues[row, col].RemoveAll(x => !guaranteedValues.Contains(x));
            }
        }

        /// <summary>
        /// Analyze all remaining possible boards to determine whether the remaining
        /// possible 2s and 3s must be correct
        /// </summary>
        void AnalyzeBoards()
        {
            // Loop through entire board to find total number of 2s and 3s
            int totalNum2s = 0;
            int totalNum3s = 0;
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    // Ignore known tiles
                    if (InternalGameBoard[r, c] != 4) continue;

                    List<byte> values = PossibleValues[r, c];
                    if (values.Contains(2)) totalNum2s++;
                    if (values.Contains(3)) totalNum3s++;
                }
            }

            // Check these against possible boards
            bool twosGuaranteed = true;
            bool threesGuaranteed = true;
            foreach ((int, int, int) board in currentPossibleBoards)
            {
                // If there are fewer or equal possible twos on the board than
                // there are in all possible boards, all possible twos must be twos
                if (totalNum2s > board.Item1 - currentFoundValues.Item1)
                    twosGuaranteed = false;
                if (totalNum3s > board.Item2 - currentFoundValues.Item2)
                    threesGuaranteed = false;
            }

            if (!twosGuaranteed && !threesGuaranteed) return; // Nothing to do

            // Loop through entire board again to flip over guaranteed twos/threes
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    // Ignore known tiles
                    if (InternalGameBoard[r, c] != 4) continue;

                    List<byte> values = PossibleValues[r, c];
                    if (twosGuaranteed && values.Contains(2))
                        values.RemoveAll(x => x != 2);
                    else if (threesGuaranteed && values.Contains(3))
                        values.RemoveAll(x => x != 3);
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

                // Ignore known cards
                if (InternalGameBoard[row, col] != 4) continue;

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
                    if (InternalGameBoard[row, col] != 4) continue;

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
        /// Finds all positions that exist in every <see cref="Dictionary{TKey, TValue}{T}"/>
        /// within a <see cref="List{T}"/>
        /// </summary>
        /// <typeparam name="T">The type of element within the <paramref name="values"/> list</typeparam>
        /// <param name="values">The values to check</param>
        /// <returns>A <see cref="Dictionary{TKey, TValue}"/> containing the
        /// common keys and values</returns>
        Dictionary<T, List<T>> GetCommonPositions<T>(List<Dictionary<T, T>> values)
        {
            Dictionary<T, List<T>> result = new();

            for (int i = 0; i < values.Count; i++)
            {
                if (i == 0)
                {
                    // Add values to result list
                    Dictionary<T, T> values1 = values[0];
                    foreach (T position in values1.Keys)
                    {
                        T value = values1[position];
                        result.Add(position, new() { value });
                    }
                    continue;
                }

                // Check each position-value pair in valueList for matching positions
                Dictionary<T, T> valueList = values[i];

                List<T> keysToRemove = new();
                foreach ((T key, List<T> resultValues) in new Dictionary<T, List<T>>(result))
                {
                    // Remove positions that aren't shared
                    if (!valueList.ContainsKey(key)) result.Remove(key);

                    // Combine values in matching positions
                    foreach ((T position, T value) in valueList)
                    {
                        if (key.Equals(position) && !resultValues.Contains(value))
                        {
                            result[key].Add(value);
                        }
                    }
                }

                foreach (T key in keysToRemove)
                    result.Remove(key);
            }

            return result;
        }
        /// <summary>
        /// Find all combinations of the possible values provided in
        /// <paramref name="values"/> that coincide with <paramref name="match"/>
        /// </summary>
        /// <param name="values">A <see cref="List{byte}"/> of all possible values
        /// for each position in a row/column</param>
        /// <param name="match">The <see cref="List{byte}"/> to compare against</param>
        /// <returns>A <see cref="List{List{byte}}"/> of all combinations of positions
        /// that create <paramref name="match"/></returns>
        List<Dictionary<byte, byte>> GetAllCombinations(List<List<byte>> values, List<byte> match)
        {
            List<Dictionary<byte, byte>> combinations = new();

            FindCombinations(values, combinations, new(), match, 0);

            return combinations;
        }
        /// <summary>
        /// Find all combinations of the possible values provided in
        /// <paramref name="values"/> that coincide with <paramref name="match"/>
        /// </summary>
        /// <param name="values">The possible values for each card</param>
        /// <param name="combinations">The list to add found combinations to</param>
        /// <param name="temp">Temporary list that stores each combination</param>
        /// <param name="match">The sequence to find a match for</param>
        /// <param name="position">The position of the card in the sequence</param>
        void FindCombinations(List<List<byte>> values, List<Dictionary<byte, byte>> combinations, Dictionary<byte, byte> temp, List<byte> match, byte position)
        {
            if (position == match.Count)
            {
                // Look for a copy of temp in combinations
                foreach (Dictionary<byte, byte> combo in combinations)
                {
                    if (combo.Keys.SequenceEqual(temp.Keys)) return;
                }
                combinations.Add(new Dictionary<byte, byte>(temp));
                return;
            }

            for (byte i = 0; i < values.Count; i++)
            {
                // No repeat values
                if (temp.ContainsKey(i)) continue;

                List<byte> valueList = values[i];
                foreach (byte value in valueList)
                {
                    if (value == match[position])
                    {
                        temp.Add(i, value);
                        FindCombinations(values, combinations, new Dictionary<byte, byte>(temp), match, (byte)(position + 1));
                        temp.Remove(i);
                    }
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
        /// Checks a certain combination of 1s, 2s, and 3s to determine whether
        /// there are enough 2s and 3s left in the current level for the combination
        /// to be possible
        /// </summary>
        /// <param name="combination">The combination to check</param>
        /// <returns>True if it is possible, False if not</returns>
        bool IsCombinationPossible(List<byte> combination)
        {
            int num2s = combination.Count(x => x == 2);
            int num3s = combination.Count(x => x == 3);
            bool twoSafe = false;
            bool threeSafe = false;

            foreach ((int, int, int) values in currentPossibleBoards)
            {
                // Combination must have too many 2s or 3s for all possible boards
                int twosLeft = values.Item1 - currentFoundValues.Item1;
                if (num2s <= twosLeft) twoSafe = true;
                int threesLeft = values.Item2 - currentFoundValues.Item2;
                if (num3s <= threesLeft) threeSafe = true;
            }

            return twoSafe && threeSafe;
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
            Triple result = new(vals.Points, vals.Voltorbs, vals.Squares);
            for (int n = 0; n < 5; n++)
            {
                int row = i * n + (1 - i) * j;
                int col = (1 - i) * n + i * j;

                int val = InternalGameBoard[row, col];
                if (val != 4)
                {
                    // We know this value, so remove these points from the total
                    result.Points -= val;
                    if (val == 0) result.Voltorbs--;
                    result.Squares--;
                }
            }
            return result;
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
                if (InternalGameBoard[row, col] != 4) continue;
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
