using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

namespace Voltorb_Flip.ML
{
    public class Model
    {
        // Model
        readonly MLContext _mlContext;
        RegressionPredictionTransformer<LinearRegressionModelParameters> _trainedModel;
        ITransformer _transformer;

        // Data
        IDataView _trainData;
        IDataView _transformedTrainingData;
        IDataView _testData;
        IDataView _transformedTestData;

        // Singleton
        static Model _instance;
        public static Model Instance { get
            {
                _instance ??= new();
                return _instance;
            } }

        public Model()
        {
            // Initialize machine learning context
            _mlContext = new();
        }

        public void Train()
        {
            // Load training data
            IDataView data = _mlContext.Data.LoadFromTextFile<Board>("training_data.csv", hasHeader: true, allowSparse: true);
            DataOperationsCatalog.TrainTestData dataSplit = _mlContext.Data.TrainTestSplit(data);
            _trainData = dataSplit.TrainSet;
            _testData = dataSplit.TestSet;

            // Concatenate feature columns
            // Convert int vectors to float
            // Normalize feature columns
            // Cache prepared data
            IEstimator<ITransformer> dataPrepEstimator = _mlContext.Transforms
                .Concatenate("Features", "Level", "VoltorbNumbers", "KnownBoardState")
                .Append(_mlContext.Transforms.Conversion.ConvertType("Features"))
                .Append(_mlContext.Transforms.Conversion.ConvertType("Label"))
                .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                .AppendCacheCheckpoint(_mlContext);
            _transformer = dataPrepEstimator.Fit(_trainData);
            _transformedTrainingData = _transformer.Transform(_trainData);
            _transformedTestData = _transformer.Transform(_testData);

            // Build trainer
            SdcaRegressionTrainer sdcaTrainer = _mlContext.Regression.Trainers.Sdca();

            // Train model
            _trainedModel = sdcaTrainer.Fit(_transformedTrainingData);
        }

        public double Evaluate()
        {
            if (_trainedModel == null) return 0;

            // Predict results of test data
            IDataView testDataPredictions = _trainedModel.Transform(_transformedTestData);

            RegressionMetrics metrics = _mlContext.Regression.Evaluate(testDataPredictions);
            return metrics.RSquared;
        }

        public BoardPrediction Predict(Board input)
        {
            // Generate prediction engine
            PredictionEngine<Board, BoardPrediction> predictionEngine = _mlContext?.Model
                .CreatePredictionEngine<Board, BoardPrediction>(_transformer);
            return predictionEngine?.Predict(input);
        }
    }
}
