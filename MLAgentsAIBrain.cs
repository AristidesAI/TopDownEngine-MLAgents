using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using MoreMountains.TopDownEngine;
using MoreMountains.Tools;
using System.Collections.Generic;

namespace SpookNet.MLAgents
{
    /// <summary>
    /// ML-Agents adapter for TopDownEngine AI system. This component bridges ML-Agents
    /// with TopDownEngine's AIBrain, allowing ML-Agents to control TopDownEngine characters.
    /// </summary>
    public class MLAgentsAIBrain : Agent
    {
        [Header("TopDownEngine Integration")]
        [Tooltip("The AIBrain component to integrate with. If null, will try to find it on the same GameObject.")]
        public AIBrain LinkedAIBrain;
        
        [Tooltip("The character component. If null, will try to find it in parent.")]
        public Character LinkedCharacter;
        
        [Tooltip("The TopDownController for movement. If null, will try to find it.")]
        public TopDownController LinkedController;

        [Header("Observation Settings")]
        [Tooltip("Include the agent's position and velocity in observations")]
        public bool IncludePositionObservations = true;
        
        [Tooltip("Include threat detection observations")]
        public bool IncludeThreatObservations = true;
        
        [Tooltip("Number of raycast directions for wall detection")]
        public int WallDetectionRays = 8;
        
        [Tooltip("Maximum distance for wall detection raycasts")]
        public float WallDetectionDistance = 5f;
        
        [Tooltip("Layer mask for wall detection")]
        public LayerMask WallLayerMask = -1;

        [Header("Action Settings")]
        [Tooltip("Movement speed multiplier for normal movement")]
        public float NormalSpeedMultiplier = 1f;
        
        [Tooltip("Movement speed multiplier when threatened")]
        public float ThreatenedSpeedMultiplier = 1.5f;
        
        [Tooltip("Movement speed multiplier when in immediate danger")]
        public float DangerSpeedMultiplier = 2f;

        [Header("Threat Detection")]
        [Tooltip("Reference to the player transform")]
        public Transform PlayerTransform;
        
        [Tooltip("Distance at which the agent considers itself threatened")]
        public float ThreatDistance = 10f;
        
        [Tooltip("Distance at which the agent considers itself in immediate danger")]
        public float DangerDistance = 3f;
        
        [Header("Memory System")]
        [Tooltip("How long to remember the player's last known position (seconds)")]
        public float MemoryDuration = 5f;
        
        // Private variables
        private Vector3 _lastKnownPlayerPosition;
        private float _lastPlayerSeenTime;
        private bool _playerInSight;
        private float _currentThreatLevel; // 0 = safe, 1 = threatened, 2 = danger
        private CharacterMovement _characterMovement;
        private Health _health;
        private MMConeOfVision2D _coneOfVision;
        private Collider2D _collider2D;
        
        // Action buffer indices
        private const int ACTION_MOVE_X = 0;
        private const int ACTION_MOVE_Y = 1;
        private const int ACTION_SPEED_MODE = 0; // Discrete action

        protected override void Awake()
        {
            base.Awake();
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // Find required components if not assigned
            if (LinkedAIBrain == null)
                LinkedAIBrain = GetComponent<AIBrain>();
                
            if (LinkedCharacter == null)
                LinkedCharacter = GetComponentInParent<Character>();
                
            if (LinkedController == null)
                LinkedController = GetComponentInParent<TopDownController>();
                
            // Get additional components
            _characterMovement = LinkedCharacter?.FindAbility<CharacterMovement>();
            _health = GetComponentInParent<Health>();
            _coneOfVision = GetComponentInChildren<MMConeOfVision2D>();
            _collider2D = GetComponentInParent<Collider2D>();
            
            // Validate components
            if (LinkedCharacter == null || LinkedController == null)
            {
                Debug.LogError("MLAgentsAIBrain: Missing required TopDownEngine components!");
                enabled = false;
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            
            // Find player if not assigned
            if (PlayerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    PlayerTransform = player.transform;
            }
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            // Agent position and velocity (4 values)
            if (IncludePositionObservations)
            {
                sensor.AddObservation(transform.localPosition.x);
                sensor.AddObservation(transform.localPosition.y);
                sensor.AddObservation(LinkedController.Speed.x);
                sensor.AddObservation(LinkedController.Speed.y);
            }
            
            // Threat observations (7 values)
            if (IncludeThreatObservations)
            {
                UpdateThreatDetection();
                
                // Current threat level
                sensor.AddObservation(_currentThreatLevel);
                
                // Player visibility
                sensor.AddObservation(_playerInSight ? 1f : 0f);
                
                // Last known player position (relative to agent)
                if (_lastKnownPlayerPosition != Vector3.zero)
                {
                    Vector3 relativePos = _lastKnownPlayerPosition - transform.position;
                    sensor.AddObservation(relativePos.x);
                    sensor.AddObservation(relativePos.y);
                }
                else
                {
                    sensor.AddObservation(0f);
                    sensor.AddObservation(0f);
                }
                
                // Time since player was last seen (normalized)
                float timeSinceLastSeen = _playerInSight ? 0f : (Time.time - _lastPlayerSeenTime) / MemoryDuration;
                sensor.AddObservation(Mathf.Clamp01(timeSinceLastSeen));
                
                // Distance to player (if known)
                float distanceToPlayer = PlayerTransform != null ? 
                    Vector3.Distance(transform.position, PlayerTransform.position) / ThreatDistance : 1f;
                sensor.AddObservation(distanceToPlayer);
                
                // Direction to last known player position
                if (_lastKnownPlayerPosition != Vector3.zero)
                {
                    Vector3 dirToPlayer = (_lastKnownPlayerPosition - transform.position).normalized;
                    sensor.AddObservation(dirToPlayer.x);
                    sensor.AddObservation(dirToPlayer.y);
                }
                else
                {
                    sensor.AddObservation(0f);
                    sensor.AddObservation(0f);
                }
            }
            
            // Wall detection raycasts
            CollectWallObservations(sensor);
        }

        private void CollectWallObservations(VectorSensor sensor)
        {
            float angleStep = 360f / WallDetectionRays;
            Vector2 origin = transform.position;
            
            for (int i = 0; i < WallDetectionRays; i++)
            {
                float angle = i * angleStep;
                Vector2 direction = Quaternion.Euler(0, 0, angle) * Vector2.right;
                
                RaycastHit2D hit = Physics2D.Raycast(origin, direction, WallDetectionDistance, WallLayerMask);
                
                if (hit.collider != null)
                {
                    // Normalized distance to wall
                    sensor.AddObservation(hit.distance / WallDetectionDistance);
                }
                else
                {
                    // No wall detected within range
                    sensor.AddObservation(1f);
                }
                
                // Debug visualization
                Debug.DrawRay(origin, direction * (hit.collider != null ? hit.distance : WallDetectionDistance), 
                    hit.collider != null ? Color.red : Color.green);
            }
        }

        private void UpdateThreatDetection()
        {
            if (PlayerTransform == null) return;
            
            // Check if player is in cone of vision
            _playerInSight = false;
            if (_coneOfVision != null && _coneOfVision.VisibleTargets.Contains(PlayerTransform))
            {
                _playerInSight = true;
                _lastKnownPlayerPosition = PlayerTransform.position;
                _lastPlayerSeenTime = Time.time;
            }
            
            // Update threat level based on distance
            float distanceToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);
            
            if (distanceToPlayer <= DangerDistance && _playerInSight)
            {
                _currentThreatLevel = 2f; // Immediate danger
            }
            else if (distanceToPlayer <= ThreatDistance && _playerInSight)
            {
                _currentThreatLevel = 1f; // Threatened
            }
            else if (Time.time - _lastPlayerSeenTime < MemoryDuration && _lastKnownPlayerPosition != Vector3.zero)
            {
                // Still alert based on memory
                _currentThreatLevel = 0.5f;
            }
            else
            {
                _currentThreatLevel = 0f; // Safe
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            // Get movement from continuous actions
            float moveX = actions.ContinuousActions[ACTION_MOVE_X];
            float moveY = actions.ContinuousActions[ACTION_MOVE_Y];
            
            // Get speed mode from discrete actions
            int speedMode = actions.DiscreteActions[ACTION_SPEED_MODE];
            
            // Apply movement
            ApplyMovement(moveX, moveY, speedMode);
            
            // Apply rewards
            ApplyRewards();
        }

        private void ApplyMovement(float moveX, float moveY, int speedMode)
        {
            // Create movement vector
            Vector2 movement = new Vector2(moveX, moveY);
            
            // Clamp to unit circle to prevent faster diagonal movement
            if (movement.magnitude > 1f)
                movement = movement.normalized;
            
            // Apply speed multiplier based on speed mode
            float speedMultiplier = GetSpeedMultiplier(speedMode);
            movement *= speedMultiplier;
            
            // Send movement to TopDownController
            if (LinkedController != null)
            {
                LinkedController.SetMovement(movement);
            }
            
            // Update character movement ability if needed
            if (_characterMovement != null)
            {
                _characterMovement.SetMovement(movement);
            }
        }

        private float GetSpeedMultiplier(int speedMode)
        {
            switch (speedMode)
            {
                case 0: // Normal speed
                    return NormalSpeedMultiplier;
                case 1: // Fast speed (threatened)
                    return ThreatenedSpeedMultiplier;
                case 2: // Fastest speed (danger)
                    return DangerSpeedMultiplier;
                default:
                    return NormalSpeedMultiplier;
            }
        }

        private void ApplyRewards()
        {
            // Survival reward (small positive reward each step)
            AddReward(0.001f);
            
            // Distance-based reward
            if (PlayerTransform != null && _playerInSight)
            {
                float distance = Vector3.Distance(transform.position, PlayerTransform.position);
                
                // Reward for maintaining distance from player when visible
                if (distance > DangerDistance && distance < ThreatDistance)
                {
                    AddReward(0.01f); // Good distance
                }
                else if (distance >= ThreatDistance)
                {
                    AddReward(0.005f); // Safe distance
                }
                else
                {
                    AddReward(-0.02f); // Too close, danger!
                }
            }
            
            // Movement reward (encourage movement over standing still)
            if (LinkedController.Speed.magnitude > 0.1f)
            {
                AddReward(0.002f);
            }
            else
            {
                AddReward(-0.001f); // Small penalty for not moving
            }
            
            // Check if stuck against wall (penalty)
            if (IsStuckAgainstWall())
            {
                AddReward(-0.01f);
            }
        }

        private bool IsStuckAgainstWall()
        {
            // Simple check: if we're trying to move but speed is very low
            return LinkedController.CurrentMovement.magnitude > 0.5f && 
                   LinkedController.Speed.magnitude < 0.1f;
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            // Heuristic for testing - simple flee behavior
            var continuousActions = actionsOut.ContinuousActions;
            var discreteActions = actionsOut.DiscreteActions;
            
            if (PlayerTransform != null && _playerInSight)
            {
                // Move away from player
                Vector3 fleeDirection = (transform.position - PlayerTransform.position).normalized;
                continuousActions[ACTION_MOVE_X] = fleeDirection.x;
                continuousActions[ACTION_MOVE_Y] = fleeDirection.y;
                
                // Use appropriate speed based on distance
                if (_currentThreatLevel >= 2f)
                    discreteActions[ACTION_SPEED_MODE] = 2; // Fastest
                else if (_currentThreatLevel >= 1f)
                    discreteActions[ACTION_SPEED_MODE] = 1; // Fast
                else
                    discreteActions[ACTION_SPEED_MODE] = 0; // Normal
            }
            else
            {
                // Random movement when no threat
                continuousActions[ACTION_MOVE_X] = Random.Range(-1f, 1f);
                continuousActions[ACTION_MOVE_Y] = Random.Range(-1f, 1f);
                discreteActions[ACTION_SPEED_MODE] = 0;
            }
        }

        // Called when the agent is tagged/caught
        public void OnTagged()
        {
            AddReward(-10f); // Large negative reward
            EndEpisode();
        }
        
        // Called when time runs out (agent survived)
        public void OnSurvived()
        {
            AddReward(5f); // Bonus for surviving the round
            EndEpisode();
        }
        
        // Integration with TopDownEngine's health system
        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnDeath += OnDeath;
            }
        }
        
        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnDeath -= OnDeath;
            }
        }
        
        private void OnDeath()
        {
            OnTagged();
        }
    }
} 