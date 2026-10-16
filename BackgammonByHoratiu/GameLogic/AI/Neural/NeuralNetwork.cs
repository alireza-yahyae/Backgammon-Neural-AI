using System;
using System.IO;
using System.Text;

namespace BackgammonByHoratiu.GameLogic.AI.Neural
{
    /// <summary>
    /// Forward-only value network: ReLU hidden layers and five sigmoid outputs (a port of model.py's Predictor).
    ///
    /// Outputs, all from the point of view of the side to move:
    /// 0 = P(win), 1 = P(win gammon or backgammon), 2 = P(win backgammon),
    /// 3 = P(lose gammon or backgammon), 4 = P(lose backgammon).
    ///
    /// File format (little endian), written by tools/export_weights.py:
    /// "BGNN", int32 format version (1), int32 feature version, int32 layer count, then per layer
    /// int32 inputs, int32 outputs, float32 weights [inputs x outputs, row-major], float32 biases [outputs].
    /// </summary>
    internal sealed class NeuralNetwork
    {
        const int FileFormatVersion = 1;
        const int MaxLayerSize = 100_000;
        const int OutputCount = 5;

        readonly int[] inputSizes;
        readonly int[] outputSizes;
        readonly float[][] weights;
        readonly float[][] biases;

        NeuralNetwork(int[] inputSizes, int[] outputSizes, float[][] weights, float[][] biases)
        {
            this.inputSizes = inputSizes;
            this.outputSizes = outputSizes;
            this.weights = weights;
            this.biases = biases;
        }

        internal static NeuralNetwork Load(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using BinaryReader reader = new(stream, Encoding.ASCII);

            string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));

            if (magic != "BGNN")
            {
                throw new InvalidDataException($"{path}: not a network file (bad header).");
            }

            int formatVersion = reader.ReadInt32();
            int featureVersion = reader.ReadInt32();
            int layerCount = reader.ReadInt32();

            if (formatVersion != FileFormatVersion)
            {
                throw new InvalidDataException($"{path}: unsupported file format version {formatVersion}.");
            }

            if (featureVersion != FeatureEncoder.FeatureVersion)
            {
                throw new InvalidDataException(
                    $"{path}: weights were made for feature version {featureVersion}, expected {FeatureEncoder.FeatureVersion}.");
            }

            if (layerCount < 2 || layerCount > 16)
            {
                throw new InvalidDataException($"{path}: unexpected layer count {layerCount}.");
            }

            int[] inputSizes = new int[layerCount];
            int[] outputSizes = new int[layerCount];
            float[][] weights = new float[layerCount][];
            float[][] biases = new float[layerCount][];

            for (int layer = 0; layer < layerCount; layer++)
            {
                int inputs = reader.ReadInt32();
                int outputs = reader.ReadInt32();

                if (inputs < 1 || outputs < 1 || inputs > MaxLayerSize || outputs > MaxLayerSize)
                {
                    throw new InvalidDataException($"{path}: bad size of layer {layer}.");
                }

                if (layer > 0 && inputs != outputSizes[layer - 1])
                {
                    throw new InvalidDataException($"{path}: layer {layer} does not match the previous layer.");
                }

                inputSizes[layer] = inputs;
                outputSizes[layer] = outputs;
                weights[layer] = ReadFloats(reader, inputs * outputs);
                biases[layer] = ReadFloats(reader, outputs);
            }

            if (inputSizes[0] != FeatureEncoder.FeatureCount || outputSizes[layerCount - 1] != OutputCount)
            {
                throw new InvalidDataException(
                    $"{path}: expected {FeatureEncoder.FeatureCount} inputs and {OutputCount} outputs.");
            }

            return new NeuralNetwork(inputSizes, outputSizes, weights, biases);
        }

        static float[] ReadFloats(BinaryReader reader, int count)
        {
            float[] values = new float[count];

            for (int i = 0; i < count; i++)
            {
                values[i] = reader.ReadSingle();
            }

            return values;
        }

        internal float[] Value(float[] features)
        {
            float[] hidden = features;
            int lastLayer = weights.Length - 1;

            for (int layer = 0; layer <= lastLayer; layer++)
            {
                int inputs = inputSizes[layer];
                int outputs = outputSizes[layer];
                float[] layerWeights = weights[layer];
                float[] sums = new float[outputs];

                Array.Copy(biases[layer], sums, outputs);

                for (int i = 0; i < inputs; i++)
                {
                    float activation = hidden[i];

                    if (activation == 0f)
                    {
                        continue;
                    }

                    int row = i * outputs;

                    for (int j = 0; j < outputs; j++)
                    {
                        sums[j] += activation * layerWeights[row + j];
                    }
                }

                for (int j = 0; j < outputs; j++)
                {
                    sums[j] = layer < lastLayer
                        ? Math.Max(sums[j], 0f)
                        : 0.5f * (1f + MathF.Tanh(0.5f * sums[j]));
                }

                hidden = sums;
            }

            return hidden;
        }

        // Cubeless money equity (expected points) for the side to move.
        internal static float Equity(float[] outputs)
        {
            return (2f * outputs[0] - 1f) + (outputs[1] - outputs[3]) + (outputs[2] - outputs[4]);
        }
    }
}
