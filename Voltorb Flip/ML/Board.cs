using Microsoft.ML.Data;
using System;
using System.Linq;

namespace Voltorb_Flip.ML
{
    public class Board : IEquatable<Board>
    {
        // Which level of Voltorb Flip we are at
        [LoadColumn(0)]
        public int Level {  get; set; }

        // How many voltorbs and points are in each row and column
        [LoadColumn(1, 20)]
        [VectorType(2, 5, 2)]
        public int[] VoltorbNumbers { get; set; }

        // Stores point values/voltorbs { 0: 1, 1: Voltorb, 2: 2, 3: 3, -1: unknown }
        [LoadColumn(21, 45)]
        [VectorType(5, 5)]
        public int[] KnownBoardState { get; set; }

        // Stores full board
        [LoadColumn(46, 70)]
        [VectorType(5, 5)]
        [ColumnName("Label")]
        public int[] FullBoardState { get; set; }


        public bool Equals(Board other)
        {
            if (Level != other.Level) return false;
            if (!VoltorbNumbers.SequenceEqual(other.VoltorbNumbers)) return false;
            if (!KnownBoardState.SequenceEqual(other.KnownBoardState)) return false;
            if (!FullBoardState.SequenceEqual(other.FullBoardState)) return false;

            return true;
        }
    }
}
