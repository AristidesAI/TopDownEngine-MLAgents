using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;

namespace SpookNet.MLAgents
{
    /// <summary>
    /// Stub version of runtime inference that doesn't require Unity Sentis.
    /// Use this if you're having issues with Sentis compilation.
    /// For actual deployment, you'll need to implement proper inference.
    /// </summary>
    public class MLAgentsRuntimeInference_Stub : MonoBehaviour
    {
        [Header("Model Configuration")]
        [Tooltip("Placeholder for ONNX model path")]
        public string ModelPath = "Assets/Models/spook_ai.onnx";
        
        [Header("Inference Settings")]
        [Tooltip("Use deterministic inference")]
        public bool Deterministic = true;
        
        [Tooltip("Random seed for stochastic actions")]
        public int Seed = 42;

        [Header("References")]
        [Tooltip("The ML-Agents AIBrain to provide actions for")]
        public MLAgentsAIBrain LinkedAgent;
        
        [Tooltip("Override the BehaviorParameters inference model")]
        public bool OverrideBehaviorParameters = true;

        private System.Random _random;
        private bool _isInitialized = false;
        
        private void Awake()
        {
            if (LinkedAgent == null)
            {
                LinkedAgent = GetComponent<MLAgentsAIBrain>();
            }
            
            _random = new System.Random(Seed);
        }

        private void Start()
        {
            InitializeInference();
            
            if (OverrideBehaviorParameters && LinkedAgent != null)
            {
                OverrideAgentInference();
            }
        }

        private void InitializeInference()
        {
            // Stub implementation - no actual model loading
            Debug.LogWarning("MLAgentsRuntimeInference_Stub: This is a stub implementation. " +
                           "For actual inference, implement Unity Sentis or ONNX Runtime integration.");
            
            _isInitialized = true;
        }

        private void OverrideAgentInference()
        {
            var behaviorParams = LinkedAgent.GetComponent<BehaviorParameters>();
            if (behaviorParams != null)
            {
                // For training, keep default behavior
                // For deployment, you would set this to InferenceOnly
                Debug.Log("MLAgentsRuntimeInference_Stub: Agent inference mode not changed (stub implementation)");
            }
        }

        /// <summary>
        /// Performs fake inference for testing
        /// </summary>
        public void DoInference(float[] observations, out float[] continuousActions, out int[] discreteActions)
        {
            // For testing, return random actions
            // In production, this would call your inference engine
            
            continuousActions = new float[2]; // Assuming 2D movement
            discreteActions = new int[0];     // No discrete actions in this example
            
            if (!_isInitialized)
            {
                Debug.LogError("MLAgentsRuntimeInference_Stub: Not initialized!");
                return;
            }
            
            // Generate random continuous actions for testing
            if (Deterministic)
            {
                // Use seeded random for deterministic behavior
                continuousActions[0] = (float)(_random.NextDouble() * 2 - 1); // X movement [-1, 1]
                continuousActions[1] = (float)(_random.NextDouble() * 2 - 1); // Y movement [-1, 1]
            }
            else
            {
                // Use Unity's random for non-deterministic behavior
                continuousActions[0] = Random.Range(-1f, 1f);
                continuousActions[1] = Random.Range(-1f, 1f);
            }
            
            Debug.Log($"MLAgentsRuntimeInference_Stub: Generated actions [{continuousActions[0]:F2}, {continuousActions[1]:F2}]");
        }

        /// <summary>
        /// Placeholder for model updates
        /// </summary>
        public void UpdateModel(string newModelPath)
        {
            ModelPath = newModelPath;
            Debug.Log($"MLAgentsRuntimeInference_Stub: Model path updated to {newModelPath} (no actual loading in stub)");
        }

        /// <summary>
        /// Provides a simple API for external scripts to get actions
        /// </summary>
        public (float[] continuous, int[] discrete) GetActions(float[] observations)
        {
            float[] continuous;
            int[] discrete;
            DoInference(observations, out continuous, out discrete);
            return (continuous, discrete);
        }
    }
} 