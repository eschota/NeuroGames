using UnityEngine;
using System.Collections.Generic;
using System;
using System.Text;

namespace Game.Scripts.GamePlay
{
    [System.Serializable]
    public class NeuralNetwork
    {
        // Сделаем поля публичными для доступа из NeuroHuman
        public int[] layers;
        public float[][][] weights;
        public float[][] biases;
        public float fitness = 0;
        
        // Добавим параметры для более гибкой настройки
        public float leakyReLUFactor = 0.1f; // Увеличиваем коэффициент до 0.1 для лучшей динамики
        private bool useGELU = true; // Используем GELU для более плавных движений

        // Новый конструктор с проверками
        public NeuralNetwork(int[] layers)
        {
            // Проверка на достаточное количество слоев
            if (layers == null || layers.Length < 2)
            {
                throw new ArgumentException("Недостаточно слоев для создания нейросети! Нужно минимум 2 слоя.");
            }

            // Проверка размеров слоев
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] <= 0)
                {
                    throw new ArgumentException($"Некорректный размер слоя {i}: {layers[i]} (должен быть положительным)");
                }
            }

            // Инициализация структуры сети
            this.layers = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                this.layers[i] = layers[i];
            }

            // Инициализация весов и смещений
            InitializeWeightsAndBiases();
            
            // Рандомизируем начальные значения
            Randomize();
        }

        // Инициализация весов и смещений
        private void InitializeWeightsAndBiases()
        {
            // Инициализация весов
            weights = new float[layers.Length - 1][][];
            for (int i = 0; i < layers.Length - 1; i++)
            {
                weights[i] = new float[layers[i + 1]][];
                for (int j = 0; j < layers[i + 1]; j++)
                {
                    weights[i][j] = new float[layers[i]];
                }
            }

            // Инициализация смещений
            biases = new float[layers.Length - 1][];
            for (int i = 0; i < layers.Length - 1; i++)
            {
                biases[i] = new float[layers[i + 1]];
            }
        }

        // Рандомизация весов и смещений
        public void Randomize()
        {
            // Проверяем, инициализированы ли массивы
            if (weights == null || biases == null)
            {
                Debug.LogError("Веса или смещения не инициализированы!");
                InitializeWeightsAndBiases();
            }

            // Добавим немного случайности в инициализацию для разнообразия нейросетей
            bool useWideInitialization = UnityEngine.Random.value < 0.3f; // 30% на расширенную инициализацию
            
            // Задаем случайные значения весам с улучшенной инициализацией
            for (int i = 0; i < weights.Length; i++)
            {
                for (int j = 0; j < weights[i].Length; j++)
                {
                    for (int k = 0; k < weights[i][j].Length; k++)
                    {
                        // Используем He инициализацию для ReLU активации - лучше для глубоких сетей
                        float he = Mathf.Sqrt(2f / layers[i]);
                        
                        // В 30% случаев используем более широкую инициализацию для выхода из локальных минимумов
                        if (useWideInitialization) {
                            weights[i][j][k] = UnityEngine.Random.Range(-he * 2.0f, he * 2.0f);
                        } else {
                            weights[i][j][k] = UnityEngine.Random.Range(-he, he);
                        }
                    }
                }
            }

            // Задаем случайные значения смещениям с меньшим разбросом для начала
            for (int i = 0; i < biases.Length; i++)
            {
                for (int j = 0; j < biases[i].Length; j++)
                {
                    // В 30% случаев используем более широкую инициализацию для смещений
                    if (useWideInitialization) {
                        biases[i][j] = UnityEngine.Random.Range(-1.0f, 1.0f);
                    } else {
                        biases[i][j] = UnityEngine.Random.Range(-0.5f, 0.5f); // Увеличен диапазон для лучшего старта
                    }
                }
            }
        }

        // Функция активации (сигмоида)
        private float Sigmoid(float x)
        {
            // Ограничиваем x для предотвращения переполнения
            x = Mathf.Clamp(x, -15f, 15f);
            return 1f / (1f + Mathf.Exp(-x));
        }

        // Улучшенная ReLU функция активации
        private float LeakyReLU(float x)
        {
            return x > 0 ? x : leakyReLUFactor * x; // Увеличили утечку для лучшей динамики
        }
        
        // GELU активация - лучше для управления движением
        private float GELU(float x)
        {
            // Аппроксимация GELU: x * sigmoid(1.702 * x)
            return x * Sigmoid(1.702f * x);
        }

        // Прямое распространение сигнала через сеть
        public float[] FeedForward(float[] inputs)
        {
            // Проверка входных данных
            if (inputs == null)
            {
                Debug.LogError("Входные данные нейросети равны null!");
                return new float[layers[layers.Length - 1]]; // Возвращаем пустой массив соответствующего размера
            }

            // Проверка на соответствие размеров входного слоя и входных данных
            if (inputs.Length != layers[0])
            {
                Debug.LogWarning($"Размер входных данных ({inputs.Length}) не соответствует размеру входного слоя ({layers[0]})!");
                
                // Адаптивная обработка: создаем новый массив нужного размера
                float[] adjustedInputs = new float[layers[0]];
                int copyLength = Mathf.Min(inputs.Length, layers[0]);
                
                for (int i = 0; i < copyLength; i++)
                {
                    adjustedInputs[i] = inputs[i];
                }
                
                inputs = adjustedInputs;
            }

            // Преобразуем входные данные в выходные данные первого скрытого слоя
            float[] currentLayer = inputs;
            float[] nextLayer;

            // Проходим по всем слоям, кроме входного
            for (int i = 0; i < layers.Length - 1; i++)
            {
                nextLayer = new float[layers[i + 1]];

                // Для каждого нейрона в следующем слое
                for (int j = 0; j < layers[i + 1]; j++)
                {
                    // Сумма с учетом весов и смещения
                    float sum = biases[i][j];

                    // Добавляем взвешенные входы
                    for (int k = 0; k < layers[i]; k++)
                    {
                        sum += currentLayer[k] * weights[i][j][k];
                    }

                    // Применяем функцию активации и сохраняем результат
                    if (i == layers.Length - 2) // Выходной слой - используем модифицированную линейную функцию
                    {
                        // Линейная функция с жестким ограничением для лучшего контроля
                        nextLayer[j] = Mathf.Clamp(sum, -1f, 1f);
                    }
                    else // Скрытые слои
                    {
                        if (useGELU) 
                            nextLayer[j] = GELU(sum);
                        else
                            nextLayer[j] = LeakyReLU(sum);
                    }
                }

                // Теперь текущий слой - это рассчитанный следующий слой
                currentLayer = nextLayer;
            }

            // Возвращаем выходной слой
            return currentLayer;
        }

        // Улучшенная мутация весов и смещений сети
        public void Mutate(float mutationRate, float mutationStrength)
        {
            // Проверяем, инициализированы ли массивы
            if (weights == null || biases == null)
            {
                Debug.LogError("Веса или смещения не инициализированы!");
                InitializeWeightsAndBiases();
                Randomize();
                return;
            }

            // Определяем, делать ли крупную мутацию с маленькой вероятностью
            bool majorMutation = UnityEngine.Random.value < 0.05f;
            float actualMutationStrength = majorMutation ? mutationStrength * 5f : mutationStrength;
            
            // Мутируем веса с адаптивной силой
            for (int i = 0; i < weights.Length; i++)
            {
                for (int j = 0; j < weights[i].Length; j++)
                {
                    for (int k = 0; k < weights[i][j].Length; k++)
                    {
                        // С определенной вероятностью изменяем вес
                        float chance = majorMutation ? mutationRate * 2f : mutationRate;
                        if (UnityEngine.Random.value < chance)
                        {
                            // Для более эффективного обучения используем нормальное распределение
                            float noise = (float)NextGaussian() * actualMutationStrength;
                            weights[i][j][k] += noise;
                            
                            // Ограничиваем максимальное значение веса для стабильности
                            weights[i][j][k] = Mathf.Clamp(weights[i][j][k], -5f, 5f);
                        }
                    }
                }
            }

            // Мутируем смещения
            for (int i = 0; i < biases.Length; i++)
            {
                for (int j = 0; j < biases[i].Length; j++)
                {
                    // С определенной вероятностью изменяем смещение
                    float chance = majorMutation ? mutationRate * 2f : mutationRate;
                    if (UnityEngine.Random.value < chance)
                    {
                        // Используем нормальное распределение и для смещений
                        float noise = (float)NextGaussian() * actualMutationStrength;
                        biases[i][j] += noise;
                        
                        // Ограничиваем максимальное значение смещения
                        biases[i][j] = Mathf.Clamp(biases[i][j], -5f, 5f);
                    }
                }
            }
        }
        
        // Генератор случайных чисел с нормальным распределением (для мутаций)
        private double NextGaussian()
        {
            // Используем метод Бокса-Мюллера для создания нормального распределения
            double u1 = 1.0 - UnityEngine.Random.value;
            double u2 = 1.0 - UnityEngine.Random.value;
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }

        // Кроссовер (скрещивание) двух нейросетей для получения потомка
        public static NeuralNetwork Crossover(NeuralNetwork parent1, NeuralNetwork parent2)
        {
            // Проверяем совместимость родителей
            if (parent1 == null || parent2 == null)
            {
                Debug.LogError("Один из родителей равен null!");
                return parent1 != null ? new NeuralNetwork(parent1.layers) : new NeuralNetwork(parent2.layers);
            }

            // Проверяем, что структура родителей совпадает
            if (parent1.layers.Length != parent2.layers.Length)
            {
                Debug.LogError("Структуры родительских нейросетей несовместимы (разное количество слоев)!");
                return new NeuralNetwork(parent1.layers);
            }

            for (int i = 0; i < parent1.layers.Length; i++)
            {
                if (parent1.layers[i] != parent2.layers[i])
                {
                    Debug.LogError($"Структуры родительских нейросетей несовместимы (разные размеры слоя {i})!");
                    return new NeuralNetwork(parent1.layers);
                }
            }

            // Создаем потомка с той же структурой
            NeuralNetwork offspring = new NeuralNetwork(parent1.layers);
            
            // Наследуем параметры активации
            offspring.leakyReLUFactor = UnityEngine.Random.value < 0.5f ? 
                parent1.leakyReLUFactor : parent2.leakyReLUFactor;
            offspring.useGELU = UnityEngine.Random.value < 0.5f ? 
                parent1.useGELU : parent2.useGELU;

            // Используем более продвинутый кроссовер с интерполяцией для более плавных переходов
            for (int i = 0; i < offspring.weights.Length; i++)
            {
                for (int j = 0; j < offspring.weights[i].Length; j++)
                {
                    // Решаем для каждого нейрона, наследовать ли все веса от одного родителя 
                    // или делать кроссовер на уровне отдельных весов
                    bool neuronwiseCrossover = UnityEngine.Random.value < 0.7f;
                    float parentChoice = UnityEngine.Random.value;
                    
                    for (int k = 0; k < offspring.weights[i][j].Length; k++)
                    {
                        if (neuronwiseCrossover) {
                            // Полностью наследуем веса нейрона от одного родителя
                            offspring.weights[i][j][k] = parentChoice < 0.5f ? 
                                parent1.weights[i][j][k] : parent2.weights[i][j][k];
                        } else {
                            // Для каждого веса делаем отдельный выбор или интерполяцию
                            if (UnityEngine.Random.value < 0.8f) {
                                // Выбираем от одного из родителей
                                offspring.weights[i][j][k] = UnityEngine.Random.value < 0.5f ? 
                                    parent1.weights[i][j][k] : parent2.weights[i][j][k];
                            } else {
                                // Выполняем интерполяцию между родительскими весами
                                float t = UnityEngine.Random.value;
                                offspring.weights[i][j][k] = parent1.weights[i][j][k] * (1-t) + parent2.weights[i][j][k] * t;
                            }
                        }
                    }
                    
                    // Для bias используем тот же подход, что и для весов этого нейрона
                    if (neuronwiseCrossover) {
                        offspring.biases[i][j] = parentChoice < 0.5f ? 
                            parent1.biases[i][j] : parent2.biases[i][j];
                    } else {
                        if (UnityEngine.Random.value < 0.8f) {
                            offspring.biases[i][j] = UnityEngine.Random.value < 0.5f ? 
                                parent1.biases[i][j] : parent2.biases[i][j];
                        } else {
                            float t = UnityEngine.Random.value;
                            offspring.biases[i][j] = parent1.biases[i][j] * (1-t) + parent2.biases[i][j] * t;
                        }
                    }
                }
            }

            // Наследуем фитнес как среднее от родителей
            offspring.fitness = (parent1.fitness + parent2.fitness) / 2f;

            return offspring;
        }

        // Создание полной копии нейросети
        public NeuralNetwork Clone()
        {
            NeuralNetwork clone = new NeuralNetwork(this.layers);
            
            // Копируем настройки активации
            clone.leakyReLUFactor = this.leakyReLUFactor;
            clone.useGELU = this.useGELU;
            
            // Копируем веса
            for (int i = 0; i < weights.Length; i++)
            {
                for (int j = 0; j < weights[i].Length; j++)
                {
                    for (int k = 0; k < weights[i][j].Length; k++)
                    {
                        clone.weights[i][j][k] = this.weights[i][j][k];
                    }
                }
            }
            
            // Копируем смещения
            for (int i = 0; i < biases.Length; i++)
            {
                for (int j = 0; j < biases[i].Length; j++)
                {
                    clone.biases[i][j] = this.biases[i][j];
                }
            }
            
            // Копируем фитнес
            clone.fitness = this.fitness;
            
            return clone;
        }

        // Строковое представление нейросети для отладки
        public override string ToString()
        {
            string description = $"Нейросеть [";
            for (int i = 0; i < layers.Length; i++)
            {
                description += layers[i];
                if (i < layers.Length - 1) description += "-";
            }
            description += $"] Фитнес: {fitness}, Активация: {(useGELU ? "GELU" : "LeakyReLU")}";
            return description;
        }
    }
} 