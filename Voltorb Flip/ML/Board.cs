using Microsoft.ML.Data;

namespace Voltorb_Flip.ML
{
    public class Board
    {
        // Which level of Voltorb Flip we are at
        [LoadColumn(0)]
        public int Level {  get; set; }

        // How many voltorbs and points are in each row and column
        [LoadColumn(1, 20)]
        [VectorType(2, 5, 2)]
        public int[] VoltorbNumbers { get; set; }

        // Stores known point values/voltorbs { 0: 1, 1: Voltorb, 2: 2, 3: 3 }
        [LoadColumn(21, 45)]
        [VectorType(5, 5)]
        public int[] BoardState { get; set; }
    }
}
