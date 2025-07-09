using UnityEngine;
using MoreMountains.TopDownEngine;
using MoreMountains.Tools;

namespace SpookNet.MLAgents
{
    /// <summary>
    /// A TopDownEngine AIAction that allows ML-Agents to control the character.
    /// This action essentially does nothing, allowing ML-Agents to handle all decisions.
    /// </summary>
    [AddComponentMenu("TopDown Engine/Character/AI/Actions/AI Action ML Agents")]
    public class AIActionMLAgents : AIAction
    {
        [Header("ML-Agents Integration")]
        [Tooltip("If true, this action will request decisions from ML-Agents")]
        public bool RequestDecisions = true;
        
        [Tooltip("If true, will use heuristic when no model is available")]
        public bool UseHeuristicFallback = true;

        protected MLAgentsAIBrain _mlAgent;
        protected bool _initialized = false;
        
        /// <summary>
        /// On init we grab our ML-Agents component
        /// </summary>
        public override void Initialization()
        {
            base.Initialization();
            _mlAgent = GetComponentInParent<MLAgentsAIBrain>();
            
            if (_mlAgent == null)
            {
                Debug.LogError("AIActionMLAgents: No MLAgentsAIBrain found in parent!");
                enabled = false;
            }
            else
            {
                _initialized = true;
            }
        }
        
        /// <summary>
        /// On PerformAction we let ML-Agents handle everything
        /// </summary>
        public override void PerformAction()
        {
            if (!_initialized || _mlAgent == null) return;
            
            // ML-Agents handles movement through OnActionReceived
            // This action just ensures the TopDownEngine AI system doesn't interfere
            
            if (RequestDecisions)
            {
                // We can optionally request decisions here if needed
                // Though normally the DecisionRequester component handles this
                // _mlAgent.RequestDecision();
            }
        }
        
        /// <summary>
        /// Always returns true since ML-Agents is always "performing"
        /// </summary>
        public override bool ActionInProgress 
        { 
            get { return true; } 
        }
        
        /// <summary>
        /// On enter state, we could initialize ML-Agents episode if needed
        /// </summary>
        public override void OnEnterState()
        {
            base.OnEnterState();
            
            if (_mlAgent != null)
            {
                // Could trigger episode start or other ML-Agents specific logic
                Debug.Log("AIActionMLAgents: Entered ML-Agents control state");
            }
        }
        
        /// <summary>
        /// On exit state, we could end the ML-Agents episode if needed
        /// </summary>
        public override void OnExitState()
        {
            base.OnExitState();
            
            if (_mlAgent != null)
            {
                // Could trigger episode end or cleanup
                Debug.Log("AIActionMLAgents: Exited ML-Agents control state");
            }
        }
    }
} 