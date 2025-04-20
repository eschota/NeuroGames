using UnityEngine;

namespace Game.Scripts.GamePlay
{
    // Заглушка для обратной совместимости
public class Neuro : MonoBehaviour
{
        [HideInInspector] public int instance_id;
        
        // Свойства для совместимости
        public float activity_reward = 0.01f;
        public float target_reward = 100f;
        public float collision_penalty = 50f;
        public float target_tracking_reward = 0.1f;
        public float speed_change_reward = 0.05f;
        public float rotation_change_reward = 0.05f;
        public float time_bonus_multiplier = 0.5f;
        
        // Приватные переменные
        private float fitness;
        
        // Методы для SimulationManager
        public float GetFitness() { return fitness; }
        public void SetFitness(float value) { fitness = value; }
        public NeuralNetwork GetBrain() { return null; }
        public bool IsSuccessful() { return false; }
        
        // Установка начального времени
        public void SetStartTime(float time) { }
    }
} 