using Microsoft.ML.Data;

namespace Voltorb_Flip.ML
{
    public class BoardPrediction
    {
        [ColumnName("Score")]
        public int[] PredictedBoard { get; set; }
    }
}
