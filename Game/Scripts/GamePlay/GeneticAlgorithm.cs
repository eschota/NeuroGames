using UnityEngine;
using System.Collections.Generic;
using System;

namespace Game.Scripts.GamePlay
{
    [System.Serializable]
    public class GeneticAlgorithm
    {
        public int[] neural_layers = new int[] { 10, 16, 8 };
        public int population_size = 50;
        public float mutation_rate = 0.1f;
        
        // Создает случайную нейросеть по заданным слоям
        public NeuralNetwork CreateRandomNetwork()
        {
            if (neural_layers == null || neural_layers.Length < 2)
            {
                Debug.LogError("❌ Неверная конфигурация слоёв в GeneticAlgorithm!");
                return null;
            }

            try
            {
                NeuralNetwork network = new NeuralNetwork(neural_layers);
                network.Randomize(); // Вызываем метод Randomize из NeuralNetwork
                
                // КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: Форсируем ненулевые начальные веса и смещения
                // Это позволит сети сразу начать шевелиться, а не стоять столбом
                ForceNonZeroInitialization(network);
                
                return network;
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Ошибка создания сети: {e.Message}");
                throw;
            }
        }
        
        // Добавим метод принудительной инициализации ненулевых значений
        private void ForceNonZeroInitialization(NeuralNetwork network)
        {
            if (network == null || network.weights == null || network.biases == null) return;
            
            // Устанавливаем ненулевые смещения для выходного слоя
            int outputLayerIndex = network.biases.Length - 1;
            
            // Для каждого выходного нейрона устанавливаем небольшое ненулевое смещение
            for (int i = 0; i < network.biases[outputLayerIndex].Length; i++)
            {
                // Добавляем небольшое случайное смещение, достаточное для преодоления порога
                network.biases[outputLayerIndex][i] = UnityEngine.Random.Range(0.2f, 0.5f) * 
                    (UnityEngine.Random.value > 0.5f ? 1 : -1);
            }
            
            // Также слегка усиливаем веса в последнем слое для обеспечения движения
            for (int i = 0; i < network.weights[outputLayerIndex].Length; i++)
            {
                for (int j = 0; j < network.weights[outputLayerIndex][i].Length; j++)
                {
                    // Усиливаем веса, множим существующие на коэффициент
                    network.weights[outputLayerIndex][i][j] *= UnityEngine.Random.Range(1.0f, 1.5f);
                }
            }
            
            Debug.Log("🔥 Принудительная инициализация: веса и смещения усилены для движения с нуля!");
        }
        
        // Выполняет кроссовер двух сетей
        public NeuralNetwork Crossover(NeuralNetwork parent1, NeuralNetwork parent2)
        {
            if (parent1 == null || parent2 == null)
                return null;
                
            return NeuralNetwork.Crossover(parent1, parent2);
        }
        
        // Применяет мутацию к сети
        public void Mutate(NeuralNetwork network, float mutationRate, float mutationStrength = 0.5f)
        {
            if (network == null)
                return;
                
            network.Mutate(mutationRate, mutationStrength);
        }
    }
} 