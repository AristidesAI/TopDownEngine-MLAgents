using UnityEngine;
using Unity.MLAgents.Sensors;
using MoreMountains.Tools;
using System.Collections.Generic;

namespace SpookNet.MLAgents
{
    /// <summary>
    /// Custom sensor that integrates TopDownEngine's ConeOfVision with ML-Agents.
    /// Provides visual observations based on what the agent can see.
    /// </summary>
    public class MLAgentsConeOfVisionSensor : SensorComponent
    {
        [Header("Cone of Vision")]
        [Tooltip("Reference to the ConeOfVision component")]
        public MMConeOfVision2D ConeOfVision;
        
        [Header("Sensor Configuration")]
        [Tooltip("Name of the sensor")]
        public string SensorName = "ConeOfVision";
        
        [Tooltip("Number of rays to cast for detailed vision")]
        public int VisionRays = 16;
        
        [Tooltip("What information to include in observations")]
        public bool IncludeDistance = true;
        public bool IncludeTargetType = true;
        public bool IncludeTargetVelocity = true;
        
        [Header("Target Detection")]
        [Tooltip("Layer mask for different target types")]
        public LayerMask PlayerLayer;
        public LayerMask EnemyLayer;
        public LayerMask ObstacleLayer;
        
        private ConeOfVisionSensor _sensor;

        public override ISensor[] CreateSensors()
        {
            // Find ConeOfVision if not assigned
            if (ConeOfVision == null)
            {
                ConeOfVision = GetComponentInChildren<MMConeOfVision2D>();
                if (ConeOfVision == null)
                {
                    Debug.LogError("MLAgentsConeOfVisionSensor: No ConeOfVision2D component found!");
                    return new ISensor[0];
                }
            }
            
            _sensor = new ConeOfVisionSensor(this);
            return new ISensor[] { _sensor };
        }
        
        /// <summary>
        /// The actual sensor implementation
        /// </summary>
        private class ConeOfVisionSensor : ISensor
        {
            private MLAgentsConeOfVisionSensor _component;
            private string _name;
            private ObservationSpec _observationSpec;
            
            public ConeOfVisionSensor(MLAgentsConeOfVisionSensor component)
            {
                _component = component;
                _name = component.SensorName;
                
                // Calculate observation size
                int observationSize = CalculateObservationSize();
                _observationSpec = ObservationSpec.Vector(observationSize);
            }
            
            private int CalculateObservationSize()
            {
                int size = _component.VisionRays; // One value per ray for distance
                
                if (_component.IncludeTargetType)
                    size += _component.VisionRays; // Target type per ray
                    
                if (_component.IncludeTargetVelocity)
                    size += _component.VisionRays * 2; // X,Y velocity per ray
                    
                // Add visible targets info
                size += 10; // Space for up to 5 visible targets (distance + angle each)
                
                return size;
            }
            
            public int Write(ObservationWriter writer)
            {
                int observationCount = 0;
                
                // Cast rays within the cone of vision
                float angleStep = _component.ConeOfVision.VisionAngle / _component.VisionRays;
                float startAngle = -_component.ConeOfVision.VisionAngle / 2f;
                
                Vector2 origin = _component.transform.position;
                
                for (int i = 0; i < _component.VisionRays; i++)
                {
                    float currentAngle = startAngle + (angleStep * i) + _component.ConeOfVision.EulerAngles.y;
                    Vector2 direction = MMMaths.DirectionFromAngle2D(currentAngle, 0f);
                    
                    RaycastHit2D hit = Physics2D.Raycast(
                        origin, 
                        direction, 
                        _component.ConeOfVision.VisionRadius,
                        _component.PlayerLayer | _component.EnemyLayer | _component.ObstacleLayer
                    );
                    
                    // Distance observation
                    if (hit.collider != null)
                    {
                        float normalizedDistance = hit.distance / _component.ConeOfVision.VisionRadius;
                        writer[observationCount++] = normalizedDistance;
                    }
                    else
                    {
                        writer[observationCount++] = 1f; // Max distance
                    }
                    
                    // Target type observation
                    if (_component.IncludeTargetType)
                    {
                        if (hit.collider != null)
                        {
                            if (IsInLayerMask(hit.collider.gameObject.layer, _component.PlayerLayer))
                                writer[observationCount++] = 1f; // Player
                            else if (IsInLayerMask(hit.collider.gameObject.layer, _component.EnemyLayer))
                                writer[observationCount++] = 0.5f; // Enemy
                            else if (IsInLayerMask(hit.collider.gameObject.layer, _component.ObstacleLayer))
                                writer[observationCount++] = 0f; // Obstacle
                            else
                                writer[observationCount++] = -1f; // Unknown
                        }
                        else
                        {
                            writer[observationCount++] = -1f; // Nothing
                        }
                    }
                    
                    // Target velocity observation
                    if (_component.IncludeTargetVelocity)
                    {
                        if (hit.collider != null && hit.collider.attachedRigidbody != null)
                        {
                            Vector2 velocity = hit.collider.attachedRigidbody.linearVelocity;
                            writer[observationCount++] = Mathf.Clamp(velocity.x / 10f, -1f, 1f);
                            writer[observationCount++] = Mathf.Clamp(velocity.y / 10f, -1f, 1f);
                        }
                        else
                        {
                            writer[observationCount++] = 0f;
                            writer[observationCount++] = 0f;
                        }
                    }
                }
                
                // Add visible targets information
                List<Transform> visibleTargets = _component.ConeOfVision.VisibleTargets;
                int maxTargets = 5;
                
                for (int i = 0; i < maxTargets; i++)
                {
                    if (i < visibleTargets.Count && visibleTargets[i] != null)
                    {
                        Vector3 toTarget = visibleTargets[i].position - _component.transform.position;
                        float distance = toTarget.magnitude / _component.ConeOfVision.VisionRadius;
                        float angle = Vector2.SignedAngle(Vector2.right, toTarget.normalized) / 180f;
                        
                        writer[observationCount++] = Mathf.Clamp01(distance);
                        writer[observationCount++] = angle;
                    }
                    else
                    {
                        writer[observationCount++] = 1f; // Max distance
                        writer[observationCount++] = 0f; // No angle
                    }
                }
                
                return observationCount;
            }
            
            private bool IsInLayerMask(int layer, LayerMask mask)
            {
                return (mask.value & (1 << layer)) != 0;
            }
            
            public byte[] GetCompressedObservation()
            {
                return null;
            }
            
            public void Update()
            {
                // Sensor updates are handled by the ConeOfVision component
            }
            
            public void Reset()
            {
                // Nothing to reset
            }
            
            public ObservationSpec GetObservationSpec()
            {
                return _observationSpec;
            }
            
            public string GetName()
            {
                return _name;
            }
            
            public CompressionSpec GetCompressionSpec()
            {
                return CompressionSpec.Default();
            }
        }
    }
} 