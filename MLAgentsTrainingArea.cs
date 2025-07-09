using UnityEngine;
using Unity.MLAgents;
using MoreMountains.TopDownEngine;
using MoreMountains.Tools;
using System.Collections.Generic;
using System.Linq;

namespace SpookNet.MLAgents
{
    /// <summary>
    /// Manages the training environment for ML-Agents. Handles episode resets,
    /// maze regeneration, spawn points, and training metrics.
    /// </summary>
    public class MLAgentsTrainingArea : MonoBehaviour
    {
        [Header("Training Configuration")]
        [Tooltip("Maximum episode length in seconds")]
        public float MaxEpisodeLength = 180f; // 3 minutes
        
        [Tooltip("Regenerate maze on episode reset")]
        public bool RegenerateMazeOnReset = true;
        
        [Tooltip("Reset all agents when any agent is caught")]
        public bool ResetAllOnCapture = false;

        [Header("Spawn Configuration")]
        [Tooltip("Minimum distance between spawn points")]
        public float MinSpawnDistance = 5f;
        
        [Tooltip("Layer mask for valid spawn areas")]
        public LayerMask SpawnableAreaMask = -1;
        
        [Tooltip("Layer mask for obstacles to avoid when spawning")]
        public LayerMask ObstacleMask = -1;

        [Header("References")]
        [Tooltip("Reference to the maze generator (if using procedural generation)")]
        public MMTilemapGenerator MazeGenerator;
        
        [Tooltip("The player character")]
        public GameObject PlayerCharacter;
        
        [Tooltip("List of AI agents in the training area")]
        public List<MLAgentsAIBrain> AIAgents = new List<MLAgentsAIBrain>();
        
        [Tooltip("Spawn points for agents and player")]
        public Transform[] SpawnPoints;

        [Header("Training Metrics")]
        [ReadOnly] public int CurrentEpisode = 0;
        [ReadOnly] public float CurrentEpisodeTime = 0f;
        [ReadOnly] public int AgentsCaughtThisEpisode = 0;
        [ReadOnly] public float AverageEpisodeLength = 0f;
        
        // Private variables
        private float _episodeStartTime;
        private List<float> _episodeLengths = new List<float>();
        private Dictionary<MLAgentsAIBrain, Vector3> _agentStartPositions = new Dictionary<MLAgentsAIBrain, Vector3>();
        private Health _playerHealth;
        private bool _isTraining = true;

        private void Awake()
        {
            // Find components if not assigned
            if (MazeGenerator == null)
                MazeGenerator = FindObjectOfType<MMTilemapGenerator>();
                
            if (PlayerCharacter != null)
                _playerHealth = PlayerCharacter.GetComponent<Health>();
                
            // Find all AI agents if list is empty
            if (AIAgents.Count == 0)
                AIAgents = FindObjectsOfType<MLAgentsAIBrain>().ToList();
        }

        private void Start()
        {
            // Subscribe to agent events
            foreach (var agent in AIAgents)
            {
                SubscribeToAgentEvents(agent);
            }
            
            // Start first episode
            StartNewEpisode();
        }

        private void Update()
        {
            if (!_isTraining) return;
            
            // Update episode timer
            CurrentEpisodeTime = Time.time - _episodeStartTime;
            
            // Check for episode timeout
            if (CurrentEpisodeTime >= MaxEpisodeLength)
            {
                OnEpisodeTimeout();
            }
        }

        private void SubscribeToAgentEvents(MLAgentsAIBrain agent)
        {
            // We'll need to listen for when agents are caught
            // This would typically be done through the tagging system
            var health = agent.GetComponent<Health>();
            if (health != null)
            {
                health.OnDeath += () => OnAgentCaught(agent);
            }
        }

        public void StartNewEpisode()
        {
            CurrentEpisode++;
            _episodeStartTime = Time.time;
            AgentsCaughtThisEpisode = 0;
            
            // Regenerate maze if configured
            if (RegenerateMazeOnReset && MazeGenerator != null)
            {
                RegenerateMaze();
            }
            
            // Reset all agents and player
            ResetAllCharacters();
            
            // Log episode start
            Debug.Log($"[MLAgentsTrainingArea] Starting Episode {CurrentEpisode}");
        }

        private void RegenerateMaze()
        {
            // Generate new maze
            MazeGenerator.Generate();
            
            // Wait a frame for physics to update
            StartCoroutine(WaitAndRespawnCharacters());
        }

        private System.Collections.IEnumerator WaitAndRespawnCharacters()
        {
            yield return new WaitForFixedUpdate();
            ResetAllCharacters();
        }

        private void ResetAllCharacters()
        {
            // Generate spawn positions
            List<Vector3> usedSpawnPoints = new List<Vector3>();
            
            // Spawn player first
            if (PlayerCharacter != null)
            {
                Vector3 playerSpawn = GetValidSpawnPoint(usedSpawnPoints);
                PlayerCharacter.transform.position = playerSpawn;
                usedSpawnPoints.Add(playerSpawn);
                
                // Reset player state
                ResetCharacterState(PlayerCharacter);
            }
            
            // Spawn AI agents
            foreach (var agent in AIAgents)
            {
                if (agent != null && agent.gameObject.activeInHierarchy)
                {
                    Vector3 agentSpawn = GetValidSpawnPoint(usedSpawnPoints);
                    agent.transform.position = agentSpawn;
                    usedSpawnPoints.Add(agentSpawn);
                    
                    // Store start position
                    _agentStartPositions[agent] = agentSpawn;
                    
                    // Reset agent state
                    ResetCharacterState(agent.gameObject);
                    
                    // Reset ML-Agents episode
                    agent.EndEpisode();
                }
            }
        }

        private Vector3 GetValidSpawnPoint(List<Vector3> usedPoints)
        {
            Vector3 spawnPoint = Vector3.zero;
            int maxAttempts = 100;
            int attempts = 0;
            
            // If we have predefined spawn points, try those first
            if (SpawnPoints != null && SpawnPoints.Length > 0)
            {
                List<Transform> availablePoints = SpawnPoints.Where(sp => sp != null).ToList();
                
                while (attempts < maxAttempts && availablePoints.Count > 0)
                {
                    int randomIndex = Random.Range(0, availablePoints.Count);
                    spawnPoint = availablePoints[randomIndex].position;
                    
                    if (IsValidSpawnPoint(spawnPoint, usedPoints))
                    {
                        return spawnPoint;
                    }
                    
                    availablePoints.RemoveAt(randomIndex);
                    attempts++;
                }
            }
            
            // If no valid predefined points, generate random positions
            Bounds searchBounds = GetSearchBounds();
            
            while (attempts < maxAttempts)
            {
                float x = Random.Range(searchBounds.min.x, searchBounds.max.x);
                float y = Random.Range(searchBounds.min.y, searchBounds.max.y);
                spawnPoint = new Vector3(x, y, 0);
                
                if (IsValidSpawnPoint(spawnPoint, usedPoints))
                {
                    return spawnPoint;
                }
                
                attempts++;
            }
            
            // Fallback: return any position
            Debug.LogWarning("[MLAgentsTrainingArea] Could not find valid spawn point, using fallback");
            return spawnPoint;
        }

        private bool IsValidSpawnPoint(Vector3 point, List<Vector3> usedPoints)
        {
            // Check distance from other spawn points
            foreach (var usedPoint in usedPoints)
            {
                if (Vector3.Distance(point, usedPoint) < MinSpawnDistance)
                    return false;
            }
            
            // Check if point is in walkable area
            Collider2D overlap = Physics2D.OverlapCircle(point, 0.5f, ObstacleMask);
            if (overlap != null)
                return false;
            
            // Additional validation can be added here
            return true;
        }

        private Bounds GetSearchBounds()
        {
            // Get bounds from maze generator if available
            if (MazeGenerator != null && MazeGenerator.GetComponent<Collider2D>() != null)
            {
                return MazeGenerator.GetComponent<Collider2D>().bounds;
            }
            
            // Otherwise, use a default area
            return new Bounds(Vector3.zero, new Vector3(50f, 50f, 0f));
        }

        private void ResetCharacterState(GameObject character)
        {
            // Reset health
            var health = character.GetComponent<Health>();
            if (health != null)
            {
                health.Revive();
                health.ResetHealthToMaxHealth();
            }
            
            // Reset movement
            var controller = character.GetComponent<TopDownController>();
            if (controller != null)
            {
                controller.SetMovement(Vector2.zero);
            }
            
            // Reset any status effects
            var statusEffects = character.GetComponent<CharacterStates>();
            if (statusEffects != null)
            {
                // Reset states as needed
            }
            
            // Enable character if it was disabled
            character.SetActive(true);
        }

        private void OnAgentCaught(MLAgentsAIBrain agent)
        {
            AgentsCaughtThisEpisode++;
            
            Debug.Log($"[MLAgentsTrainingArea] Agent caught! Total this episode: {AgentsCaughtThisEpisode}");
            
            // Notify the agent
            agent.OnTagged();
            
            // Check if we should reset all agents
            if (ResetAllOnCapture)
            {
                EndEpisode(false);
            }
            else
            {
                // Just disable this agent
                agent.gameObject.SetActive(false);
                
                // Check if all agents are caught
                if (AgentsCaughtThisEpisode >= AIAgents.Count)
                {
                    EndEpisode(false);
                }
            }
        }

        private void OnEpisodeTimeout()
        {
            Debug.Log($"[MLAgentsTrainingArea] Episode timeout - agents survived!");
            
            // Notify all active agents they survived
            foreach (var agent in AIAgents)
            {
                if (agent != null && agent.gameObject.activeInHierarchy)
                {
                    agent.OnSurvived();
                }
            }
            
            EndEpisode(true);
        }

        private void EndEpisode(bool agentsSurvived)
        {
            // Record episode length
            float episodeLength = Time.time - _episodeStartTime;
            _episodeLengths.Add(episodeLength);
            
            // Update average
            AverageEpisodeLength = _episodeLengths.Average();
            
            // Log episode results
            Debug.Log($"[MLAgentsTrainingArea] Episode {CurrentEpisode} ended. " +
                     $"Length: {episodeLength:F1}s, " +
                     $"Agents caught: {AgentsCaughtThisEpisode}/{AIAgents.Count}, " +
                     $"Result: {(agentsSurvived ? "Agents Win" : "Player Win")}");
            
            // Start new episode
            StartNewEpisode();
        }

        public void SetTrainingMode(bool isTraining)
        {
            _isTraining = isTraining;
            
            // Adjust time scale for training
            if (isTraining)
            {
                Time.timeScale = 1f; // Can be increased for faster training
            }
            else
            {
                Time.timeScale = 1f;
            }
        }

        private void OnDrawGizmos()
        {
            // Draw spawn points
            if (SpawnPoints != null)
            {
                Gizmos.color = Color.green;
                foreach (var spawn in SpawnPoints)
                {
                    if (spawn != null)
                        Gizmos.DrawWireSphere(spawn.position, 0.5f);
                }
            }
            
            // Draw agent positions
            Gizmos.color = Color.red;
            foreach (var agent in AIAgents)
            {
                if (agent != null)
                    Gizmos.DrawWireSphere(agent.transform.position, 0.3f);
            }
            
            // Draw player position
            if (PlayerCharacter != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(PlayerCharacter.transform.position, 0.3f);
            }
        }
    }
} 